/// Generates top-level blob containers folders.
module internal FSharp.Azure.StorageTypeProvider.Blob.BlobMemberFactory

open FSharp.Azure.StorageTypeProvider.Blob.BlobRepository
open ProviderImplementation.ProvidedTypes
open System
open Microsoft.WindowsAzure.Storage.Blob

let rec private createBlobItem (domainType : ProvidedTypeDefinition) connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, contents) ->
        let folderProp = ProvidedTypeDefinition((sprintf "%s.%s" containerName path), Some typeof<BlobFolder>, HideObjectMethods = true)
        domainType.AddMember(folderProp)
        folderProp.AddMembersDelayed(fun _ -> 
            (contents.Value
             |> Array.choose (createBlobItem domainType connectionString containerName)
             |> Array.toList))
        Some <| ProvidedProperty(name, folderProp, GetterCode = fun _ -> <@@ ContainerBuilder.createBlobFolder connectionString containerName path @@>)
    | Blob(path, name, blobType, length) -> 
        let fileTypeDefinition = 
            match blobType, path with
            | BlobType.PageBlob, _ -> "PageBlob"
            | _, BlobBuilder.XML -> "XmlBlob"
            | _, BlobBuilder.Binary | _, BlobBuilder.Text -> "BlockBlob"
            |> fun typeName -> domainType.GetMember(typeName).[0] :?> ProvidedTypeDefinition

        match blobType, length with
        | _, Some 0L -> None
        | BlobType.PageBlob, _ -> Some <| ProvidedProperty(name, fileTypeDefinition, GetterCode = fun _ -> <@@ BlobBuilder.createPageBlobFile connectionString containerName path @@>)
        | BlobType.BlockBlob, _ -> Some <| ProvidedProperty(name, fileTypeDefinition, GetterCode = fun _ -> <@@ BlobBuilder.createBlockBlobFile connectionString containerName path @@>)
        | _ -> None

let private createContainerType (domainType : ProvidedTypeDefinition) connectionString (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name + "Container", Some typeof<BlobContainer>, HideObjectMethods = true)
    individualContainerType.AddXmlDoc <| sprintf "Provides access to the '%s' container." container.Name
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.Contents.Value
         |> Seq.choose (createBlobItem domainType connectionString container.Name)
         |> Seq.toList))
    domainType.AddMember(individualContainerType)
    // this local binding is required for the quotation.
    let containerName = container.Name
    let containerProp = 
        ProvidedProperty(container.Name, individualContainerType, GetterCode = fun _ -> <@@ ContainerBuilder.createContainer connectionString containerName @@>)
    containerProp.AddXmlDocDelayed(fun () -> sprintf "Provides access to the '%s' container." containerName)
    containerProp

/// Builds up the Blob Storage container members
let getBlobStorageMembers staticSchema (connectionString, domainType : ProvidedTypeDefinition) = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>, HideObjectMethods = true)
    
    match staticSchema with
    | Some staticSchema -> containerListingType.AddMembers ([ staticSchema ] |> List.map (createContainerType domainType connectionString))
    | None -> containerListingType.AddMembersDelayed(fun _ -> getBlobStorageAccountManifest (connectionString) |> List.map (createContainerType domainType connectionString))
    
    domainType.AddMember containerListingType

    let cbcProp = ProvidedProperty("CloudBlobClient", typeof<CloudBlobClient>, GetterCode = (fun _ -> <@@ ContainerBuilder.createBlobClient connectionString @@>))
    cbcProp.AddXmlDoc "Gets a handle to the Blob Azure SDK client for this storage account."
    containerListingType.AddMember(cbcProp)

    let containerListingProp = ProvidedProperty("Containers", containerListingType, GetterCode = (fun _ -> <@@ () @@>), IsStatic = true)
    containerListingProp.AddXmlDoc "Gets the list of all containers in this storage account."
    containerListingProp