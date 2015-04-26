module FSharp.Azure.StorageTypeProvider.QueueHelpers

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open System

let private queueClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudQueueClient()

let private resetQueue name = 
    let queue = queueClient.GetQueueReference name
    queue.DeleteIfExists() |> ignore
    queue.Create()

let private addMessage (queue:CloudQueue) (text:string) = queue.AddMessage(CloudQueueMessage text)

let resetData() = 
    [ "sample-queue"; "second-sample"; "third-sample" ]
    |> List.map resetQueue
    |> ignore

    let secondQueue = queueClient.GetQueueReference "second-sample"
    
    [ "Hello from Azure Type Provider"
      "F# is cool"
      "Azure is also pretty great" ]
    |> List.iter (addMessage secondQueue)
    

