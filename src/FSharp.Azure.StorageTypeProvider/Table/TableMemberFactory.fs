module internal FSharp.Azure.StorageTypeProvider.Table.TableMemberFactory

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage.Table
open ProviderImplementation.ProvidedTypes

/// Builds up the Table Storage member
let getTableStorageMembers schemaInferenceRowCount (connectionString, domainType : ProvidedTypeDefinition) =
    /// Creates an individual Table member
    let createTableType connectionString tableName propertyName = 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>, HideObjectMethods = true)
        let tableType = ProvidedTypeDefinition(tableName + "Table", Some typeof<AzureTable>, HideObjectMethods = true)
        domainType.AddMembers [ tableEntityType; tableType ]
        TableEntityMemberFactory.buildTableEntityMembers schemaInferenceRowCount (tableType, tableEntityType, domainType, connectionString, tableName)
        let tableProp = ProvidedProperty(propertyName, tableType, GetterCode = (fun _ -> <@@ TableBuilder.createAzureTable connectionString tableName @@>))
        tableProp.AddXmlDoc <| sprintf "Provides access to the '%s' table." tableName
        tableProp

    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>, HideObjectMethods = true)
    domainType.AddMember tableListingType
    
    getTables connectionString
    |> Seq.map (fun table -> createTableType connectionString table table)
    |> Seq.toList
    |> tableListingType.AddMembers

    // Get any metrics tables that are available
    let metrics = getMetricsTables connectionString    
    if metrics <> Seq.empty then
        let metricsTablesType = ProvidedTypeDefinition("$Azure_Metrics", Some typeof<obj>, HideObjectMethods = true)
        domainType.AddMember metricsTablesType

        for (period, theLocation, service, tableName) in metrics do
            createTableType connectionString tableName (sprintf "%s %s metrics (%s)" period service theLocation)
            |> metricsTablesType.AddMember

        let metricsTablesProp = ProvidedProperty("Azure Metrics", metricsTablesType, GetterCode = (fun _ -> <@@ () @@>))
        metricsTablesProp.AddXmlDoc "Provides access to metrics tables populated by Azure that are available on this storage account."
        tableListingType.AddMember metricsTablesProp

    let ctcProp = ProvidedProperty("CloudTableClient", typeof<CloudTableClient>, GetterCode = (fun _ -> <@@ TableBuilder.createAzureTableRoot connectionString @@>))
    ctcProp.AddXmlDoc "Gets a handle to the Table Azure SDK client for this storage account."
    tableListingType.AddMember ctcProp
      
    let tableListingProp = ProvidedProperty("Tables", tableListingType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>))
    tableListingProp.AddXmlDoc "Gets the list of all tables in this storage account."
    tableListingProp
