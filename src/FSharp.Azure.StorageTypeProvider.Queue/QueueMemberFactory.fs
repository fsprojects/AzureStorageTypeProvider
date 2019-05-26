module internal FSharp.Azure.StorageTypeProvider.Queue.QueueMemberFactory

open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.Azure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

let private createPeekForQueue (connectionString, domainType:ProvidedTypeDefinition, queueName) =
    let peekType = ProvidedTypeDefinition(sprintf "%s.Queues.%s.Peek" connectionString queueName, None, hideObjectMethods = true)
    domainType.AddMember peekType
    peekType.AddMembersDelayed(fun () ->
        let messages = peekMessages connectionString queueName 32
        messages
        |> Seq.map(fun msg ->
            let messageType = ProvidedTypeDefinition(sprintf "%s.Queues.%s.Peek.%s" connectionString queueName (string msg.Id), None, hideObjectMethods = true)
            domainType.AddMember messageType
            messageType.AddMembersDelayed(fun () ->
                let contents = msg.AsString
                let contentsProp =
                    let out = String(contents.ToCharArray() |> Seq.truncate 32 |> Seq.toArray)
                    if contents.Length <= 32 then out
                    else out + "..."

                let dequeueCount = msg.DequeueCount
                let inserted = msg.InsertionTime.ToString()
                let expires = msg.ExpirationTime.ToString()
                let id = msg.Id

                [ ProvidedProperty(sprintf "Id: %s" (string msg.Id), typeof<string>, getterCode = (fun _ -> <@@ id @@>))
                  ProvidedProperty(sprintf "Contents: '%s'" contentsProp, typeof<string>, getterCode = (fun _ -> <@@ contents @@>))
                  ProvidedProperty(sprintf "Dequeued %d times" msg.DequeueCount, typeof<int>, getterCode = (fun _ -> <@@ dequeueCount @@>))
                  ProvidedProperty(sprintf "Inserted on %A" msg.InsertionTime, typeof<string>, getterCode = (fun _ -> <@@ inserted @@>))
                  ProvidedProperty(sprintf "Expires at %A" msg.ExpirationTime, typeof<string>, getterCode = (fun _ -> <@@ expires @@>))
                ])
            ProvidedProperty(sprintf "%s (%s)" (String(msg.AsString.ToCharArray() |> Seq.truncate 32 |> Seq.toArray)) (string msg.Id), messageType, getterCode = (fun _ -> <@@ () @@>)))
        |> Seq.toList)
    peekType

let createQueueMemberType connectionString (domainType:ProvidedTypeDefinition) queueName =
    let queueType = ProvidedTypeDefinition(sprintf "%s.queue.%s" connectionString queueName, Some typeof<ProvidedQueue>, hideObjectMethods = true)
    domainType.AddMember queueType
    queueType.AddMemberDelayed(fun () ->
        let p = ProvidedProperty("Peek", createPeekForQueue(connectionString, domainType, queueName), getterCode = (fun _ -> <@@ () @@>))
        p.AddXmlDoc <| sprintf "Allows you to peek at the top 32 items on the queue (as of %A)." DateTime.UtcNow
        p)
    queueName, queueType

open Microsoft.Azure.Storage.RetryPolicies

let private doesQueueEndpointExists connectionString = async {
    let client = getQueueClient connectionString
    client.DefaultRequestOptions.RetryPolicy <- NoRetry()
    client.DefaultRequestOptions.MaximumExecutionTime <- Nullable(TimeSpan(0, 0, 5))
    match! client.GetQueueReference("test").ExistsAsync() |> Async.AwaitTask |> Async.toAsyncResult with
    | Ok _ -> return true
    | Error _ -> return false }

/// Builds up the Queue Storage member
let getQueueStorageMembers (connectionString, domainType : ProvidedTypeDefinition) =
    if not (doesQueueEndpointExists connectionString |> Async.RunSynchronously) then None
    else
        let queueListingType = ProvidedTypeDefinition("Queues", Some typeof<obj>, hideObjectMethods = true)
        let createQueueMember = createQueueMemberType connectionString domainType
        queueListingType.AddMembersDelayed(fun () ->
            connectionString
            |> getQueues
            |> Async.RunSynchronously
            |> Array.map (createQueueMember >> fun (name, queueType) ->
                ProvidedProperty(name, queueType, getterCode = fun _ -> <@@ ProvidedQueue(connectionString, name) @@> ))
            |> Array.toList)

        domainType.AddMember queueListingType
        queueListingType.AddMember(ProvidedProperty("CloudQueueClient", typeof<CloudQueueClient>, getterCode = (fun _ -> <@@ QueueBuilder.getQueueClient connectionString @@>)))
        let queueListingProp = ProvidedProperty("Queues", queueListingType, isStatic = true, getterCode = (fun _ -> <@@ () @@>))
        queueListingProp.AddXmlDoc "Gets the list of all queues in this storage account."
        Some queueListingProp