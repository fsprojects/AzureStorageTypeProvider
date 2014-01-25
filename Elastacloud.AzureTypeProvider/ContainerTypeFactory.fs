/// Generates top-level blob containers folders.
module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories
open Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository
open Elastacloud.FSharp.AzureTypeProvider.MemberFactories
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let rec private createFileItem connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, getContents) -> 
        let folderProp = ProvidedTypeDefinition("/" + name, Some typeof<obj>)
        folderProp.AddMembersDelayed
            (fun _ -> 
            [ BlobMemberFactory.createDownloadFolderFunction (connectionString, containerName, path) :> MemberInfo ] 
            @ (getContents()
               |> Array.map (createFileItem connectionString containerName)
               |> Array.toList))
        folderProp :> MemberInfo
    | Blob(path, name, properties) -> 
        let fileDetails = connectionString, containerName, path
        let fileProp = ProvidedTypeDefinition(name, Some typeof<obj>)
        fileProp.AddMembersDelayed(fun _ -> 
            [ BlobMemberFactory.createFileDetailsProperty path properties :> MemberInfo
              BlobMemberFactory.createDownloadFunction fileDetails :> MemberInfo
              BlobMemberFactory.createDownloadFileFunction fileDetails :> MemberInfo
              BlobMemberFactory.createGenerateSasFunction fileDetails :> MemberInfo ])
        fileProp :> MemberInfo

let private createContainerType connectionString (container:LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.GetFiles()
         |> Seq.map (createFileItem connectionString container.Name)
         |> Seq.toList) @ [ BlobMemberFactory.createUploadFileFunction (connectionString, container.Name)
                            BlobMemberFactory.createDownloadContainerFunction (connectionString, container.Name) ])
    individualContainerType

/// Builds up the Blob Storage container members
let getBlobStorageMembers connectionString = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
    containerListingType.AddMembersDelayed
        (fun _ -> BlobRepository.getBlobStorageAccountManifest (connectionString) 
                  |> List.map (createContainerType connectionString))
    containerListingType

let private createEntityProperty (_,id,timestamp,properties) =
    let entityProperty = ProvidedTypeDefinition (id, Some typeof<obj>)
    entityProperty.AddMembersDelayed(fun _ ->
        ProvidedProperty (sprintf "Timestamp: %A" timestamp , typeof<DateTimeOffset>, GetterCode = (fun args -> <@@ timestamp @@>), IsStatic = true) 
        :: (properties
            |> Seq.map(fun (key,value) -> ProvidedProperty (sprintf "%s: %A" key value, typeof<obj>, GetterCode = (fun args -> <@@ value @@>), IsStatic = true))
            |> Seq.toList))
    entityProperty

// Creates an individual Table member
let private createTableType connectionString tableName =
    let tableProperty = ProvidedTypeDefinition(tableName, Some typeof<obj>)
    tableProperty.AddMembersDelayed(fun _ ->
        let groupedByPartitionKey = Repositories.TableRepository.getTopRows 50 tableName connectionString
                                   |> Seq.groupBy(fun (part,_,_,_) -> part)
        groupedByPartitionKey
        |> Seq.map(fun partitionGroup ->
            let partitionMember = ProvidedTypeDefinition(fst partitionGroup, Some typeof<obj>)
            partitionMember.AddMembersDelayed(fun _ ->
                partitionGroup
                |> snd
                |> Seq.map createEntityProperty
                |> Seq.toList)
            partitionMember)
        |> Seq.toList)
    tableProperty
    
/// Builds up the Table Storage member
let getTableStorageMembers (connectionString : string) = 
    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>)
    tableListingType.AddMembersDelayed
        (fun _ -> TableRepository.getTables connectionString
                  |> Seq.map (createTableType connectionString)
                  |> Seq.toList)
    tableListingType