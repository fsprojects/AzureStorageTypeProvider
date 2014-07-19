namespace FSharp.Azure.StorageTypeProvider.Queue

open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

type ProvidedQueue (connectionDetails, name) =
    let queueRef = getQueueRef(connectionDetails, name)

    /// Gets the queue length.
    member __.CurrentLength() = queueRef.FetchAttributes()
                                if queueRef.ApproximateMessageCount.HasValue
                                    then queueRef.ApproximateMessageCount.Value
                                else 0

    /// Gets the name of the queue.
    member __.Name with get () = queueRef.Name
