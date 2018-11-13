///Contains reusable helper functions for accessing blobs
module internal FSharp.Azure.StorageTypeProvider.Blob.BlobRepositoryAsync

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO
open FSharp.Control.Tasks.ContextInsensitive
open System.Threading.Tasks

type ContainerItem = 
    | Folder of path : string * name : string * contents : ContainerItem array Lazy
    | Blob of path : string * name : string * blobType : BlobType * length : int64 option

type LightweightContainer = 
    { Name : string
      Contents : ContainerItem seq Lazy }

let getBlobClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let getContainerRef(connection, container) = (getBlobClient connection).GetContainerReference(container)
let getBlockBlobRef (connection, container, file) = getContainerRef(connection, container).GetBlockBlobReference(file)
let getPageBlobRef (connection, container, file) = getContainerRef(connection, container).GetPageBlobReference(file)

let private getItemName (item : string) (parent : CloudBlobDirectory) = 
    item, 
    match parent with
    | null -> item
    | parent -> item.Substring(parent.Prefix.Length)

let rec private getContainerStructure wildcard (container : CloudBlobContainer) = task {
    let rec getResults token = task {
        let! blobsSeqmented = container.ListBlobsSegmentedAsync(prefix = wildcard, currentToken = token)
        let token =  blobsSeqmented.ContinuationToken
        let result = blobsSeqmented.Results |> Seq.toList
        if isNull token then
            return result
        else
            let! others = getResults token
            return result @ others }
    let! results = getResults null
    let containerStructur =
        results
        |> Seq.distinctBy (fun b -> b.Uri.AbsoluteUri)
        |> Seq.choose (function
           | :? CloudBlobDirectory as directory -> 
               let path, name = getItemName directory.Prefix directory.Parent
               Some(Folder(path, name, lazy(container |> getContainerStructure directory.Prefix)))
           | :? ICloudBlob as blob ->
               let path, name = getItemName blob.Name blob.Parent
               Some(Blob(path, name, blob.BlobType, Some blob.Properties.Length))
           | _ -> None)
        |> Seq.toArray
    return containerStructur
}
let listBlobs incSubDirs (container:CloudBlobContainer) prefix = 
    container.ListBlobsSegmentedAsync(prefix = prefix, useFlatBlobListing = incSubDirs, blobListingDetails = BlobListingDetails.All, maxResults = Nullable(), currentToken = null, options = BlobRequestOptions(), operationContext = null).Result
    |> fun s -> s.Results
    |> Seq.choose(function
        | :? ICloudBlob as blob -> 
            let path, name = getItemName blob.Name blob.Parent
            Some(Blob(path, name, blob.Properties.BlobType, Some blob.Properties.Length))
        | _ -> None)    //can safely ignore folder types as we have a flat structure if & only if we want to include items from sub directories

// let getBlobStorageAccountManifest connection = task {
//     let rec getResults token = task {
//         let! containerResultSeqment  = (getBlobClient connection).ListContainersSegmentedAsync(currentToken = token)
//         let token =  containerResultSeqment.ContinuationToken
//         let result = containerResultSeqment.Results |> Seq.toList
//         if isNull token then
//             return result
//         else
//             let! others = getResults token
//             return result @ others }
//     let! results = getResults null
//     let blobStorageAccountManifest = task {
//         let blobContainerList = results |> Seq.toList
//         let manifestList =
//             blobContainerList 
//             |> List.map (fun blobContainer ->
//                 let! content = getContainerStructure null blobContainer
//                 {   Name = blobContainer.Name
//                     Contents =
//                         lazy
//                             content |> Seq.cache})
//         return manifestList
//     }
//     let! results = blobStorageAccountManifest
// }

let getBlobStorageAccountManifest connection = 
    (getBlobClient connection).ListContainersSegmentedAsync(null).Result
    |> fun s -> s.Results
    |> Seq.toList
    |> List.map (fun container -> 
        { Name = container.Name
          Contents =
            lazy
                let! stuctur = 
                    container
                    |> getContainerStructure null
                stuctur
                |> Seq.cache })
let downloadFolder (connectionDetails, path) =
    let downloadFile (blobRef:ICloudBlob) destination =
        let targetDirectory = Path.GetDirectoryName(destination)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(destination, FileMode.Create) |> Async.AwaitTask

    let connection, container, folderPath = connectionDetails
    let containerRef = (getBlobClient connection).GetContainerReference(container)
    containerRef.ListBlobsSegmentedAsync(prefix = folderPath, useFlatBlobListing = true, blobListingDetails = BlobListingDetails.All, maxResults = Nullable(), currentToken = null, options = BlobRequestOptions(), operationContext = null).Result
    |> fun s -> s.Results
    |> Seq.choose (function
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