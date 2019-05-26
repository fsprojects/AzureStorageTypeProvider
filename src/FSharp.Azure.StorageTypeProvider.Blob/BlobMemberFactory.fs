/// Generates top-level blob containers folders.
module internal FSharp.Azure.StorageTypeProvider.Blob.BlobMemberFactory

open FSharp.Azure.StorageTypeProvider.Blob.BlobRepository
open ProviderImplementation.ProvidedTypes
open System
open Microsoft.Azure.Storage.Blob
open FSharp.Control.Tasks

let rec private createBlobItem (domainType : ProvidedTypeDefinition) connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, contents) ->
        let folderProp = ProvidedTypeDefinition((sprintf "%s.%s" containerName path), Some typeof<BlobFolder>, hideObjectMethods = true)
        domainType.AddMember(folderProp)
        folderProp.AddMembersDelayed(fun _ ->
            contents
            |> Async.RunSynchronously
            |> Array.choose (createBlobItem domainType connectionString containerName)
            |> Array.toList)
        Some <| ProvidedProperty(name, folderProp, getterCode = fun _ -> <@@ ContainerBuilder.createBlobFolder connectionString containerName path @@>)
    | Blob(path, name, blobType, length) -> 
        match blobType, length with
        | _, Some 0L -> None
        | BlobType.PageBlob, _ -> Some <| ProvidedProperty(name, typeof<PageBlobFile>, getterCode = fun _ -> <@@ BlobBuilder.createPageBlobFile connectionString containerName path @@>)
        | BlobType.BlockBlob, _ ->
            let blobType =
                match path with
                | BlobBuilder.XML -> typeof<XmlFile>
                | BlobBuilder.Binary | BlobBuilder.Text -> typeof<BlockBlobFile>            
            Some <| ProvidedProperty(name, blobType, getterCode = fun _ -> <@@ BlobBuilder.createBlockBlobFile connectionString containerName path @@>)
        | _ -> None

let private createContainerType (domainType : ProvidedTypeDefinition) connectionString (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name + "Container", Some typeof<BlobContainer>, hideObjectMethods = true)
    individualContainerType.AddXmlDoc <| sprintf "Provides access to the '%s' container." container.Name
    individualContainerType.AddMembersDelayed(fun _ -> 
        container.Contents
        |> Async.RunSynchronously
        |> Array.choose (createBlobItem domainType connectionString container.Name)
        |> Array.toList)
    domainType.AddMember individualContainerType
    // this local binding is required for the quotation.
    let containerName = container.Name
    let containerProp = 
        ProvidedProperty(container.Name, individualContainerType, getterCode = fun _ -> <@@ ContainerBuilder.createContainer connectionString containerName @@>)
    containerProp.AddXmlDocDelayed(fun () -> sprintf "Provides access to the '%s' container." containerName)
    containerProp

/// Builds up the Blob Storage container members
let getBlobStorageMembers staticSchema (connectionString, domainType : ProvidedTypeDefinition) = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>, hideObjectMethods = true)
    let createContainerType = createContainerType domainType connectionString
    
    match staticSchema with
    | [] -> containerListingType.AddMembersDelayed(fun _ ->
        getBlobStorageAccountManifest connectionString
        |> Async.RunSynchronously
        |> Array.map createContainerType
        |> Array.toList)
    | staticSchema ->
        staticSchema
        |> List.map createContainerType
        |> containerListingType.AddMembers
    
    domainType.AddMember containerListingType

    let cbcProp = ProvidedProperty("CloudBlobClient", typeof<CloudBlobClient>, getterCode = (fun _ -> <@@ ContainerBuilder.createBlobClient connectionString @@>))
    cbcProp.AddXmlDoc "Gets a handle to the Blob Azure SDK client for this storage account."
    containerListingType.AddMember(cbcProp)

    let containerListingProp = ProvidedProperty("Containers", containerListingType, isStatic = true, getterCode = (fun _ -> <@@ () @@>))
    containerListingProp.AddXmlDoc "Gets the list of all containers in this storage account."
    Some containerListingProp