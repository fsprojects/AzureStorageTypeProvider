/// Generates top-level blob containers folders.
module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open Elastacloud.FSharp.AzureTypeProvider.MemberFactories
open Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory
open Elastacloud.FSharp.AzureTypeProvider.Repositories
open Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let rec private createFileItem (domainType : ProvidedTypeDefinition) connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, getContents) -> 
        let folderProp = ProvidedTypeDefinition((sprintf "%s.%s" containerName path), Some typeof<BlobFolder>, HideObjectMethods = true)
        domainType.AddMember(folderProp)
        folderProp.AddMembersDelayed(fun _ -> 
            (getContents()
             |> Array.map (createFileItem domainType connectionString containerName)
             |> Array.toList))
        ProvidedProperty("/" + name, folderProp, GetterCode = fun _ -> <@@ Builder.createBlobFolder connectionString containerName path @@>)
    | Blob(path, name, properties) -> 
        let fileDetails = connectionString, containerName, path        
        let fileType = 
            match path with
            | Builder.XML -> "XmlFile"
            | Builder.Binary | Builder.Text -> "BlobFile"
        let fileTypeDefinition = domainType.GetMember(fileType).[0] :?> ProvidedTypeDefinition
        ProvidedProperty(name, fileTypeDefinition, GetterCode = fun _ -> <@@ Builder.createBlobFile connectionString containerName path @@>)

let private createContainerType (domainType : ProvidedTypeDefinition) connectionString (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<BlobContainer>, HideObjectMethods = true)
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.GetFiles()
         |> Seq.map (createFileItem domainType connectionString container.Name)
         |> Seq.toList))
    domainType.AddMember(individualContainerType)
    // this local binding is required for the quotation.
    let containerName = container.Name
    ProvidedProperty
        (container.Name, individualContainerType, IsStatic = true, GetterCode = fun _ -> <@@ Builder.createContainer connectionString containerName @@>)

/// Builds up the Blob Storage container members
let getBlobStorageMembers connectionString (domainType : ProvidedTypeDefinition) = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
    containerListingType.AddMembersDelayed(fun _ -> BlobRepository.getBlobStorageAccountManifest (connectionString)
                                                    |> List.map (createContainerType domainType connectionString))
    containerListingType

// Creates an individual Table member
let private createTableType (domainType : ProvidedTypeDefinition) connectionString tableName = 
    let tableProperty = ProvidedTypeDefinition(tableName, Some typeof<obj>)
    tableProperty.AddMembersDelayed(fun _ -> 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>, HideObjectMethods = true)
        let createdTypes, createdMembers = TableEntityMemberFactory.buildTableEntityMembers tableEntityType connectionString tableName
        domainType.AddMembers(tableEntityType :: createdTypes)
        createdMembers)
    tableProperty

/// Builds up the Table Storage member
let getTableStorageMembers connectionString domainType = 
    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>)
    tableListingType.AddMembersDelayed(fun _ -> 
        TableRepository.getTables connectionString
        |> Seq.map (createTableType domainType connectionString)
        |> Seq.toList)
    tableListingType
