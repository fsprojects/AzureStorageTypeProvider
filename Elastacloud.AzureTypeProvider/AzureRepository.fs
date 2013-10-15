module Elastacloud.FSharp.AzureTypeProvider.AzureRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System

type internal LightweightContainer = 
    { Name : string
      GetFiles : unit -> seq<string> }

let private getCloudClient connection = 
    CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let private getBlobRef connection container file = 
    (getCloudClient connection).GetContainerReference(container).GetBlockBlobReference(file)

/// Generates a set a lightweight container lists for a blob storage account
let internal getBlobStorageAccountManifest connection = 
    (getCloudClient connection).ListContainers()
    |> Seq.toList
    |> List.map (fun c -> 
           { Name = c.Name
             GetFiles = 
                 (fun _ -> 
                 c.ListBlobs(useFlatBlobListing = true)
                 |> Seq.map (fun b -> (b :?> CloudBlockBlob).Name)
                 |> Seq.cache) })

let downloadText connection container fileName = 
    let blobRef = getBlobRef connection container fileName
    Async.AwaitTask(blobRef.DownloadTextAsync())

let private awaitUnit = Async.AwaitIAsyncResult >> Async.Ignore

let downloadData connection container fileName = 
    async { 
        let blobRef = getBlobRef connection container fileName
        do! awaitUnit (blobRef.FetchAttributesAsync())
        let destinationArray = Array.zeroCreate (int blobRef.Properties.Length)
        do! awaitUnit (blobRef.DownloadToByteArrayAsync(destinationArray, 0))
        return destinationArray
    }

let downloadToFile connection container fileName path = 
    let blobRef = getBlobRef connection container fileName
    awaitUnit (blobRef.DownloadToFileAsync(path, IO.FileMode.Create))

let getCopyState (blobRef : CloudBlockBlob) = 
    if (blobRef.CopyState <> null) then Some blobRef.CopyState
    else None

let getDetails connection container fileName = 
    let blobRef = getBlobRef connection container fileName
    blobRef.FetchAttributes()
    let copyState = getCopyState blobRef
    
    let copyDescription = 
        match copyState with
        | Some(copyState) -> 
            sprintf "%s%s" (copyState.Status.ToString()) (match copyState.Status with
                                                          | CopyStatus.Success -> 
                                                              sprintf " (completed on %s)." 
                                                                  (blobRef.CopyState.CompletionTime.Value.UtcDateTime.ToString
                                                                       ())
                                                          | CopyStatus.Pending -> 
                                                              sprintf " (%f complete)." 
                                                                  (((float 
                                                                         blobRef.CopyState.TotalBytes.Value) 
                                                                    / 100.0) 
                                                                   * float 
                                                                         blobRef.CopyState.BytesCopied.Value)
                                                          | _ -> String.Empty)
        | _ -> "Uknown"
    blobRef.Uri.AbsoluteUri, copyDescription
