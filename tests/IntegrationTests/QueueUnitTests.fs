module QueueTests

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open Swensen.Unquote
open Swensen.Unquote.Operators
open Microsoft.WindowsAzure.Storage.Queue
open System
open System.Linq
open System.Net
open System.Text
open Expecto
open FSharp.Control.Tasks.ContextSensitive

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

let getQueues (queueClient:CloudQueueClient) = task {
    let rec getResults token = task {
        let! blobsSeqmented = queueClient.ListQueuesSegmentedAsync(token)
        let token =  blobsSeqmented.ContinuationToken
        let result = blobsSeqmented.Results |> Seq.toList
        if isNull token then
            return result
        else
            let! others = getResults token
            return result @ others }
    let! results = getResults null
    return results |> Seq.map(fun c -> c.Name) |> Set.ofSeq
}


[<Tests>]
let readOnlyQueueTests =
    testList "Queue Tests Read Only" [
        testTask "Cloud Queue Client gives same results as the Type Provider" {
            let queues = Local.Queues
            let! q = getQueues queues.CloudQueueClient
            Expect.containsAll q [ queues.``sample-queue``.Name; queues.``second-sample``.Name;  queues.``third-sample``.Name ] ""
        }        
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
        // testCaseAsync "Dequeues a message of bytes" <| queueSafeAsync (async {
        //     do! queue.Enqueue [| 0uy; 1uy; 2uy; |]
        //     let! message = queue.Dequeue()
        //     message.Value.AsBytes.Value |> shouldEqual [| 0uy; 1uy; 2uy; |] }) ///Has to be fixed
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
        // testCaseAsync "Update Message affects the bytes message body" <| queueSafeAsync (async {
        //     do! queue.Enqueue [| 0uy; 1uy; 2uy |]
        //     do! Async.Sleep 100
        //     let! message = queue.Dequeue()
        //     do! queue.UpdateMessage(message.Value.Id, [| 2uy; 1uy; 0uy |], TimeSpan.FromSeconds 0.)
        //     do! Async.Sleep 100
        //     let! message = queue.Dequeue()
        //     message.Value.AsBytes.Value |> shouldEqual [| 2uy; 1uy; 0uy |] }) ///Has to be fixed
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

[<Tests>]
let sasTokenTests =
    testList "SAS Token Tests" [
        testCase "Generates token with default (full-access) queue permissions" (fun _ ->
            let sas = queue.GenerateSharedAccessSignature(TimeSpan.FromDays 7.)
            Expect.stringContains sas "sp=raup" "Invalid permissions"
        )
        testCase "Generates token with specific queue permissions" (fun _ ->
            let sas = queue.GenerateSharedAccessSignature(TimeSpan.FromDays 7., permissions = (QueuePermission.Enqueue ||| QueuePermission.UpdateMessage))
            Expect.stringContains sas "sp=au" "Invalid permissions"
        )
    ]