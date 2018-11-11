namespace FSharp.Azure.StorageTypeProvider.Queue

open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage.Queue
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
      /// The number of times this message has been dequeued.
      DequeueCount : int
      /// The time that this message was inserted.
      InsertionTime : DateTimeOffset option
      /// The time that this message will expire.
      ExpirationTime : DateTimeOffset option
      /// The time that this message will next become visible.
      NextVisibleTime : DateTimeOffset option
      /// Gets the contents of the message as a byte array.
      AsBytes : Lazy<byte array>
      /// Gets the contest of the message as a string.
      AsString : Lazy<string> }

/// Represents the set of possible permissions for a shared access queue policy.
[<FlagsAttribute>]
type QueuePermission =
    | Peek = 1
    | Enqueue = 2 
    | UpdateMessage = 4
    | DequeueAndDeleteMessageAndClear = 8

module internal Factory = 
    let unpackId messageId =
        let (ProvidedMessageId(MessageId messageId, PopReceipt popReceipt)) = messageId
        messageId, popReceipt

    let toProvidedQueueMessage (message : CloudQueueMessage) = 
        { Id = ProvidedMessageId(MessageId message.Id, PopReceipt message.PopReceipt)
          DequeueCount = message.DequeueCount
          InsertionTime = message.InsertionTime |> Option.ofNullable
          ExpirationTime = message.ExpirationTime |> Option.ofNullable
          NextVisibleTime = message.NextVisibleTime |> Option.ofNullable
          AsBytes = lazy message.AsBytes
          AsString = lazy message.AsString }
    
    let toAzureQueueMessage providedMessageId = 
        let messageId, popReceipt = providedMessageId |> unpackId
        CloudQueueMessage(messageId, popReceipt)

/// Represents an Azure Storage Queue.
type ProvidedQueue(defaultConnectionString, name) = 
    let getConnectionString connection = defaultArg connection defaultConnectionString
    let getQueue = getConnectionString >> getQueueRef name
    let enqueue message = getQueue >> (fun q -> q.AddMessageAsync message |> Async.AwaitTask)
    let updateMessage fields connectionString newTimeout message =
        let newTimeout = defaultArg newTimeout TimeSpan.Zero
        connectionString
        |> getQueue
        |> (fun queue -> queue.UpdateMessageAsync(message, newTimeout, fields) |> Async.AwaitTask)

    /// Gets a handle to the Azure SDK client for this queue.
    member __.AsCloudQueue(?connectionString) = getQueue connectionString

    /// Gets the queue length.
    member __.GetCurrentLength(?connectionString) = 
        let queueRef = getQueue connectionString
        queueRef.FetchAttributesAsync() |> Async.AwaitTask |> Async.RunSynchronously
        queueRef.ApproximateMessageCount
        |> Option.ofNullable
        |> defaultArg <| 0
    
    /// Dequeues the next message and optionally sets the visibility timeout (i.e. how long you can work with the message before it reappears in the queue)
    member __.Dequeue(?connectionString, ?visibilityTimeout) = 
        async { 
            let! message = (getQueue connectionString).GetMessageAsync(visibilityTimeout |> Option.toNullable, null, null) |> Async.AwaitTask
            return
                match message with
                | null -> None
                | _ -> Some(message |> Factory.toProvidedQueueMessage)
        }

    /// Dequeues the next message using the default connection string and sets the visibility timeout (i.e. how long you can work with the message before it reappears in the queue)
    member __.Dequeue(visibilityTimeout) = __.Dequeue(defaultConnectionString, visibilityTimeout)

    ///Generates a shared access signature, defaulting to start from now. Do not pass 'permissions' for full-access.
    member __.GenerateSharedAccessSignature(duration, ?start, ?connectionString, ?permissions) =
        let permissions = defaultArg permissions (QueuePermission.Peek ||| QueuePermission.Enqueue ||| QueuePermission.UpdateMessage ||| QueuePermission.DequeueAndDeleteMessageAndClear)
        let typeMap = 
            [ QueuePermission.Peek, SharedAccessQueuePermissions.Read;
              QueuePermission.Enqueue, SharedAccessQueuePermissions.Add;
              QueuePermission.UpdateMessage, SharedAccessQueuePermissions.Update;
              QueuePermission.DequeueAndDeleteMessageAndClear, SharedAccessQueuePermissions.ProcessMessages;
            ] |> Map.ofList

        let sharedAccessQueuePermissions = 
            typeMap
            |> Map.fold (fun s k v -> if permissions.HasFlag(k) then s ||| v else s) SharedAccessQueuePermissions.None 

        getQueue connectionString |> generateSas start duration sharedAccessQueuePermissions
    
    /// Enqueues a new message.
    member __.Enqueue(content, ?connectionString) =
        connectionString |> enqueue (CloudQueueMessage(content))
    
    /// Deletes an existing message.
    member __.DeleteMessage(providedMessageId, ?connectionString) = 
        let messageId, popReceipt = providedMessageId |> Factory.unpackId
        (connectionString |> getQueue).DeleteMessageAsync(messageId, popReceipt) |> Async.AwaitTask
    
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
        |> Async.AwaitTask

    /// Gets the name of the queue.
    member __.Name = (None |> getQueue).Name

/// [omit]
/// Allows creation of queue entities.
module QueueBuilder =
    /// Gets a queue client.
    let getQueueClient connectionString = QueueRepository.getQueueClient connectionString