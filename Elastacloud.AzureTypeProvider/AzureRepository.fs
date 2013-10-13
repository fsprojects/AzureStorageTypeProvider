module Elastacloud.FSharp.AzureTypeProvider.AzureRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System

type LightweightContainer = 
    { Name : string
      GetFiles : unit -> seq<string> }

let private getCloudClient connection = CloudStorageAccount.Parse(connection).CreateCloudBlobClient()
let private getBlobRef connection container file = getCloudClient(connection).GetContainerReference(container).GetBlockBlobReference(file)

/// Generates a set a lightweight container lists for a blob storage account
let getBlobStorageAccountManifest connection = 
    getCloudClient(connection).ListContainers()
    |> Seq.toList
    |> List.map (fun c -> 
           { Name = c.Name
             GetFiles = 
                 (fun _ -> 
                 c.ListBlobs(useFlatBlobListing = true)
                 |> Seq.map (fun b -> (b :?> CloudBlockBlob).Name)
                 |> Seq.cache) })

let downloadText connection container fileName = (getBlobRef connection container fileName).DownloadText()
let downloadData connection container fileName destinationArray = (getBlobRef connection container fileName).DownloadToByteArray(destinationArray, 0)