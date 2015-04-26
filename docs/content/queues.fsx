(*** hide ***)
#load @"..\tools\references.fsx"
open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Queue
open System.Xml.Linq
open System
type Azure = AzureTypeProvider<"UseDevelopmentStorage=true">
let printMessage (msg:ProvidedQueueMessage) = printfn "Message %A with body '%s' has been dequeued %d times." msg.Id msg.AsString msg.DequeueCount
(**
Working with Queues
==================

For more information on Queues in general, please see some of the many articles on
[MSDN](https://msdn.microsoft.com/en-us/library/microsoft.windowsazure.storage.queue.aspx) or the [Azure](http://azure.microsoft.com/en-us/documentation/services/storage/) [documentation](http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-queues/). Some of the core features of the Queue provider are: -

## Rapid navigation

You can easily move between queues and view key information on that queue. Simply dotting into
a queue will automatically request the latest details on the queue. This allows easy exploration
of your queue assets, directly from within the REPL.
*)

(*** define-output: blobStats ***)
let queue = Azure.Queues.``sample-queue``
printfn "Queue '%s' has %d items on it." queue.Name (queue.GetCurrentLength())
(*** include-output: blobStats ***)

(**
## Processing messages
It is easy to push and pop messages onto / off a queue - simply call the Enqueue() and Dequeue()
methods on the appropriate queue. Enqueue will return an option message, in case there is nothing
on the queue. Once you have finished processing the message, simply call Delete().
*)

(*** define-output: queue1 ***)
async {
    printfn "Queue length is %d." (queue.GetCurrentLength())

    // Put a message on the queue
    printfn "Enqueuing a message!"
    do! queue.Enqueue("Hello from Azure Type Provider")
    printfn "Queue length is %d." (queue.GetCurrentLength())

    // Get the message back off the queue
    let dequeuedMessage = (queue.Dequeue() |> Async.RunSynchronously).Value // don't try this at home :)
    printfn "%A" dequeuedMessage
    
    // Delete it off the queue to tell Azure we're done with it.
    printfn "Deleting the message."
    do! queue.DeleteMessage dequeuedMessage.Id
    printfn "Queue length is %d." (queue.GetCurrentLength())
} |> Async.RunSynchronously
(*** include-output: queue1 ***)

(**
## Modifying Queues
You can easily modify the contents of an existing message and push it back onto the queue, or clear
the queue entirely.
*)
(*** define-output: queue2 ***)
async {
    printfn "Enqueuing a message!"
    do! queue.Enqueue("Hello from Azure Type Provider")
    
    // Get the message, then put it back on the queue with a new payload immediately.
    printfn "Dequeuing it."
    let! message = queue.Dequeue()
    match message with
    | Some message ->
        printMessage message
        printfn "Updating it and dequeuing it again."
        do! queue.UpdateMessage(message.Id, "Goodbye from Azure Type Provider")
        
        // Now dequeue the message again and interrogate it
        let! message = queue.Dequeue()
        match message with
        | Some message ->
            printMessage message
            do! queue.DeleteMessage message.Id
        | None -> ()
    | None -> ()
} |> Async.RunSynchronously
(*** include-output: queue2 ***)

(**
## Peeking the queue
The Queue Provider allows you to preview messages on the queue directly in intellisense. Simply
dot into the "Peek" property on the queue, and the first 32 messages on the queue will appear.
Properties on them can be bound with their values.
*)

