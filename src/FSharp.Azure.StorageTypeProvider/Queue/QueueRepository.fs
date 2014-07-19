module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue.Protocol

let private getQueueClient connection = CloudStorageAccount.Parse(connection).CreateCloudQueueClient()

let getQueues connectionString =
    getQueueClient(connectionString).ListQueues()
    |> Seq.map(fun q -> q.Name)
    |> Seq.toList

let getQueueRef(connection,name) =
    getQueueClient(connection).GetQueueReference(name)

let peekMessages(connection,name,count) =
    let queue = getQueueRef(connection,name)
    queue.PeekMessages(count)
    |> Seq.map(fun m -> m.AsString)
    |> Seq.toList
