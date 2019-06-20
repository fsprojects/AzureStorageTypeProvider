module FSharp.Azure.StorageTypeProvider.QueueHelpers

open Microsoft.Azure.Storage
open Microsoft.Azure.Storage.Queue

let private queueClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudQueueClient()

let private resetQueue name = 
    let queue = queueClient.GetQueueReference name
    queue.DeleteIfExistsAsync().Result |> ignore
    queue.CreateAsync() |> Async.AwaitTask |> Async.RunSynchronously

let private addMessage (queue:CloudQueue) (text:string) = queue.AddMessageAsync(CloudQueueMessage text) |> Async.AwaitTask |> Async.RunSynchronously

let resetData() = 
    [ "sample-queue"; "second-sample"; "third-sample" ]
    |> List.map resetQueue
    |> ignore

    let secondQueue = queueClient.GetQueueReference "second-sample"
    
    [ "Hello from Azure Type Provider"
      "F# is cool"
      "Azure is also pretty great" ]
    |> List.iter (addMessage secondQueue)
    

