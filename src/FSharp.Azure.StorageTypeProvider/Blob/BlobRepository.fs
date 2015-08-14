///Contains reusable helper functions for accessing blobs
module internal FSharp.Azure.StorageTypeProvider.Blob.BlobRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

type ContainerItem = 
    | Folder of path : string * name : string * contents : (unit -> array<ContainerItem>)
    | Blob of path : string * name : string * properties : BlobProperties

type LightweightContainer = 
    { Name : string
      GetFiles : unit -> seq<ContainerItem> }

let getBlobClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let getContainerRef(connection, container) = (getBlobClient connection).GetContainerReference(container)
let getBlockBlobRef (connection, container, file) = getContainerRef(connection, container).GetBlockBlobReference(file)
let getPageBlobRef (connection, container, file) = getContainerRef(connection, container).GetPageBlobReference(file)

let private getItemName (item : string) (parent : CloudBlobDirectory) = 
    item, 
    if parent = null then item
    else item.Substring(parent.Prefix.Length)

let rec private getContainerStructure wildcard (container : CloudBlobContainer) = 
    container.ListBlobs(prefix = wildcard)
    |> Seq.distinctBy (fun b -> b.Uri.AbsoluteUri)
    |> Seq.choose (function
       | :? CloudBlobDirectory as directory -> 
           let path, name = getItemName directory.Prefix directory.Parent
           Some(Folder(path, name, (fun () -> container |> getContainerStructure directory.Prefix)))
       | :? ICloudBlob as blob ->
           let path, name = getItemName blob.Name blob.Parent
           Some(Blob(path, name, blob.Properties))
       | _ -> None)
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
    let downloadFile (blobRef:ICloudBlob) destination =
        let targetDirectory = Path.GetDirectoryName(destination)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(destination, FileMode.Create) |> awaitUnit

    let connection, container, folderPath = connectionDetails
    let containerRef = (getBlobClient connection).GetContainerReference(container)
    containerRef.ListBlobs(prefix = folderPath, useFlatBlobListing = true)
    |> Seq.choose (fun b -> 
           match b with
           | :? ICloudBlob as b -> Some b
           | _ -> None)
    |> Seq.map (fun blob -> 
           let targetName = 
               match folderPath with
               | folderPath when String.IsNullOrEmpty folderPath -> blob.Name
               | _ -> blob.Name.Replace(folderPath, String.Empty)
           downloadFile blob (Path.Combine(path, targetName)))
    |> Async.Parallel
    |> Async.Ignore
    |> Async.Start