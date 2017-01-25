module QueueTests

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open Swensen.Unquote
open Swensen.Unquote.Operators
open System
open System.Linq
open System.Net
open System.Text
open Expecto

type Local = AzureTypeProvider<"DevStorageAccount">

[<Tests>]
let compilationTests =
    testList "Queue Compilation Tests" [
        testCase "Correctly identifies queues" (fun _ ->
            Local.Queues.``sample-queue`` |> ignore
            Local.Queues.``second-sample`` |> ignore
            Local.Queues.``third-sample`` |> ignore)
    ]

let queue = Local.Queues.``sample-queue``

let queueSafeAsync = beforeAfterAsync QueueHelpers.resetData QueueHelpers.resetData

[<Tests>]
let readOnlyQueueTests =
    testList "Queue Tests Read Only" [
        testCase "Cloud Queue Client gives same results as the Type Provider" (fun _ ->
            let queues = Local.Queues
            let queueNames = queues.CloudQueueClient.ListQueues() |> Seq.map(fun q -> q.Name) |> Set.ofSeq
            Expect.containsAll queueNames [ queues.``sample-queue``.Name; queues.``second-sample``.Name;  queues.``third-sample``.Name ] "")
        testCase "Cloud Queue is the same queue as the Type Provider" (fun _ ->  (queue.AsCloudQueue().Name) |> shouldEqual queue.Name)
        testCaseAsync "Dequeue with nothing on the queue returns None" <| async {
            let! msg = queue.Dequeue()
            Expect.isNone msg "" }
    ]

[<Tests>]
let detailedQueueTests =
    testSequenced <| testList "Queue Tests" [
        testCaseAsync "Enqueues a message" <| queueSafeAsync (async {
            do! queue.Enqueue "Foo"
            queue.GetCurrentLength() |> shouldEqual 1 })
        testCaseAsync "Dequeues a message" <| queueSafeAsync (async {
            do! queue.Enqueue "Foo"
            let! message = queue.Dequeue()
            message.Value.AsString.Value |> shouldEqual "Foo" })
        testCaseAsync "Dequeues a message of bytes" <| queueSafeAsync (async {
            do! queue.Enqueue [| 0uy; 1uy; 2uy; |]
            let! message = queue.Dequeue()
            message.Value.AsBytes.Value |> shouldEqual [| 0uy; 1uy; 2uy; |] })
        testCaseAsync "Safely supports lazy evaluation of 'bad data'" <| queueSafeAsync (async {
            let uri =
                let sas = queue.GenerateSharedAccessSignature(TimeSpan.FromDays 7.)
                sprintf "http://127.0.0.1:10001/devstoreaccount1/%s/messages%s" queue.Name sas

            //Create a broken message to send to the queue. The queue system expects "Test" to be a Base64 encoded string.
            let request = Encoding.UTF8.GetBytes @"<QueueMessage><MessageText>Test</MessageText></QueueMessage>"
            
            // Send the request
            use wc = new WebClient()
            wc.UploadData(uri, request) |> ignore

            let! msg = queue.Dequeue()
            Expect.isSome msg "No message was returned"
            Expect.throws (fun _ -> msg.Value.AsString.Value |> ignore) "Value shouldn't have been string parseable" })       
        testCaseAsync "Deletes a message" <| queueSafeAsync (async {
            do! queue.Enqueue "Foo"
            let! message = queue.Dequeue()
            do! queue.DeleteMessage message.Value.Id
            0 |> shouldEqual <| queue.GetCurrentLength() })
        testCaseAsync "Update Message affects the text message body" <| queueSafeAsync (async {
            do! queue.Enqueue "Foo"
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, "Bar", TimeSpan.FromSeconds 0.)
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            message.Value.AsString.Value |> shouldEqual "Bar"  })
        testCaseAsync "Update Message affects the bytes message body" <| queueSafeAsync (async {
            do! queue.Enqueue [| 0uy; 1uy; 2uy |]
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, [| 2uy; 1uy; 0uy |], TimeSpan.FromSeconds 0.)
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            message.Value.AsBytes.Value |> shouldEqual [| 2uy; 1uy; 0uy |] })
        testCaseAsync "Dequeue Count is correctly emitted" <| queueSafeAsync (async {
            do! queue.Enqueue("Foo")
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds 0.)
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            do! queue.UpdateMessage(message.Value.Id, TimeSpan.FromSeconds 0.)
            do! Async.Sleep 100
            let! message = queue.Dequeue()
            3 |> shouldEqual message.Value.DequeueCount })
        testCaseAsync "Clear correctly empties the queue" <| queueSafeAsync (async {
            do! queue.Enqueue "Foo"
            do! queue.Enqueue "Bar"
            do! queue.Enqueue "Test"
            do! queue.Clear()
            queue.GetCurrentLength() |> shouldEqual 0 })
        testCaseAsync "Queue message with visibility timeout reappears correctly" <| queueSafeAsync (async {
            do! queue.Enqueue "test"
            do! queue.Dequeue(TimeSpan.FromSeconds 1.) |> Async.Ignore
            do! Async.Sleep 2000
            let! message = queue.Dequeue()
            Expect.isSome message "Should be a message"
            message.Value.DequeueCount |> shouldEqual 2 })
        testCaseAsync "Queue message with visibility timeout is correctly applied" <| queueSafeAsync (async {
            do! queue.Enqueue "test"
            let! message = queue.Dequeue(TimeSpan.FromSeconds 3.)
            do! Async.Sleep 2000
            let! message = queue.Dequeue()
            Expect.isNone message "Should be no message" })
    ]

