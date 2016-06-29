﻿///[omit]
///Contains helper functions for accessing tables
module FSharp.Azure.StorageTypeProvider.Table.TableRepository

open FSharp.Azure.StorageTypeProvider.Table
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable
open System

/// Suggests batch sizes based on a given entity type and published EDM property type sizes (source: https://msdn.microsoft.com/en-us/library/dd179338.aspx)
module private BatchCalculator =
    /// The basic size of a single row with no custom properties. 
    let private basicRowSize =
        let partitionKey = 1
        let rowKey = 1
        let timestamp = 2
        partitionKey + rowKey + timestamp

    /// Gets the maximum size, in KB, of a single property.
    let private getMaxPropertySize (property:EntityProperty) =
        match property.PropertyType with 
        | Table.EdmType.DateTime -> 2
        | Table.EdmType.Binary -> 64
        | Table.EdmType.Boolean -> 1
        | Table.EdmType.Double -> 2
        | Table.EdmType.Guid -> 4
        | Table.EdmType.Int32 -> 1
        | Table.EdmType.Int64 -> 1
        | Table.EdmType.String -> 64
        | unknown -> failwith (sprintf "Unknown EdmType %A" unknown)

    /// Calculates the maximum size of a given entity. 
    let private getMaxEntitySize (entity:DynamicTableEntity) =
        let entityRowSize = entity.Properties.Values |> Seq.sumBy getMaxPropertySize
        basicRowSize + entityRowSize
        
    let private maximumBatchSizeKb = 4000

    /// Calculates the maximum number of entities of a given type that can be inserted in a single batch.
    let getBatchSize entity = maximumBatchSizeKb / (getMaxEntitySize entity)

let internal getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

let buildTableEntity partitionKey rowKey names (values: obj []) = 
    LightweightTableEntity(partitionKey, rowKey, DateTimeOffset.MinValue, (Seq.zip names values) |> Map.ofSeq)

let internal getTable tableName connection = 
    let client = getTableClient connection
    client.GetTableReference tableName

/// Gets all tables
let internal getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map(fun table -> table.Name)

type private DynamicQuery = TableQuery<DynamicTableEntity>

let internal getRowsForSchema (rowCount: int) connection tableName = 
    let table = getTable tableName connection
    table.ExecuteQuery(DynamicQuery().Take(Nullable rowCount))
    |> Seq.truncate rowCount
    |> Seq.toArray

let toLightweightTableEntity (dte:DynamicTableEntity) = 
    LightweightTableEntity(
                    Partition dte.PartitionKey,
                    Row dte.RowKey,
                    dte.Timestamp,
                    dte.Properties
                    |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject)
                    |> Map.ofSeq)

let executeQueryAsync connection tableName maxResults filterString = async {
        let query = DynamicQuery().Where(filterString)
        let query = if maxResults > 0 then query.Take(Nullable maxResults) else query
        
        let resSet = ResizeArray<LightweightTableEntity>()
        let getBatch contTkn = async {
            let! batch = (getTable tableName connection).ExecuteQuerySegmentedAsync(query,contTkn) |> Async.AwaitTask
            batch 
            |> Seq.map(toLightweightTableEntity)
            |> resSet.AddRange
            return contTkn
        } 
        let! firstContTkn = getBatch null
        let mutable contTkn = firstContTkn
        while (contTkn <> null) do
            let! nextTkn = getBatch contTkn
            contTkn <- nextTkn

        return resSet |> Array.ofSeq
    }

let executeQuery connection tableName maxResults filterString = 
    let query = DynamicQuery().Where(filterString)
    let query = if maxResults > 0 then query.Take(Nullable maxResults) else query

    (getTable tableName connection).ExecuteQuery(query)
    |> Seq.map(toLightweightTableEntity)
    |> Seq.toArray

let internal buildDynamicTableEntity(entity:LightweightTableEntity) =
    let tableEntity = DynamicTableEntity(entity.PartitionKey, entity.RowKey, ETag = "*")
    for (key, value) in entity.Values |> Map.toArray do
        tableEntity.Properties.[key] <-
            match value with
            | :? (byte []) as value -> EntityProperty.GeneratePropertyForByteArray(value)
            | :? string as value -> EntityProperty.GeneratePropertyForString(value)
            | :? int as value -> EntityProperty.GeneratePropertyForInt(Nullable value)
            | :? bool as value -> EntityProperty.GeneratePropertyForBool(Nullable value)
            | :? DateTime as value -> EntityProperty.GeneratePropertyForDateTimeOffset(Nullable(DateTimeOffset value))
            | :? double as value -> EntityProperty.GeneratePropertyForDouble(Nullable value)
            | :? System.Guid as value -> EntityProperty.GeneratePropertyForGuid(Nullable value)
            | :? int64 as value -> EntityProperty.GeneratePropertyForLong(Nullable value)
            | _ -> EntityProperty.CreateEntityPropertyFromObject(value)
    tableEntity

let internal createInsertOperation(insertMode) = 
    match insertMode with
    | TableInsertMode.Insert -> TableOperation.Insert
    | TableInsertMode.Upsert -> TableOperation.InsertOrReplace
    | _ -> failwith "unknown insertion mode"

