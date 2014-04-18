///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Elastacloud.FSharp.AzureTypeProvider
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable
open System

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

type LightweightTableEntity = 
    { PartitionKey: string
      RowKey: string
      Timestamp: System.DateTimeOffset
      Values: Map<string, obj> }

let buildTableEntity partitionKey rowKey names (values: obj []) = 
    { PartitionKey = partitionKey
      RowKey = rowKey
      Timestamp = DateTimeOffset.MinValue
      Values = (Seq.zip names values) |> Map.ofSeq }

let private getTable connection tableName = 
    let client = getTableClient connection
    client.GetTableReference tableName

/// Gets all tables
let internal getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map(fun table -> table.Name)

type DynamicQuery = TableQuery<DynamicTableEntity>

let internal getRowsForSchema (rowCount: int) connection tableName = 
    let table = getTable connection tableName
    table.ExecuteQuery(DynamicQuery().Take(Nullable<_>(rowCount)))
    |> Seq.truncate rowCount
    |> Seq.toArray

let executeQuery connection tableName maxResults filterString = 
    let query = DynamicQuery().Where(filterString)
    let query = if maxResults > 0 then query.Take(Nullable(maxResults)) else query

    (getTable connection tableName).ExecuteQuery(query)
    |> Seq.map(fun dte -> 
           { PartitionKey = dte.PartitionKey
             RowKey = dte.RowKey
             Timestamp = dte.Timestamp
             Values = 
                 dte.Properties
                 |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject)
                 |> Map.ofSeq })
    |> Seq.toArray

let private buildDynamicTableEntity entity =
    let tableEntity = DynamicTableEntity(entity.PartitionKey, entity.RowKey, ETag = "*")
    for (key, value) in entity.Values |> Map.toArray do
        tableEntity.Properties.[key] <- match value with
                                        | :? (byte []) as value -> EntityProperty.GeneratePropertyForByteArray(value)
                                        | :? string as value -> EntityProperty.GeneratePropertyForString(value)
                                        | :? int as value -> EntityProperty.GeneratePropertyForInt(Nullable(value))
                                        | :? bool as value -> EntityProperty.GeneratePropertyForBool(Nullable(value))
                                        | :? DateTime as value -> 
                                            EntityProperty.GeneratePropertyForDateTimeOffset
                                                (Nullable(DateTimeOffset(value)))
                                        | :? double as value -> 
                                            EntityProperty.GeneratePropertyForDouble(Nullable(value))
                                        | :? System.Guid as value -> 
                                            EntityProperty.GeneratePropertyForGuid(Nullable(value))
                                        | :? int64 as value -> EntityProperty.GeneratePropertyForLong(Nullable(value))
                                        | _ -> EntityProperty.CreateEntityPropertyFromObject(value)
    tableEntity

let private createInsertOperation insertMode = 
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

let private executeBatchOperation operation (table:CloudTable) entities =
    entities
    |> Seq.groupBy(fun (entity:DynamicTableEntity) -> entity.PartitionKey)
    |> Seq.collect(fun (partitionKey, entities) -> 
           entities
           |> batch 100
           |> Seq.map(fun entityBatch ->
                let batchForPartition = TableBatchOperation()
                entityBatch
                |> Seq.map operation
                |> Seq.iter batchForPartition.Add
                batchForPartition))
    |> Seq.collect table.ExecuteBatch

let deleteEntities connection tableName entities =
    let table = getTable connection tableName    
    entities
    |> Array.map buildDynamicTableEntity
    |> executeBatchOperation TableOperation.Delete table
    |> Seq.map(fun result -> result.HttpStatusCode)
    |> Seq.toArray

let deleteEntity connection tableName entity =
    deleteEntities connection tableName [| entity |] |> Seq.head

let deleteEntitiesTuple connection tableName entities =
    let table = getTable connection tableName
    entities
    |> Seq.map (fun (partitionKey, rowKey) -> DynamicTableEntity(partitionKey, rowKey, ETag = "*"))
    |> executeBatchOperation TableOperation.Delete table
    |> Seq.map(fun result -> result.HttpStatusCode)
    |> Seq.toArray
    
let insertEntity connection tableName insertMode entity = 
    let table = getTable connection tableName
    (entity
     |> buildDynamicTableEntity
     |> createInsertOperation insertMode
     |> table.Execute).HttpStatusCode

let insertEntityObjectBatch connection tableName insertMode entities = 
    let table = getTable connection tableName
    entities
    |> Seq.map(fun (partitionKey, rowKey, entity) -> 
           { PartitionKey = partitionKey
             RowKey = rowKey
             Timestamp = DateTimeOffset.MinValue
             Values = 
                 entity.GetType().GetProperties(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Instance)
                 |> Seq.map(fun prop -> prop.Name, prop.GetValue(entity, null))
                 |> Map.ofSeq })
    |> Seq.map buildDynamicTableEntity
    |> executeBatchOperation (createInsertOperation insertMode) table
    |> Seq.map(fun result -> result.HttpStatusCode)
    |> Seq.toArray

let insertEntityObject connection tableName partitionKey rowKey insertMode entity =
    insertEntityObjectBatch connection tableName insertMode [ partitionKey, rowKey, entity ]
    |> Seq.head

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
    | :? DateTime as value -> TableQuery.GenerateFilterConditionForDate(propertyName, comparison, DateTimeOffset(value))
    | :? Guid as value -> TableQuery.GenerateFilterConditionForGuid(propertyName, comparison, value)
    | _ -> TableQuery.GenerateFilterCondition(propertyName, comparison, value.ToString())

let getEntity rowKey partitionKey connection tableName = 
    let results = match partitionKey with
                  | null -> [ ("RowKey", rowKey) ]
                  | partitionKey -> 
                      [ ("RowKey", rowKey)
                        ("PartitionKey", partitionKey) ]
                  |> List.map(fun (prop, value) -> buildFilter(prop, QueryComparisons.Equal, value))
                  |> composeAllFilters
                  |> executeQuery connection tableName 0
    match results with
    | [| exactMatch |] -> Some exactMatch
    | [||] -> None
    | _ -> failwith <| sprintf "More than one row identified with the row key '%s'." rowKey

let getPartitionRows (partitionKey:string) connection tableName = 
    buildFilter("PartitionKey", QueryComparisons.Equal, partitionKey) |> executeQuery connection tableName 0
