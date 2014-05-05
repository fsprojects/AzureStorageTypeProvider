/// Generates top-level blob containers folders.
module internal FSharp.Azure.StorageTypeProvider.ContainerTypeFactory

open FSharp.Azure.StorageTypeProvider.MemberFactories
open FSharp.Azure.StorageTypeProvider.MemberFactories.TableEntityMemberFactory
open FSharp.Azure.StorageTypeProvider.Repositories
open FSharp.Azure.StorageTypeProvider.Repositories.BlobRepository
open FSharp.Azure.StorageTypeProvider.Repositories.TableRepository
open FSharp.Azure.StorageTypeProvider.Types
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let rec private createBlobItem (domainType : ProvidedTypeDefinition) connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, getContents) -> 
        let folderProp = ProvidedTypeDefinition((sprintf "%s.%s" containerName path), Some typeof<BlobFolder>, HideObjectMethods = true)
        domainType.AddMember(folderProp)
        folderProp.AddMembersDelayed(fun _ -> 
            (getContents()
             |> Array.choose (createBlobItem domainType connectionString containerName)
             |> Array.toList))
        Some <| ProvidedProperty(name, folderProp, GetterCode = fun _ -> <@@ ContainerBuilder.createBlobFolder connectionString containerName path @@>)
    | Blob(path, name, properties) -> 
        let fileDetails = connectionString, containerName, path
        
        let fileType = 
            match path with
            | ContainerBuilder.XML -> "XmlFile"
            | ContainerBuilder.Binary | ContainerBuilder.Text -> "BlobFile"
        
        let fileTypeDefinition = domainType.GetMember(fileType).[0] :?> ProvidedTypeDefinition
        match BlobFile(connectionString, containerName, path).Size with
        | 0L -> None
        | _ -> 
            Some 
            <| ProvidedProperty(name, fileTypeDefinition, GetterCode = fun _ -> <@@ ContainerBuilder.createBlobFile connectionString containerName path @@>)

let private createContainerType (domainType : ProvidedTypeDefinition) connectionString (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name + "Container", Some typeof<BlobContainer>, HideObjectMethods = true)
    individualContainerType.AddXmlDoc <| sprintf "Provides access to the '%s' container." container.Name
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.GetFiles()
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
let getBlobStorageMembers (connectionString, domainType : ProvidedTypeDefinition) = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>, HideObjectMethods = true)
    containerListingType.AddMembersDelayed
        (fun _ -> BlobRepository.getBlobStorageAccountManifest (connectionString) |> List.map (createContainerType domainType connectionString))
    domainType.AddMember containerListingType
    let containerListingProp = ProvidedProperty("Containers", containerListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    containerListingProp.AddXmlDoc "Gets the list of all containers in this storage account."
    containerListingProp

/// Builds up the Table Storage member
let getTableStorageMembers (connectionString, domainType : ProvidedTypeDefinition) = 
    
    /// Creates an individual Table member
    let createTableType connectionString tableName = 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>, HideObjectMethods = true)
        let tableType = ProvidedTypeDefinition(tableName + "Table", Some typeof<AzureTable>, HideObjectMethods = true)
        domainType.AddMembers [ tableEntityType; tableType ]
        TableEntityMemberFactory.buildTableEntityMembers (tableType, tableEntityType, domainType, connectionString, tableName)
        let tableProp = ProvidedProperty(tableName, tableType, GetterCode = (fun _ -> <@@ TableBuilder.createAzureTable connectionString tableName @@>))
        tableProp.AddXmlDoc <| sprintf "Provides access to the '%s' table." tableName
        tableProp

    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>, HideObjectMethods = true)
    tableListingType.AddMembersDelayed(fun _ -> 
        TableRepository.getTables connectionString
        |> Seq.map (createTableType connectionString)
        |> Seq.toList)
    
    domainType.AddMember tableListingType
    let tableListingProp = ProvidedProperty("Tables", tableListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    tableListingProp.AddXmlDoc "Gets the list of all tables in this storage account."
    tableListingProp
