module ``Queue Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open Swensen.Unquote
open Swensen.Unquote.Operators
open System
open System.Linq
open System.Net
open System.Text
open Xunit

type Local = AzureTypeProvider<"DevStorageAccount", "">

type ResetQueueDataAttribute() =
    inherit BeforeAfterTestAttribute()
    override __.Before _ = QueueHelpers.resetData()
    override __.After _ = QueueHelpers.resetData()

[<Fact>]
let ``Correctly identifies queues``() =
    // compiles!
    Local.Queues.``sample-queue`` |> ignore
    Local.Queues.``second-sample`` |> ignore
    Local.Queues.``third-sample`` |> ignore

let queue = Local.Queues.``sample-queue``

[<Fact>]
[<ResetQueueData>]
let ``Enqueues a message``() =
    queue.Enqueue "Foo" |> Async.RunSynchronously
    test <@ queue.GetCurrentLength() = 1 @>

[<Fact>]
[<ResetQueueData>]
let ``Dequeues a message``() =
    let message = async { do! queue.Enqueue "Foo"
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously
    test <@ message.Value.AsString.Value = "Foo" @>

[<Fact>]
[<ResetQueueData>]
let ``Dequeues a message of bytes``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy; |]
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously
    test <@ message.Value.AsBytes.Value = [| 0uy; 1uy; 2uy; |] @>

[<Fact>]
[<ResetQueueData>]
let ``Safely supports lazy evaluation of "bad data"``() =
    let uri =
        let sas = queue.GenerateSharedAccessSignature(TimeSpan.FromDays 7.)
        sprintf "http://127.0.0.1:10001/devstoreaccount1/%s/messages%s" queue.Name sas

    //Create a broken message to send to the queue. The queue system expects "Test" to be a Base64 encoded string.
    let request = Encoding.UTF8.GetBytes @"<QueueMessage><MessageText>Test</MessageText></QueueMessage>"
    
    // Send the request
    use wc = new WebClient()
    wc.UploadData(uri, request) |> ignore

    let msg = queue.Dequeue() |> Async.RunSynchronously
    test <@ msg.IsSome @>
    raises<DecoderFallbackException> <@ msg.Value.AsString.Value @>

[<Fact>]
[<ResetQueueData>]
let ``Deletes a message``() =
    async { do! queue.Enqueue "Foo"
            let! message = queue.Dequeue()
            do! queue.DeleteMessage message.Value.Id }
    |> Async.RunSynchronously
    test <@ queue.GetCurrentLength() = 0 @>

[<Fact>]
[<ResetQueueData>]
let ``Dequeue with nothing on the queue returns None``() =
    test <@ queue.Dequeue() |> Async.RunSynchronously = None @>

[<Fact>]
[<ResetQueueData>]
let ``Update Message affects the text message body``() =
    let message = async { do! queue.Enqueue "Foo"
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessage(message.Value.Id, "Bar", TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    test <@ message.Value.AsString.Value = "Bar" @>

[<Fact>]
[<ResetQueueData>]
let ``Update Message affects the bytes message body``() =
    let message = async { do! queue.Enqueue [| 0uy; 1uy; 2uy |]
                          let! message = queue.Dequeue()
                          do! queue.UpdateMessage(message.Value.Id, [| 2uy; 1uy; 0uy |], TimeSpan.FromSeconds(0.))
                          return! queue.Dequeue() }
                  |> Async.RunSynchronously

    test <@ message.Value.AsBytes.Value = [| 2uy; 1uy; 0uy |] @>

[<Fact>]
[<ResetQueueData>]
let ``Dequeue Count is correctly emitted``() =
    let message = async {
            do! queue.Enqueue("Foo")
            do! Async.Sleep 250
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds 0.)
            do! Async.Sleep 250
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds 0.)
            do! Async.Sleep 250
            return! queue.Dequeue() } |> Async.RunSynchronously
    test <@ message.Value.DequeueCount = 3 @>

[<Fact>]
[<ResetQueueData>]
let ``Clear correctly empties the queue``() =
    async {
        do! queue.Enqueue "Foo"
        do! queue.Enqueue "Bar"
        do! queue.Enqueue "Test"
        do! queue.Clear() } |> Async.Ignore |> Async.RunSynchronously
    test <@ queue.GetCurrentLength() = 0 @>

[<Fact>]
[<ResetQueueData>]
let ``Cloud Queue Client gives same results as the Type Provider``() =
    let queues = Local.Queues
    let queueNames = queues.CloudQueueClient.ListQueues() |> Seq.map(fun q -> q.Name) |> Set.ofSeq
    test <@ queueNames
            |> Set.isSubset (Set [ queues.``sample-queue``.Name
                                   queues.``second-sample``.Name
                                   queues.``third-sample``.Name ]) @>

[<Fact>]
[<ResetQueueData>]
let ``Cloud Queue is the same queue as the Type Provider``() =
    test <@ queue.AsCloudQueue().Name = queue.Name @>

[<Fact>]
[<ResetQueueData>]
let ``Queue message with visibility timeout reappears correctly``() =
    queue.Enqueue "test" |> Async.RunSynchronously
    do queue.Dequeue(TimeSpan.FromSeconds 1.) |> Async.RunSynchronously |> ignore
    Threading.Thread.Sleep 2000
    let message = queue.Dequeue() |> Async.RunSynchronously
    test <@ Option.isSome message @>
    test <@ message.Value.DequeueCount = 2 @>

[<Fact>]
[<ResetQueueData>]
let ``Queue message with visibility timeout is correctly applied``() =
    queue.Enqueue "test" |> Async.RunSynchronously
    let message = queue.Dequeue(TimeSpan.FromSeconds 3.) |> Async.RunSynchronously
    Threading.Thread.Sleep 2000
    let message = queue.Dequeue() |> Async.RunSynchronously
    test <@ Option.isNone message @>