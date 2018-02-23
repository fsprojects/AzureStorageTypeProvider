module internal FSharp.Azure.StorageTypeProvider.Table.TableMemberFactory

open FSharp.Azure.StorageTypeProvider.Table
open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage.Table
open ProviderImplementation.ProvidedTypes

/// Builds up the Table Storage member
let getTableStorageMembers optionalStaticSchema schemaInferenceRowCount humanize (connectionString, domainType : ProvidedTypeDefinition) =
    /// Creates an individual Table member
    let createTableType columnDefinitions connectionString tableName propertyName = 
        let tableEntityType = ProvidedTypeDefinition(tableName + "Entity", Some typeof<LightweightTableEntity>, hideObjectMethods = true)
        let tableType = ProvidedTypeDefinition(tableName + "Table", Some typeof<AzureTable>, hideObjectMethods = true)
        domainType.AddMembers [ tableEntityType; tableType ]

        TableEntityMemberFactory.buildTableEntityMembers columnDefinitions humanize (tableType, tableEntityType, domainType, connectionString, tableName)
        let tableProp = ProvidedProperty(propertyName, tableType, getterCode = (fun _ -> <@@ TableBuilder.createAzureTable connectionString tableName @@>))
        tableProp.AddXmlDoc <| sprintf "Provides access to the '%s' table." tableName
        tableProp

    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>, hideObjectMethods = true)
    domainType.AddMember tableListingType
    
    match optionalStaticSchema with
    | Some (optionalStaticSchema:StaticSchema.Parsed.TableSchema) ->
        optionalStaticSchema.Tables
        |> Array.map(fun table -> createTableType table.Columns connectionString table.Table table.Table)
        |> Array.toList
        |> tableListingType.AddMembers
    | None ->
        tableListingType.AddMembersDelayed(fun _ ->
            getTables connectionString
            |> Seq.map (fun table ->
                let schema = TableEntityMemberFactory.generateSchema table schemaInferenceRowCount connectionString
                createTableType schema connectionString table table)
            |> Seq.toList)

        // Get any metrics tables that are available
        let metrics = getMetricsTables connectionString    
        if metrics <> Seq.empty then
            tableListingType.AddMembersDelayed(fun _ ->
                let metricsTablesType = ProvidedTypeDefinition("$Azure_Metrics", Some typeof<obj>, hideObjectMethods = true)
                domainType.AddMember metricsTablesType

                for (period, theLocation, service, tableName) in metrics do
                    let schema = TableEntityMemberFactory.generateSchema tableName schemaInferenceRowCount connectionString
                    createTableType schema connectionString tableName (sprintf "%s %s metrics (%s)" period service theLocation)
                    |> metricsTablesType.AddMember

                let metricsTablesProp = ProvidedProperty("Azure Metrics", metricsTablesType, getterCode = (fun _ -> <@@ () @@>))
                metricsTablesProp.AddXmlDoc "Provides access to metrics tables populated by Azure that are available on this storage account."
                [ metricsTablesProp ])

    let ctcProp = ProvidedProperty("CloudTableClient", typeof<CloudTableClient>, getterCode = (fun _ -> <@@ TableBuilder.createAzureTableRoot connectionString @@>))
    ctcProp.AddXmlDoc "Gets a handle to the Table Azure SDK client for this storage account."
    tableListingType.AddMember ctcProp
      
    let tableListingProp = ProvidedProperty("Tables", tableListingType, isStatic = true, getterCode = (fun _ -> <@@ () @@>))
    tableListingProp.AddXmlDoc "Gets the list of all tables in this storage account."
    tableListingProp
