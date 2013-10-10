module Elastacloud.FSharp.AzureTypeProvider.AzureRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open System

type LightweightContainer = { Name : string; Files : seq<string> }

let getBlobStorageAccountManifest connection =
    CloudStorageAccount.Parse(connection)
                       .CreateCloudBlobClient()
                       .ListContainers()
                       |> Seq.map(fun c -> { Name = c.Name; Files = c.ListBlobs() |> Seq.map(fun b -> (b :?> CloudBlockBlob).Name) })
                       |> Seq.toList