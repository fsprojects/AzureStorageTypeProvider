namespace FSharp.Azure.StorageTypeProvider.Queue

open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

type MessageUpdate = 
    | Visibility
    | VisibilityAndMessage

type ProvidedQueueMessage = 
    { Id : string
      DequeueCount : int
      InsertionTime : DateTimeOffset option
      ExpirationTime : DateTimeOffset option
      NextVisibleTime : DateTimeOffset option
      GetBytes : unit -> byte array
      GetString : unit -> string
      PopReceipt : string }

module Async = 
    let awaitTaskUnit = Async.AwaitIAsyncResult >> Async.Ignore

module internal Factory = 
    let toOption (value : Nullable<_>) = 
        if value.HasValue then Some value.Value
        else None
    
    let toProvidedQueueMessage (message : CloudQueueMessage) = 
        { Id = message.Id
          DequeueCount = message.DequeueCount
          InsertionTime = message.InsertionTime |> toOption
          ExpirationTime = message.ExpirationTime |> toOption
          NextVisibleTime = message.NextVisibleTime |> toOption
          GetBytes = fun () -> message.AsBytes
          GetString = fun () -> message.AsString
          PopReceipt = message.PopReceipt }
    
    let toAzureQueueMessage message = 
        let msg = CloudQueueMessage(message.Id, message.PopReceipt)
        msg.SetMessageContent(message.GetBytes())
        msg

type ProvidedQueue(connectionDetails, name) = 
    let queueRef = getQueueRef (connectionDetails, name)
    
    let enqueue message = queueRef.AddMessageAsync(message) |> Async.awaitTaskUnit            

    /// Gets the queue length.
    member __.GetCurrentLength() = 
        queueRef.FetchAttributes()
        if queueRef.ApproximateMessageCount.HasValue then queueRef.ApproximateMessageCount.Value
        else 0
    
    /// Dequeues the next message.
    member __.Dequeue() = 
        async { 
            let! message = queueRef.GetMessageAsync() |> Async.AwaitTask
            return match message with
                   | null -> None
                   | _ -> Some(message |> Factory.toProvidedQueueMessage)
        }
    
    /// Enqueues a new message.
    member __.Enqueue(content : string) = enqueue(CloudQueueMessage(content))
    
    /// Enqueues a new message.
    member __.Enqueue(content : byte array) = enqueue(CloudQueueMessage(content))
    
    /// Deletes an existing message.
    member __.Delete(message) = queueRef.DeleteMessageAsync(message.Id, message.PopReceipt) |> Async.awaitTaskUnit
    
    /// Updates an existing message.
    member __.Update(message : ProvidedQueueMessage, newTimeout, updateType) = 
        let updateFields = 
            match updateType with
            | Visibility -> MessageUpdateFields.Visibility
            | VisibilityAndMessage -> MessageUpdateFields.Visibility ||| MessageUpdateFields.Content
        queueRef.UpdateMessageAsync(message |> Factory.toAzureQueueMessage, newTimeout, updateFields) |> Async.awaitTaskUnit
    
    /// Gets the name of the queue.
    member __.Name = queueRef.Name
