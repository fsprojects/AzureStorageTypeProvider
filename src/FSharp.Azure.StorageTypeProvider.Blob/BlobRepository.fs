///Contains reusable helper functions for accessing blobs
module FSharp.Azure.StorageTypeProvider.Blob.BlobRepository

open FSharp.Azure.StorageTypeProvider
open Microsoft.Azure.Storage
open Microsoft.Azure.Storage.Blob
open System
open System.IO

type ContainerItem = 
    | Folder of path : string * name : string * contents : ContainerItem array Async
    | Blob of path : string * name : string * blobType : BlobType * length : int64 option

type LightweightContainer = 
    { Name : string
      Contents : ContainerItem array Async }

let getBlobClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let getContainerRef(connection, container) = (getBlobClient connection).GetContainerReference(container)
let getBlockBlobRef (connection, container, file) = getContainerRef(connection, container).GetBlockBlobReference(file)
let getPageBlobRef (connection, container, file) = getContainerRef(connection, container).GetPageBlobReference(file)

let private getItemName (item : string) (parent : CloudBlobDirectory) = 
    item, 
    match parent with
    | null -> item
    | parent -> item.Substring(parent.Prefix.Length)

[<AutoOpen>]
module private SdkExtensions =
    type CloudBlobClient with
        member blobClient.ListContainersAsync() =
            let listContainers token = async {
                let! results = blobClient.ListContainersSegmentedAsync token |> Async.AwaitTask
                return results.ContinuationToken, results.Results }
            Async.segmentedAzureOperation listContainers

    type CloudBlobContainer with
        member container.ListBlobsAsync incSubDirs prefix =
            let listBlobs token = async {
                let! results = container.ListBlobsSegmentedAsync(prefix = prefix, useFlatBlobListing = incSubDirs, blobListingDetails = BlobListingDetails.None, maxResults = Nullable(), currentToken = token, options = BlobRequestOptions(), operationContext = null) |> Async.AwaitTask
                return results.ContinuationToken, results.Results }
            Async.segmentedAzureOperation listBlobs

let listBlobs incSubDirs (container:CloudBlobContainer) prefix = async {
    let! results = container.ListBlobsAsync incSubDirs prefix

    //can safely ignore folder types as we have a flat structure if & only if we want to include items from sub directories
    return
        [| for result in results do
               match result with
               | :? ICloudBlob as blob -> 
                   let path, name = getItemName blob.Name blob.Parent
                   yield Blob(path, name, blob.Properties.BlobType, Some blob.Properties.Length)
               | _ -> () |] }

let getBlobStorageAccountManifest (connectionString:string) =
    let rec getContainerStructureAsync prefix (container:CloudBlobContainer) = async {
        let! blobs = container.ListBlobsAsync false prefix
        let blobs = blobs |> Array.distinctBy (fun b -> b.Uri.AbsoluteUri)
        return
            [| for blob in blobs do
                   match blob with
                   | :? CloudBlobDirectory as directory -> 
                       let path, name = getItemName directory.Prefix directory.Parent
                       yield Folder(path, name, container |> getContainerStructureAsync directory.Prefix)
                   | :? ICloudBlob as blob ->
                       let path, name = getItemName blob.Name blob.Parent
                       yield Blob(path, name, blob.Properties.BlobType, Some blob.Properties.Length)
                   | _ -> () |] }
    
    async {
        let client = (CloudStorageAccount.Parse connectionString).CreateCloudBlobClient()
        let! containers = client.ListContainersAsync() 
        return!
            containers
            |> Array.map (fun container -> async {
                let structure = container |> getContainerStructureAsync null
                return { Name = container.Name; Contents = structure } })
            |> Async.Parallel }

let downloadFolder (connectionDetails, path) = async {
    let downloadFile (blobRef:ICloudBlob) destination =
        let targetDirectory = Path.GetDirectoryName(destination)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(destination, FileMode.Create) |> Async.AwaitTask

    let connection, container, folderPath = connectionDetails
    let containerRef = (getBlobClient connection).GetContainerReference(container)
    let! blobs = containerRef.ListBlobsAsync true folderPath
    
    return!
        blobs
        |> Array.choose (function
            | :? ICloudBlob as b -> Some b
            | _ -> None)
        |> Array.map (fun blob -> 
            let targetName = 
                match folderPath with
                | folderPath when String.IsNullOrEmpty folderPath -> blob.Name
                | _ -> blob.Name.Replace(folderPath, String.Empty)
            downloadFile blob (Path.Combine(path, targetName)))
        |> Async.Parallel
        |> Async.Ignore }