let private batch size source =
    let rec doBatch output currentBatch counter remainder =
        match remainder with
        | [] -> if currentBatch = [] then output else currentBatch::output |> List.rev
        | theList when counter = size -> doBatch ((currentBatch |> List.rev) ::output) [] 0 theList
        | head::tail -> doBatch output (head::currentBatch) (counter + 1) tail
    doBatch [] [] 0 (source |> Seq.toList)

let internal executeBatchOperation createTableOp (table:CloudTable) entities =
    let batchSize = entities |> Seq.head |> BatchCalculator.getBatchSize
    entities
    |> Seq.groupBy(fun (entity:DynamicTableEntity) -> entity.PartitionKey)
    |> Seq.collect(fun (partitionKey, entities) -> 
           entities
           |> batch batchSize
           |> Seq.map(fun entityBatch ->
                let batchForPartition = TableBatchOperation()
                entityBatch |> Seq.iter (createTableOp >> batchForPartition.Add)
                partitionKey, entityBatch, batchForPartition))
    |> Seq.map(fun (partitionKey, entityBatch, batchOperation) ->
        let buildEntityId (entity:DynamicTableEntity) = Partition(entity.PartitionKey), Row(entity.RowKey)
        let responses =
            try
            table.ExecuteBatch(batchOperation)
            |> Seq.zip entityBatch
            |> Seq.map(fun (entity, res) -> SuccessfulResponse(buildEntityId entity, res.HttpStatusCode))
            with :? StorageException as ex ->
            let requestInformation = ex.RequestInformation
            match requestInformation.ExtendedErrorInformation.ErrorMessage.Split('\n').[0].Split(':') with
            | [|index;message|] ->
                match Int32.TryParse(index) with
                | true, index ->
                    entityBatch
                    |> Seq.mapi(fun entityIndex entity ->
                        if entityIndex = index then EntityError(buildEntityId entity, requestInformation.HttpStatusCode, requestInformation.ExtendedErrorInformation.ErrorCode)
                        else BatchOperationFailedError(buildEntityId entity))
                | _ -> entityBatch |> Seq.map(fun entity -> BatchError(buildEntityId entity, requestInformation.HttpStatusCode, requestInformation.ExtendedErrorInformation.ErrorCode))
            | [|message|] -> entityBatch |> Seq.map(fun entity -> EntityError(buildEntityId entity, requestInformation.HttpStatusCode, requestInformation.ExtendedErrorInformation.ErrorCode))
            | _ -> entityBatch |> Seq.map(fun entity -> BatchError(buildEntityId entity, requestInformation.HttpStatusCode, requestInformation.ExtendedErrorInformation.ErrorCode))
        partitionKey, responses |> Seq.toArray)

    |> Seq.toArray

let deleteEntities connection tableName entities =
    let table = getTable tableName connection
    entities
    |> Array.map buildDynamicTableEntity
    |> executeBatchOperation TableOperation.Delete table

let deleteEntity connection tableName entity =
    deleteEntities connection tableName [| entity |] |> Seq.head |> snd |> Seq.head
    
let insertEntityBatch connection tableName insertMode entities = 
    let table = getTable tableName connection
    let insertOp = createInsertOperation insertMode
    entities
    |> Seq.map buildDynamicTableEntity
    |> executeBatchOperation insertOp table

let insertEntity connection tableName insertMode entity = 
    insertEntityBatch connection tableName insertMode [entity] |> Seq.head |> snd |> Seq.head    

let composeAllFilters filters = 
    match filters with
    | [] -> String.Empty
    | _ -> 
        filters
        |> List.rev
        |> List.reduce(fun acc filter -> TableQuery.CombineFilters(acc, TableOperators.And, filter))

let buildFilter(propertyName, comparison, value) = 
    match box value with
    | :? string as value -> TableQuery.GenerateFilterCondition(propertyName, comparison, value)
    | :? int as value -> TableQuery.GenerateFilterConditionForInt(propertyName, comparison, value)
    | :? int64 as value -> TableQuery.GenerateFilterConditionForLong(propertyName, comparison, value)
    | :? float as value -> TableQuery.GenerateFilterConditionForDouble(propertyName, comparison, value)
    | :? bool as value -> TableQuery.GenerateFilterConditionForBool(propertyName, comparison, value)
    | :? DateTime as value -> TableQuery.GenerateFilterConditionForDate(propertyName, comparison, DateTimeOffset value)
    | :? Guid as value -> TableQuery.GenerateFilterConditionForGuid(propertyName, comparison, value)
    | _ -> TableQuery.GenerateFilterCondition(propertyName, comparison, value.ToString())

let getEntity rowKey partitionKey connection tableName = 
    let (Row rowKey, Partition partitionKey) = rowKey, partitionKey
    let results =
        [ ("RowKey", rowKey)
          ("PartitionKey", partitionKey) ]
        |> List.map(fun (prop, value) -> buildFilter(prop, QueryComparisons.Equal, value))
        |> composeAllFilters
        |> executeQuery connection tableName 0
    match results with
    | [| exactMatch |] -> Some exactMatch
    | _ -> None

let getPartitionRows (partitionKey:string) connection tableName = 
    buildFilter("PartitionKey", QueryComparisons.Equal, partitionKey) |> executeQuery connection tableName 0