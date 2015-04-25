module FSharp.Azure.StorageTypeProvider.``Queue Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open Swensen.Unquote
open Swensen.Unquote.Operators
open System
open System.Linq
open Xunit

type Local = AzureTypeProvider<"DevStorageAccount", "">

type ResetQueueDataAttribute() =
    inherit BeforeAfterTestAttribute()
    override x.Before(methodUnderTest) = QueueHelpers.resetData()
    override x.After(methodUnderTest) = QueueHelpers.resetData()

[<Fact>]
let ``Correctly identifies queues``() =
    // compiles!
    Local.Queues.``sample-queue`` |> ignore
    Local.Queues.``second-sample`` |> ignore
    Local.Queues.``third-sample``

let queue = Local.Queues.``sample-queue``

[<Fact>]
[<ResetQueueData>]
let ``Enqueues a message``() =
    queue.Enqueue("Foo") |> Async.RunSynchronously
    queue.GetCurrentLength() =? 1

[<Fact>]
[<ResetQueueData>]
let ``Dequeues a message``() =
    let message = async { do! queue.Enqueue "Foo"
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously
    message.Value.AsString =? "Foo"

[<Fact>]
[<ResetQueueData>]
let ``Dequeues a message of bytes``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy; |]
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously
    message.Value.AsBytes =? [| 0uy; 1uy; 2uy; |]

[<Fact>]
[<ResetQueueData>]
let ``Deletes a message``() =
    async { do! queue.Enqueue "Foo"
            let! message = queue.Dequeue()
            do! queue.DeleteMessage(message.Value.Id) }
    |> Async.RunSynchronously
    queue.GetCurrentLength() =? 0

[<Fact>]
[<ResetQueueData>]
let ``Dequeue with nothing on the queue returns None``() =
    let message = queue.Dequeue() |> Async.RunSynchronously
    message.IsNone =? true

[<Fact>]
[<ResetQueueData>]
let ``Update Message affects the text message body``() =
    let message = async { do! queue.Enqueue "Foo"
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessage(message.Value.Id, "Bar", TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsString =? "Bar"

[<Fact>]
[<ResetQueueData>]
let ``Update Message affects the bytes message body``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy |]
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessage(message.Value.Id, [| 2uy; 1uy; 0uy |], TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsBytes =? [| 2uy; 1uy; 0uy |]

[<Fact>]
[<ResetQueueData>]
let ``Dequeue Count is correctly emitted``() =
    let message = async {
            do! queue.Enqueue("Foo")
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds(0.))
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds(0.))
            return! queue.Dequeue() } |> Async.RunSynchronously
    message.Value.DequeueCount =? 3

[<Fact>]
[<ResetQueueData>]
let ``Clear correctly empties the queue``() =
    async {
        do! queue.Enqueue "Foo"
        do! queue.Enqueue "Bar"
        do! queue.Enqueue "Test"
        do! queue.Clear() } |> Async.Ignore |> Async.RunSynchronously
    queue.GetCurrentLength() =? 0

[<Fact>]
[<ResetQueueData>]
let ``Cloud Queue Client gives same results as the Type Provider``() =
    let queues = Local.Queues
    let queueNames = queues.CloudQueueClient.ListQueues() |> Seq.map(fun q -> q.Name) |> Set.ofSeq
    queueNames
    |> Set.isSubset (Set [ queues.``sample-queue``.Name
                           queues.``second-sample``.Name
                           queues.``third-sample``.Name ])
    =? true

[<Fact>]
[<ResetQueueData>]
let ``Cloud Queue is the same queue as the Type Provider``() =
    queue.AsCloudQueue().Name =? queue.Name
