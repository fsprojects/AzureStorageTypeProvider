module internal FSharp.Azure.StorageTypeProvider.Queue.QueueMemberFactory

open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

let private createPeekForQueue (connectionString, domainType:ProvidedTypeDefinition, ctx:ProvidedTypesContext, queueName) =
    let peekType = ctx.ProvidedTypeDefinition(sprintf "%s.Queues.%s.Peek" connectionString queueName, None, hideObjectMethods = true)
    domainType.AddMember peekType
    peekType.AddMembersDelayed(fun () ->
        let messages = peekMessages connectionString queueName 32
        messages
        |> Seq.map(fun msg ->
            let messageType = ctx.ProvidedTypeDefinition(sprintf "%s.Queues.%s.Peek.%s" connectionString queueName msg.Id, None, hideObjectMethods = true)
            domainType.AddMember messageType
            messageType.AddMembersDelayed(fun () ->
                let contents = msg.AsString
                let contentsProp =
                    let out = String(msg.AsString.ToCharArray() |> Seq.truncate 32 |> Seq.toArray)
                    if contents.Length <= 32 then out
                    else out + "..."

                let dequeueCount = msg.DequeueCount
                let inserted = msg.InsertionTime.ToString()
                let expires = msg.ExpirationTime.ToString()
                let id = msg.Id

                [ ctx.ProvidedProperty(sprintf "Id: %s" msg.Id, typeof<string>, getterCode = (fun _ -> <@@ id @@>))
                  ctx.ProvidedProperty(sprintf "Contents: '%s'" contentsProp, typeof<string>, getterCode = (fun _ -> <@@ contents @@>))
                  ctx.ProvidedProperty(sprintf "Dequeued %d times" msg.DequeueCount, typeof<int>, getterCode = (fun _ -> <@@ dequeueCount @@>))
                  ctx.ProvidedProperty(sprintf "Inserted on %A" msg.InsertionTime, typeof<string>, getterCode = (fun _ -> <@@ inserted @@>))
                  ctx.ProvidedProperty(sprintf "Expires at %A" msg.ExpirationTime, typeof<string>, getterCode = (fun _ -> <@@ expires @@>))
                ])
            ctx.ProvidedProperty(sprintf "%s (%s)" (String(msg.AsString.ToCharArray() |> Seq.truncate 32 |> Seq.toArray)) msg.Id, messageType, getterCode = (fun _ -> <@@ () @@>)))
        |> Seq.toList)
    peekType

let createQueueMemberType connectionString (domainType:ProvidedTypeDefinition) (ctx:ProvidedTypesContext) queueName =
    let queueType = ctx.ProvidedTypeDefinition(sprintf "%s.queue.%s" connectionString queueName, Some typeof<ProvidedQueue>, hideObjectMethods = true)
    domainType.AddMember queueType
    queueType.AddMemberDelayed(fun () ->
        let p = ctx.ProvidedProperty("Peek", createPeekForQueue(connectionString, domainType, ctx, queueName), getterCode = (fun _ -> <@@ () @@>))
        p.AddXmlDoc <| sprintf "Allows you to peek at the top 32 items on the queue (as of %A)." DateTime.UtcNow
        p)
    queueName, queueType

/// Builds up the Table Storage member
let getQueueStorageMembers (connectionString, domainType : ProvidedTypeDefinition, ctx:ProvidedTypesContext) =
    let queueListingType = ctx.ProvidedTypeDefinition("Queues", Some typeof<obj>, hideObjectMethods = true)
    let createQueueMember = createQueueMemberType connectionString domainType ctx
    queueListingType.AddMembersDelayed(fun () ->
        connectionString
        |> getQueues
        |> List.map (createQueueMember >> fun (name, queueType) ->
            ctx.ProvidedProperty(name, queueType, getterCode = fun _ -> <@@ ProvidedQueue(connectionString, name) @@> )))
    domainType.AddMember queueListingType
    queueListingType.AddMember(ctx.ProvidedProperty("CloudQueueClient", typeof<CloudQueueClient>, getterCode = (fun _ -> <@@ QueueBuilder.getQueueClient connectionString @@>)))
    let queueListingProp = ctx.ProvidedProperty("Queues", queueListingType, isStatic = true, getterCode = (fun _ -> <@@ () @@>))
    queueListingProp.AddXmlDoc "Gets the list of all queues in this storage account."
    queueListingProp