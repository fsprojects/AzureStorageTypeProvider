module FSharp.Azure.StorageTypeProvider.``Queue Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open Xunit
open System
open System.Linq
open Swensen.Unquote
open Swensen.Unquote.Operators

type Local = AzureTypeProvider<"DevStorageAccount", "">

type ResetQueueDataAttribute() =
    inherit BeforeAfterTestAttribute()
    override x.Before(methodUnderTest) = QueueHelpers.resetData()
    override x.After(methodUnderTest) = QueueHelpers.resetData()


[<Fact>]
let ``Correctly identifies queues``() =
    // compiles!
    Local.Queues.tptest |> ignore
    Local.Queues.second |> ignore
    Local.Queues.third

let queue = Local.Queues.tptest

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
            do! queue.Delete(message.Value.Id) }
    |> Async.RunSynchronously
    queue.GetCurrentLength() =? 0

[<Fact>]
[<ResetQueueData>]
let ``Dequeue with nothing on the queue returns None``() =
    let message = queue.Dequeue() |> Async.RunSynchronously
    message.IsNone =? true

[<Fact>]
[<ResetQueueData>]
let ``UpdateMessageContent affects the message body``() =
    let message = async { do! queue.Enqueue("Foo")
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessageContent(message.Value.Id, TimeSpan.FromSeconds(0.), "Bar")
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsString =? "Bar"

[<Fact>]
[<ResetQueueData>]
let ``UpdateVisibility does not affect the text message body``() =
    let message = async { do! queue.Enqueue("Foo")
                          let! message = queue.Dequeue()
                          let message = message.Value
                          let message = { message with AsString = "Bar" }
                          do! queue.UpdateVisibility(message.Id, TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsString =? "Foo"

[<Fact>]
[<ResetQueueData>]
let ``UpdateMessageContent affects the bytes message body``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy |]
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessageContent(message.Value.Id, TimeSpan.FromSeconds(0.), [| 2uy; 1uy; 0uy |])
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsBytes =? [| 2uy; 1uy; 0uy |]

[<Fact>]
[<ResetQueueData>]
let ``UpdateVisibility does not affect the byte message body``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy |]
                          let! message = queue.Dequeue()
                          let message = message.Value
                          let message = { message with AsBytes = [| 2uy; 1uy; 0uy |] }
                          do! queue.UpdateVisibility(message.Id, TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    message.Value.AsBytes =? [| 0uy; 1uy; 2uy |]

[<Fact>]
[<ResetQueueData>]
let ``Dequeue Count is correctly emitted``() =
    let message = async {
            do! queue.Enqueue("Foo")
            let! message = queue.Dequeue()
            do! queue.UpdateVisibility(message.Value.Id, TimeSpan.FromSeconds(0.))
            let! message = queue.Dequeue()
            do! queue.UpdateVisibility(message.Value.Id, TimeSpan.FromSeconds(0.))
            return! queue.Dequeue() } |> Async.RunSynchronously
    message.Value.DequeueCount =? 3

