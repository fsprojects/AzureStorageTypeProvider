module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue.Protocol

let private getQueueClient connection = CloudStorageAccount.Parse(connection).CreateCloudQueueClient()

let getQueues connectionString =
    getQueueClient(connectionString).ListQueues()
    |> Seq.map(fun q -> q.Name)
    |> Seq.toArray