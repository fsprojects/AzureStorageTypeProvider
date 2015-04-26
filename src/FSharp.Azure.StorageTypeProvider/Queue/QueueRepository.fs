module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open Microsoft.WindowsAzure.Storage.Queue.Protocol
open System

let internal getQueueClient connectionString = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient()

let getQueues connectionString = 
    getQueueClient(connectionString).ListQueues()
    |> Seq.map (fun q -> q.Name)
    |> Seq.toList

let getQueueRef name = getQueueClient >> (fun q -> q.GetQueueReference name)

let peekMessages connectionString name = getQueueRef name connectionString |> (fun x -> x.PeekMessages)

let generateSas start duration (queue:CloudQueue) =
    let policy = SharedAccessQueuePolicy(Permissions = (SharedAccessQueuePermissions.Add |||
                                                        SharedAccessQueuePermissions.ProcessMessages |||
                                                        SharedAccessQueuePermissions.Read |||
                                                        SharedAccessQueuePermissions.Update),
                                         SharedAccessStartTime = (start |> Option.map(fun start -> DateTimeOffset start) |> Option.toNullable),
                                         SharedAccessExpiryTime = Nullable(DateTimeOffset.UtcNow.Add duration))
    queue.GetSharedAccessSignature(policy, null)