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

let private createContainerType connectionString (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.GetFiles()
         |> Seq.map (createFileItem connectionString container.Name)
         |> Seq.toList) @ [ BlobMemberFactory.createUploadFileFunction (connectionString, container.Name)
                            BlobMemberFactory.createDownloadContainerFunction (connectionString, container.Name) ])
    individualContainerType

/// Builds up the Blob Storage container members
let getBlobStorageMembers connectionString _ = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
    containerListingType.AddMembersDelayed
        (fun _ -> 
        BlobRepository.getBlobStorageAccountManifest (connectionString) 
        |> List.map (createContainerType connectionString))
    containerListingType

// Creates an individual Table member
let private createTableType (domainType : ProvidedTypeDefinition) connectionString tableName = 
    let tableProperty = ProvidedTypeDefinition(tableName, Some typeof<obj>)
    tableProperty.AddMembersDelayed
        (fun _ -> 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>)
        tableEntityType.HideObjectMethods <- true
        let propertiesCreated = 
            tableName
            |> getRowsForSchema 5 connectionString
            |> setPropertiesForEntity tableEntityType
        domainType.AddMember tableEntityType
        let partitionType = createPartitionType tableEntityType connectionString tableName
        let queryBuilderType = TableQueryBuilder.createTableQueryType domainType tableEntityType connectionString tableName propertiesCreated
        domainType.AddMembers [ partitionType; queryBuilderType ]
        [ ProvidedMethod
              ("GetPartition", [ ProvidedParameter("key", typeof<string>) ], partitionType, 
               InvokeCode = (fun args -> <@@ (%%args.[0] : string) @@>), IsStaticMethod = true)
          
          ProvidedMethod
              ("ExecuteQuery", [ ProvidedParameter("rawQuery", typeof<string>) ], (tableEntityType.MakeArrayType()), 
               InvokeCode = (fun args -> <@@ executeQuery (%%args.[0] : string) connectionString tableName @@>), 
               IsStaticMethod = true)
          
          ProvidedMethod
              ("Where", [], queryBuilderType, InvokeCode = (fun args -> <@@ ([] : string list) @@>), IsStaticMethod = true)
          
          ProvidedMethod
              ("GetEntity", 
               [ ProvidedParameter("key", typeof<string>)
                 ProvidedParameter("partition", typeof<string>, optionalValue = null) ], 
               (typeof<option<_>>).GetGenericTypeDefinition().MakeGenericType(tableEntityType), 
               
               InvokeCode = (fun args -> 
               <@@ getEntity (%%args.[0] : string) (%%args.[1] : string) connectionString tableName @@>), 
               IsStaticMethod = true) ])
    tableProperty

/// Builds up the Table Storage member
let getTableStorageMembers connectionString domainType = 
    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>)
    tableListingType.AddMembersDelayed(fun _ -> 
        TableRepository.getTables connectionString
        |> Seq.map (createTableType domainType connectionString)
        |> Seq.toList)
    tableListingType
