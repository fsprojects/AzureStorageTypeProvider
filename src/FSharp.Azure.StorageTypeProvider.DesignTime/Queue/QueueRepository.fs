module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open System

let internal getQueueClient connectionString = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient()

let getQueues connectionString = 
    getQueueClient(connectionString).ListQueuesSegmentedAsync(null).Result
    |> fun s -> s.Results
    |> Seq.map (fun q -> q.Name)
    |> Seq.toList

let getQueueRef name = getQueueClient >> (fun q -> q.GetQueueReference name)

let peekMessages connectionString name = getQueueRef name connectionString |> (fun x -> x.PeekMessagesAsync >> fun t -> t.Result)

let generateSas start duration queuePermissions (queue:CloudQueue) =
    let policy = SharedAccessQueuePolicy(Permissions = queuePermissions,
                                         SharedAccessStartTime = (start |> Option.map(fun start -> DateTimeOffset start) |> Option.toNullable),
                                         SharedAccessExpiryTime = Nullable(DateTimeOffset.UtcNow.Add duration))
    queue.GetSharedAccessSignature(policy, null)