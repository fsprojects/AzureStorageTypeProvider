module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue.Protocol

let private getQueueClient = CloudStorageAccount.Parse >> (fun csa -> csa.CreateCloudQueueClient())

let getQueues connectionString = 
    getQueueClient(connectionString).ListQueues()
    |> Seq.map (fun q -> q.Name)
    |> Seq.toList

let getQueueRef name = getQueueClient >> (fun q -> q.GetQueueReference name)

let peekMessages connectionString name = getQueueRef name connectionString |> (fun x -> x.PeekMessages)
