module internal FSharp.Azure.StorageTypeProvider.Queue.QueueMemberFactory

open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Queue.QueueRepository
open Microsoft.WindowsAzure.Storage.Queue
open ProviderImplementation.ProvidedTypes

let createIndividualsForQueue (connectionString, domainType:ProvidedTypeDefinition, queueName) =
    let individualsType = ProvidedTypeDefinition(sprintf "%s.queue.%s.messages" connectionString queueName, None, HideObjectMethods = true)       
    domainType.AddMember individualsType
    individualsType.AddMembersDelayed(fun () ->
        peekMessages(connectionString, queueName, 20)
        |> List.map(fun msg -> ProvidedProperty(msg, typeof<string>, GetterCode = (fun _ -> <@@ () @@>))))
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