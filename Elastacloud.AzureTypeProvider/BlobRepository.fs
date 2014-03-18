///Contains helper functions for accessing blobs
module Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

type internal containerItem =
    | Folder of path:string * name:string * contents:(unit -> array<containerItem>)
    | Blob of path:string * name:string * properties:BlobProperties

type internal LightweightContainer = 
    { Name : string
      GetFiles : unit -> seq<containerItem> }

let private getBlobClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let private getBlobRef (connection,container,file) = 
    (getBlobClient connection).GetContainerReference(container).GetBlockBlobReference(file)

/// Generates a set of lightweight container lists for a blob storage account
let private getItemName (item : string) (parent : CloudBlobDirectory) = 
    item, if parent = null
                then item
                else item.Substring(parent.Prefix.Length)

let rec private getContainerStructure wildcard (container : CloudBlobContainer) = 
    container.ListBlobs(prefix = wildcard)
    |> Seq.distinctBy (fun b -> b.Uri.AbsoluteUri)
    |> Seq.choose (fun item -> 
           match item with
           | :? CloudBlobDirectory as directory -> 
               let path,name = getItemName directory.Prefix directory.Parent
               Some (Folder(path,name,(fun () -> container |> getContainerStructure directory.Prefix)))
           | :? CloudBlockBlob as blob ->
               let path, name = getItemName blob.Name blob.Parent
               Some(Blob(path, name, blob.Properties))
           | :? CloudPageBlob -> None //todo: Handle page blobs!
           | _ -> failwith "unknown type")
    |> Seq.toArray


let internal getBlobStorageAccountManifest connection = 
    (getBlobClient connection).ListContainers()
    |> Seq.toList
    |> List.map (fun c -> 
           { Name = c.Name
             GetFiles = 
                 (fun _ -> 
                 c
                 |> getContainerStructure null
                 |> Seq.cache) })

let downloadText connection container file = 
    let blobRef = getBlobRef (connection,container,file)
    blobRef.DownloadText()

let downloadTextAsync connection container file = 
    let blobRef = getBlobRef (connection,container,file)
    Async.AwaitTask(blobRef.DownloadTextAsync())

let private awaitUnit = Async.AwaitIAsyncResult >> Async.Ignore

let downloadStream connection container file = 
    let blobRef = getBlobRef (connection,container,file)
    blobRef.OpenRead()

let downloadToFile connection container file path = 
    let blobRef = getBlobRef (connection,container,file)

    let targetDirectory = Path.GetDirectoryName(path)
    if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
    blobRef.DownloadToFileAsync(path, FileMode.Create) |> awaitUnit

let downloadFolder connection container folderPath path =
    let containerRef = (getBlobClient connection).GetContainerReference(container)
    containerRef.ListBlobs(prefix = folderPath, useFlatBlobListing = true)
    |> Seq.choose(fun b -> match b with
                           | :? CloudBlockBlob as b -> Some b
                           | _ -> None)
    |> Seq.map(fun blob ->
        let targetName = match folderPath with
                        | folderPath when String.IsNullOrEmpty folderPath -> blob.Name
                        | _ -> blob.Name.Replace(folderPath, String.Empty)
        downloadToFile connection container blob.Name (Path.Combine(path, targetName)))
    |> Async.Parallel
    |> Async.Ignore
    |> Async.Start

let uploadFile connection container path = 
    let fileName = Path.GetFileName path
    let blobRef = getBlobRef(connection,container,fileName)
    awaitUnit (blobRef.UploadFromFileAsync(path, FileMode.Open))

let getFileDetails connection container file = 
    let blobRef = getBlobRef (connection,container,file)
    blobRef.FetchAttributes()
    blobRef.Uri.AbsoluteUri, blobRef.Properties

let getSas connection container file duration permissions = 
    let blobRef = getBlobRef (connection,container,file)
    let expiry = Nullable<DateTimeOffset>(DateTimeOffset.UtcNow.Add(duration))
    let policy = SharedAccessBlobPolicy(SharedAccessExpiryTime = expiry, Permissions = permissions)
    let sas = blobRef.GetSharedAccessSignature policy
    Uri(sprintf "%s%s" (blobRef.Uri.ToString()) sas)