module internal FSharp.Azure.StorageTypeProvider.Queue.QueueMemberFactory

open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes
open System

let createIndividualsForQueue (connectionString, domainType:ProvidedTypeDefinition, queueName) =
    let individualsType = ProvidedTypeDefinition(sprintf "%s.Queues.%s.Individuals" connectionString queueName, None, HideObjectMethods = true)
    domainType.AddMember individualsType
    individualsType.AddMembersDelayed(fun () ->
        let messages = peekMessages(connectionString, queueName, 32)
        messages
        |> Seq.map(fun msg ->
            let getData() = msg
            let messageType = ProvidedTypeDefinition(sprintf "%s.Queues.%s.Individuals.%s" connectionString queueName msg.Id, None, HideObjectMethods = true)
            domainType.AddMember messageType
            messageType.AddMembersDelayed(fun () ->
                let contents = msg.AsString
                let dequeueCount = msg.DequeueCount
                let expires = msg.ExpirationTime
                let id = msg.Id

                [ ProvidedProperty(sprintf "Contents: '%s'" msg.AsString, typeof<string>, GetterCode = (fun _ -> <@@ contents @@>))
                  ProvidedProperty(sprintf "Dequeued %d times" msg.DequeueCount, typeof<int>, GetterCode = (fun args -> <@@ dequeueCount @@>))
                  ProvidedProperty(sprintf "Expires at %A" msg.ExpirationTime, typeof<Nullable<DateTimeOffset>>, GetterCode = (fun _ -> <@@ expires @@>))
                  ProvidedProperty(sprintf "Id: %s" msg.Id, typeof<string>, GetterCode = (fun _ -> <@@ id @@>))
                ])
            ProvidedProperty((String(msg.AsString.ToCharArray() |> Seq.truncate 32 |> Seq.toArray)), messageType, GetterCode = (fun _ -> <@@ () @@>)))
        |> Seq.toList)
    individualsType

let createQueueMemberType connectionString (domainType:ProvidedTypeDefinition) queueName =
    let queueType = ProvidedTypeDefinition(sprintf "%s.queue.%s" connectionString queueName, Some typeof<ProvidedQueue>, HideObjectMethods = true)
    domainType.AddMember queueType
    queueType.AddMemberDelayed(fun () -> ProvidedProperty("Individuals", createIndividualsForQueue(connectionString, domainType, queueName), GetterCode = (fun _ -> <@@ () @@>)))
    queueName, queueType

/// Builds up the Table Storage member
let getQueueStorageMembers (connectionString, domainType : ProvidedTypeDefinition) =
    let queueListingType = ProvidedTypeDefinition("Queues", Some typeof<obj>, HideObjectMethods = true)
    let createQueueMember = createQueueMemberType connectionString domainType
    queueListingType.AddMembersDelayed(fun () ->
        getQueues(connectionString)
        |> List.map createQueueMember 
        |> List.map(fun (name, queueType) ->
            ProvidedProperty(name, queueType, GetterCode = fun _ -> <@@ ProvidedQueue(connectionString, name) @@> )))
    domainType.AddMember queueListingType
    let queueListingProp = ProvidedProperty("Queues", queueListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    queueListingProp.AddXmlDoc "Gets the list of all queues in this storage account."
    queueListingProp