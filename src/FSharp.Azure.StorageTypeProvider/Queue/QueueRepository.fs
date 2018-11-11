module internal FSharp.Azure.StorageTypeProvider.Queue.QueueRepository

open FSharp.Azure.StorageTypeProvider
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open System

let internal getQueueClient connectionString = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient()

[<AutoOpen>]
module private SdkExtensions =
    type CloudQueueClient with
        member cloudQueueClient.ListQueuesAsync() =
            let getTables token = async {
                let! result = cloudQueueClient.ListQueuesSegmentedAsync token |> Async.AwaitTask
                return result.ContinuationToken, result.Results }
            Async.segmentedAzureOperation getTables

let getQueues connectionString = async {
    let client = getQueueClient connectionString
    let! queues = client.ListQueuesAsync()
    return queues |> Array.map (fun q -> q.Name) }

let getQueueRef name = getQueueClient >> (fun q -> q.GetQueueReference name)

let peekMessages connectionString name = getQueueRef name connectionString |> (fun x -> x.PeekMessagesAsync >> fun t -> t.Result)

let generateSas start duration queuePermissions (queue:CloudQueue) =
    let policy = SharedAccessQueuePolicy(Permissions = queuePermissions,
                                         SharedAccessStartTime = (start |> Option.map(fun start -> DateTimeOffset start) |> Option.toNullable),
                                         SharedAccessExpiryTime = Nullable(DateTimeOffset.UtcNow.Add duration))
    queue.GetSharedAccessSignature(policy, null)