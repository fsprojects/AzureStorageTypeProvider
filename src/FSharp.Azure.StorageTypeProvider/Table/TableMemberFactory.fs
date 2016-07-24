module internal FSharp.Azure.StorageTypeProvider.Table.TableMemberFactory

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open ProviderImplementation.ProvidedTypes

/// Builds up the Table Storage member
let getTableStorageMembers schemaInferenceRowCount (connectionString, domainType : ProvidedTypeDefinition) =     
    /// Creates an individual Table member
    let createTableType connectionString tableName = 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>, HideObjectMethods = true)
        let tableType = ProvidedTypeDefinition(tableName + "Table", Some typeof<AzureTable>, HideObjectMethods = true)
        domainType.AddMembers [ tableEntityType; tableType ]
        TableEntityMemberFactory.buildTableEntityMembers schemaInferenceRowCount (tableType, tableEntityType, domainType, connectionString, tableName)
        let tableProp = ProvidedProperty(tableName, tableType, GetterCode = (fun _ -> <@@ TableBuilder.createAzureTable connectionString tableName @@>))
        tableProp.AddXmlDoc <| sprintf "Provides access to the '%s' table." tableName
        tableProp

    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>, HideObjectMethods = true)
    getTables connectionString
    |> Seq.map (createTableType connectionString)
    |> Seq.toList
    |> tableListingType.AddMembers
    
    let ctcProp = ProvidedProperty("CloudTableClient", typeof<CloudTableClient>, GetterCode = (fun _ -> <@@ TableBuilder.createAzureTableRoot connectionString @@>))
    ctcProp.AddXmlDoc "Gets a handle to the Table Azure SDK client for this storage account."
    tableListingType.AddMember(ctcProp)
    
    domainType.AddMember tableListingType
    let tableListingProp = ProvidedProperty("Tables", tableListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    tableListingProp.AddXmlDoc "Gets the list of all tables in this storage account."
    tableListingProp