module FSharp.Azure.StorageTypeProvider.QueueHelpers

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open System

let private queueClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudQueueClient()

let private resetQueue name = 
    let queue = queueClient.GetQueueReference name
    queue.DeleteIfExists() |> ignore
    queue.Create()

let resetData() = 
    [ "tptest"; "second"; "third" ]
    |> List.map resetQueue
    |> ignore
