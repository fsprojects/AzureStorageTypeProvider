/// Generates top-level blob containers folders.
module internal FSharp.Azure.StorageTypeProvider.Blob.BlobMemberFactory

open FSharp.Azure.StorageTypeProvider.Blob.BlobRepository
open ProviderImplementation.ProvidedTypes
open System
open Microsoft.WindowsAzure.Storage.Blob

let rec private createBlobItem (domainType : ProvidedTypeDefinition) (ctx:ProvidedTypesContext) connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, contents) ->
        let folderProp = ctx.ProvidedTypeDefinition((sprintf "%s.%s" containerName path), Some typeof<BlobFolder>, hideObjectMethods = true)
        domainType.AddMember(folderProp)
        folderProp.AddMembersDelayed(fun _ -> 
            (contents.Value
             |> Array.choose (createBlobItem domainType ctx connectionString containerName)
             |> Array.toList))
        Some <| ctx.ProvidedProperty(name, folderProp, getterCode = fun _ -> <@@ ContainerBuilder.createBlobFolder connectionString containerName path @@>)
    | Blob(path, name, blobType, length) -> 
        let fileTypeDefinition = 
            match blobType, path with
            | BlobType.PageBlob, _ -> "PageBlob"
            | _, BlobBuilder.XML -> "XmlBlob"
            | _, BlobBuilder.Binary | _, BlobBuilder.Text -> "BlockBlob"
            |> fun typeName -> domainType.GetMember(typeName).[0] :?> ProvidedTypeDefinition

        match blobType, length with
        | _, Some 0L -> None
        | BlobType.PageBlob, _ -> Some <| ctx.ProvidedProperty(name, fileTypeDefinition, getterCode = fun _ -> <@@ BlobBuilder.createPageBlobFile connectionString containerName path @@>)
        | BlobType.BlockBlob, _ -> Some <| ctx.ProvidedProperty(name, fileTypeDefinition, getterCode = fun _ -> <@@ BlobBuilder.createBlockBlobFile connectionString containerName path @@>)
        | _ -> None

let private createContainerType (domainType : ProvidedTypeDefinition) (ctx:ProvidedTypesContext) connectionString (container : LightweightContainer) = 
    let individualContainerType = ctx.ProvidedTypeDefinition(container.Name + "Container", Some typeof<BlobContainer>, hideObjectMethods = true)
    individualContainerType.AddXmlDoc <| sprintf "Provides access to the '%s' container." container.Name
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.Contents.Value
         |> Seq.choose (createBlobItem domainType ctx connectionString container.Name)
         |> Seq.toList))
    domainType.AddMember(individualContainerType)
    // this local binding is required for the quotation.
    let containerName = container.Name
    let containerProp = 
        ctx.ProvidedProperty(container.Name, individualContainerType, getterCode = fun _ -> <@@ ContainerBuilder.createContainer connectionString containerName @@>)
    containerProp.AddXmlDocDelayed(fun () -> sprintf "Provides access to the '%s' container." containerName)
    containerProp

/// Builds up the Blob Storage container members
let getBlobStorageMembers staticSchema (connectionString, domainType : ProvidedTypeDefinition, ctx:ProvidedTypesContext) = 
    let containerListingType = ctx.ProvidedTypeDefinition("Containers", Some typeof<obj>, hideObjectMethods = true)
    let createContainerType = createContainerType domainType ctx connectionString
    
    match staticSchema with
    | [] -> containerListingType.AddMembersDelayed(fun _ -> connectionString |> getBlobStorageAccountManifest |> List.map createContainerType)
    | staticSchema -> staticSchema |> List.map createContainerType |> containerListingType.AddMembers
    
    domainType.AddMember containerListingType

    let cbcProp = ctx.ProvidedProperty("CloudBlobClient", typeof<CloudBlobClient>, getterCode = (fun _ -> <@@ ContainerBuilder.createBlobClient connectionString @@>))
    cbcProp.AddXmlDoc "Gets a handle to the Blob Azure SDK client for this storage account."
    containerListingType.AddMember(cbcProp)

    let containerListingProp = ctx.ProvidedProperty("Containers", containerListingType, isStatic = true, getterCode = (fun _ -> <@@ () @@>))
    containerListingProp.AddXmlDoc "Gets the list of all containers in this storage account."
    containerListingProp