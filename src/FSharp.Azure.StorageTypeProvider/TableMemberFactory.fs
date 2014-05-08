module internal FSharp.Azure.StorageTypeProvider.Table.TableMemberFactory

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes

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
        getTables connectionString
        |> Seq.map (createTableType connectionString)
        |> Seq.toList)
    
    domainType.AddMember tableListingType
    let tableListingProp = ProvidedProperty("Tables", tableListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    tableListingProp.AddXmlDoc "Gets the list of all tables in this storage account."
    tableListingProp


