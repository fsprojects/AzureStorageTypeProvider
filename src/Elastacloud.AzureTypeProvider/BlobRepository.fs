///Contains reusable helper functions for accessing blobs
module internal Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

type containerItem = 
    | Folder of path : string * name : string * contents : (unit -> array<containerItem>)
    | Blob of path : string * name : string * properties : BlobProperties

type LightweightContainer = 
    { Name : string
      GetFiles : unit -> seq<containerItem> }

let getBlobClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let getBlobRef (connection, container, file) = (getBlobClient connection).GetContainerReference(container).GetBlockBlobReference(file)

let private getItemName (item : string) (parent : CloudBlobDirectory) = 
    item, 
    if parent = null then item
    else item.Substring(parent.Prefix.Length)

let rec private getContainerStructure wildcard (container : CloudBlobContainer) = 
    container.ListBlobs(prefix = wildcard)
    |> Seq.distinctBy (fun b -> b.Uri.AbsoluteUri)
    |> Seq.choose (fun item -> 
           match item with
           | :? CloudBlobDirectory as directory -> 
               let path, name = getItemName directory.Prefix directory.Parent
               Some(Folder(path, name, (fun () -> container |> getContainerStructure directory.Prefix)))
           | :? CloudBlockBlob as blob -> 
               let path, name = getItemName blob.Name blob.Parent
               Some(Blob(path, name, blob.Properties))
           | :? CloudPageBlob -> None //todo: Handle page blobs!
           | _ -> failwith "unknown type")
    |> Seq.toArray

let getBlobStorageAccountManifest connection = 
    (getBlobClient connection).ListContainers()
    |> Seq.toList
    |> List.map (fun container -> 
           { Name = container.Name
             GetFiles = (fun _ -> container
                                  |> getContainerStructure null
                                  |> Seq.cache) })

let awaitUnit = Async.AwaitIAsyncResult >> Async.Ignore

let downloadFolder (connectionDetails, path) =
    let downloadFile(connectionDetails, destination) =
        let blobRef = getBlobRef (connectionDetails)
        let targetDirectory = Path.GetDirectoryName(destination)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(destination, FileMode.Create) |> awaitUnit

    let connection, container, folderPath = connectionDetails
    let containerRef = (getBlobClient connection).GetContainerReference(container)
    containerRef.ListBlobs(prefix = folderPath, useFlatBlobListing = true)
    |> Seq.choose (fun b -> 
           match b with
           | :? CloudBlockBlob as b -> Some b
           | _ -> None)
    |> Seq.map (fun blob -> 
           let targetName = 
               match folderPath with
               | folderPath when String.IsNullOrEmpty folderPath -> blob.Name
               | _ -> blob.Name.Replace(folderPath, String.Empty)
           downloadFile((connection,container,blob.Name),(Path.Combine(path, targetName))))
    |> Async.Parallel
    |> Async.Ignore
    |> Async.Start

