namespace FSharp.Azure.StorageTypeProvider.Queue

open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

/// The unique identifier for this Azure queue message.
type MessageId = | MessageId of string
/// The unique identifier for this request of this Azure queue message.
type PopReceipt = | PopReceipt of string
/// The composite identifier of this Azure queue message.
type ProvidedMessageId = | ProvidedMessageId of MessageId : MessageId * PopReceipt : PopReceipt

/// Represents a single message that has been dequeued.
type ProvidedQueueMessage = 
    { /// The composite key of this message, containing both the message id and the pop receipt.
      Id : ProvidedMessageId
      DequeueCount : int
      InsertionTime : DateTimeOffset option
      ExpirationTime : DateTimeOffset option
      NextVisibleTime : DateTimeOffset option
      AsBytes : byte array
      AsString : string }

module internal Factory = 
    open FSharp.Azure.StorageTypeProvider.Utils
    
    let unpackId messageId =
        let (ProvidedMessageId(MessageId messageId, PopReceipt popReceipt)) = messageId
        messageId, popReceipt

    let toProvidedQueueMessage (message : CloudQueueMessage) = 
        { Id = ProvidedMessageId(MessageId message.Id, PopReceipt message.PopReceipt)
          DequeueCount = message.DequeueCount
          InsertionTime = message.InsertionTime |> toOption
          ExpirationTime = message.ExpirationTime |> toOption
          NextVisibleTime = message.NextVisibleTime |> toOption
          AsBytes = message.AsBytes
          AsString = message.AsString }
    
    let toAzureQueueMessage providedMessageId = 
        let messageId, popReceipt = providedMessageId |> unpackId
        CloudQueueMessage(messageId, popReceipt)

module internal Async = 
    let AwaitTaskUnit = Async.AwaitIAsyncResult >> Async.Ignore

type ProvidedQueue(defaultConnectionString, name) = 
    let getConnectionString connection = defaultArg connection defaultConnectionString
    let getQueue = getConnectionString >> getQueueRef name
    let enqueue message = getQueue >> (fun q -> q.AddMessageAsync(message) |> Async.AwaitTaskUnit)
    let updateMessage fields connectionString newTimeout message =
        let newTimeout = defaultArg newTimeout TimeSpan.Zero
        connectionString
        |> getQueue
        |> (fun queue -> queue.UpdateMessageAsync(message, newTimeout, fields) |> Async.AwaitTaskUnit)

    /// Gets a handle to the Azure SDK client for this queue.
    member __.AsCloudQueue(?connectionString) = getQueue connectionString

    /// Gets the queue length.
    member __.GetCurrentLength(?connectionString) = 
        let queueRef = getQueue connectionString
        queueRef.FetchAttributes()
        if queueRef.ApproximateMessageCount.HasValue then queueRef.ApproximateMessageCount.Value
        else 0
    
    /// Dequeues the next message.
    member __.Dequeue(?connectionString) = 
        async { 
            let! message = (getQueue connectionString).GetMessageAsync() |> Async.AwaitTask
            return match message with
                   | null -> None
                   | _ -> Some(message |> Factory.toProvidedQueueMessage)
        }
    
    /// Generates a full-access shared access signature, defaulting to start from now.
    member __.GenerateSharedAccessSignature(duration, ?start, ?connectionString) = 
        getQueue connectionString |> generateSas start duration
    
    /// Enqueues a new message.
    member __.Enqueue(content : string, ?connectionString) =
        connectionString |> enqueue (CloudQueueMessage(content))
    
    /// Enqueues a new message.
    member __.Enqueue(content : byte array, ?connectionString) = 
        connectionString |> enqueue (CloudQueueMessage(content))
    
    /// Deletes an existing message.
    member __.DeleteMessage(providedMessageId, ?connectionString) = 
        let messageId, popReceipt = providedMessageId |> Factory.unpackId
        (connectionString |> getQueue).DeleteMessageAsync(messageId, popReceipt) |> Async.AwaitTaskUnit
    
    /// Updates the visibility of an existing message.
    member __.UpdateMessage(messageId, newTimeout, ?connectionString) = 
        let message = messageId |> Factory.toAzureQueueMessage
        message |> updateMessage MessageUpdateFields.Visibility connectionString (Some newTimeout)

    /// Updates the visibility and the string contents of an existing message. If no timeout is provided, the update is immediately visible.
    member __.UpdateMessage(messageId, contents:string, ?newTimeout, ?connectionString) = 
        let message = messageId |> Factory.toAzureQueueMessage
        message.SetMessageContent contents
        message |> updateMessage (MessageUpdateFields.Visibility ||| MessageUpdateFields.Content) connectionString newTimeout

    /// Updates the visibility and the binary contents of an existing message. If no timeout is provided, the update is immediately visible.
    member __.UpdateMessage(messageId, contents:byte array, ?newTimeout, ?connectionString) = 
        let message = messageId |> Factory.toAzureQueueMessage
        message.SetMessageContent contents
        message |> updateMessage (MessageUpdateFields.Visibility ||| MessageUpdateFields.Content) connectionString newTimeout

    /// Clears the queue of all messages.
    member __.Clear(?connectionString) =
        connectionString
        |> getQueue
        |> (fun q -> q.ClearAsync())
        |> Async.AwaitTaskUnit

    /// Gets the name of the queue.
    member __.Name = (None |> getQueue).Name

module QueueBuilder =
    let getQueueClient connectionString = QueueRepository.getQueueClient connectionString