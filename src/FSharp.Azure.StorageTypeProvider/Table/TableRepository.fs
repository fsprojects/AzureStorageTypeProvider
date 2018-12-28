///[omit]
///Contains helper functions for accessing tables
module FSharp.Azure.StorageTypeProvider.Table.TableRepository

open FSharp.Azure.StorageTypeProvider.Table
open FSharp.Azure.StorageTypeProvider
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
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
        | EdmType.DateTime -> 2
        | EdmType.Binary -> 64
        | EdmType.Boolean -> 1
        | EdmType.Double -> 2
        | EdmType.Guid -> 4
        | EdmType.Int32 -> 1
        | EdmType.Int64 -> 1
        | EdmType.String -> 64
        | unknown -> failwith (sprintf "Unknown EdmType %A" unknown)

    /// Calculates the maximum size of a given entity. 
    let private getMaxEntitySize (entity:DynamicTableEntity) =
        let entityRowSize = entity.Properties.Values |> Seq.sumBy getMaxPropertySize
        basicRowSize + entityRowSize
        
    let private maximumBatchSizeKb = 4000

    /// Calculates the maximum number of entities of a given type that can be inserted in a single batch.
    let getBatchSize entity = maximumBatchSizeKb / (getMaxEntitySize entity)

let internal getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

let private boxedNone = box None
let buildTableEntity partitionKey rowKey names (values: obj []) =
    let properties =
        Seq.zip names values
        |> Seq.choose(fun (name, value) ->
            match value with
            | value when value = boxedNone -> None
            | :? Option<byte []> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<string> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<int> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<bool> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<DateTime> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<double> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<Guid> as option -> option |> Option.map(fun value -> name, box value)
            | :? Option<int64> as option -> option |> Option.map(fun value -> name, box value)
            | value -> Some(name, value))
        |> Map.ofSeq

    LightweightTableEntity(partitionKey, rowKey, DateTimeOffset.MinValue, properties)

let internal getTable tableName connection = 
    let client = getTableClient connection
    client.GetTableReference tableName

type private DynamicQuery = DynamicTableEntity TableQuery

[<AutoOpen>]
module private SdkExtensions =
    type CloudTableClient with
        member cloudTableClient.ListTablesAsync() =
            let getTables token = async {
                let! result = cloudTableClient.ListTablesSegmentedAsync token |> Async.AwaitTask
                return result.ContinuationToken, result.Results }
            Async.segmentedAzureOperation getTables

    type CloudTable with
        member cloudTable.ExecuteQueryAsync(query:DynamicQuery) =
            let mutable remainingRows = query.TakeCount |> Option.ofNullable
            let doQuery token = async {
                let! query = cloudTable.ExecuteQuerySegmentedAsync(query, token) |> Async.AwaitTask
                remainingRows <- remainingRows |> Option.map(fun remainingRows -> remainingRows - query.Results.Count)
                let token = match remainingRows with Some x when x <= 0 -> null | None | Some _ -> query.ContinuationToken
                return token, query.Results }
            Async.segmentedAzureOperation doQuery            

/// Gets all tables
let internal getTables connection = async {
    let client = getTableClient connection
    let! results = client.ListTablesAsync()
    return results |> Array.map(fun table -> table.Name) }

let internal getMetricsTables connection =
    let services = [ "Blob"; "Queue"; "Table"; "File" ]
    let locations = [ "Primary"; "Secondary" ]
    let periods = [ "Hourly", "Hour"; "Per Minute", "Minute" ]

    let client = getTableClient connection
    seq {
        for (description, period) in periods do
            for location in locations do
                for service in services do
                    let tableName = sprintf "$Metrics%s%sTransactions%s" period location service
                    if (client.GetTableReference(tableName).ExistsAsync().Result) then
                        yield description, location, service, tableName }

let internal getRowsForSchema (rowCount: int) connection tableName = async {
    let table = getTable tableName connection
    let! results = table.ExecuteQueryAsync(DynamicQuery().Take(Nullable rowCount))
    return results |> Array.truncate rowCount }

let toLightweightTableEntity (dte:DynamicTableEntity) = 
    LightweightTableEntity(
        Partition dte.PartitionKey,
        Row dte.RowKey,
        dte.Timestamp,
        dte.Properties
        |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject)
        |> Map.ofSeq)

let executeGenericQueryAsync connection tableName maxResults filterString mapToReturnEntity = async {
    let query =
        let query = DynamicQuery().Where(filterString)
        if maxResults > 0 then query.Take(Nullable maxResults) else query
    let table = getTable tableName connection
    let! output = table.ExecuteQueryAsync query
    return output |> Array.map mapToReturnEntity }

let executeQueryAsync connection tableName maxResults filterString = 
    executeGenericQueryAsync connection tableName maxResults filterString toLightweightTableEntity

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
        | [] -> if List.isEmpty currentBatch then output else currentBatch::output |> List.rev
        | theList when counter = size -> doBatch ((currentBatch |> List.rev) ::output) [] 0 theList
        | head::tail -> doBatch output (head::currentBatch) (counter + 1) tail
    doBatch [] [] 0 (source |> Seq.toList)

let private splitIntoBatches createTableOp entities =
    match entities with
    | entities when Seq.isEmpty entities -> Seq.empty
    | entities -> 
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

let private processErrorResp entityBatch buildEntityId (ex:StorageException) =
    let requestResult = ex.RequestInformation
    match requestResult.ExtendedErrorInformation.ErrorMessage.Split('\n').[0].Split(':') with
    | [|index; _|] ->
        match Int32.TryParse(index) with
        | true, index ->
            entityBatch
            |> Seq.mapi(fun entityIndex entity ->
                if entityIndex = index then EntityError(buildEntityId entity, requestResult.HttpStatusCode, requestResult.ExtendedErrorInformation.ErrorCode)
                else BatchOperationFailedError(buildEntityId entity))
        | _ -> entityBatch |> Seq.map(fun entity -> BatchError(buildEntityId entity, requestResult.HttpStatusCode, requestResult.ExtendedErrorInformation.ErrorCode))
    | [| _ |] -> entityBatch |> Seq.map(fun entity -> EntityError(buildEntityId entity, requestResult.HttpStatusCode, requestResult.ExtendedErrorInformation.ErrorCode))
    | _ -> entityBatch |> Seq.map(fun entity -> BatchError(buildEntityId entity, requestResult.HttpStatusCode, requestResult.ExtendedErrorInformation.ErrorCode))

let internal executeBatchAsynchronously batchOp entityBatch buildEntityId (table:CloudTable) =
    batchOp
    |> table.ExecuteBatchAsync
    |> Async.AwaitTask
    |> Async.toAsyncResult
    |> Async.map(function
    | Ok reponse -> 
        reponse
        |> Seq.zip entityBatch
        |> Seq.map(fun (entity, res) -> SuccessfulResponse(buildEntityId entity, res.HttpStatusCode))
    | Error ([ :? StorageException as ex ]) -> processErrorResp entityBatch buildEntityId ex
    | Error [] -> failwith "An unknown error occurred."
    | Error (topException :: _) -> raise topException)

let internal executeBatchOperationAsync createTableOp (table:CloudTable) entities = async {
    return!
        splitIntoBatches createTableOp entities
        |> Seq.map(fun (partitionKey, entityBatch, batchOperation) -> async{
            let buildEntityId (entity:DynamicTableEntity) = Partition(entity.PartitionKey), Row(entity.RowKey)
            let! responses = executeBatchAsynchronously batchOperation entityBatch buildEntityId table
            return (partitionKey, responses |> Seq.toArray) })
        |> Async.Parallel }

let deleteEntities connection tableName entities =
    let table = getTable tableName connection
    entities
    |> Array.map buildDynamicTableEntity
    |> executeBatchOperationAsync TableOperation.Delete table

let deleteEntitiesAsync connection tableName entities = async {
    let table = getTable tableName connection
    return! 
        entities
        |> Array.map buildDynamicTableEntity
        |> executeBatchOperationAsync TableOperation.Delete table }

let deleteEntityAsync connection tableName entity = async {
    let! resp = deleteEntitiesAsync connection tableName [| entity |] 
    return resp |> Seq.head |> snd |> Seq.head }

let insertEntityBatchAsync connection tableName insertMode entities = async {
    let table = getTable tableName connection
    let insertOp = createInsertOperation insertMode
    return! 
        entities
        |> Seq.map buildDynamicTableEntity
        |> executeBatchOperationAsync insertOp table }

let insertEntityAsync connection tableName insertMode entity = async {
    let! resp = insertEntityBatchAsync connection tableName insertMode [entity] 
    return resp |> Seq.head |> snd |> Seq.head }

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

let buildGetEntityQry rowKey partitionKey = 
    let (Row rowKey, Partition partitionKey) = rowKey, partitionKey
    [ ("RowKey", rowKey); ("PartitionKey", partitionKey) ]
    |> List.map(fun (prop, value) -> buildFilter(prop, QueryComparisons.Equal, value))
    |> composeAllFilters

let parseGetEntityResults results = 
    match results with
    | [| exactMatch |] -> Some exactMatch
    | _ -> None

let getEntityAsync rowKey partitionKey connection tableName = async {
    let! results =
        buildGetEntityQry rowKey partitionKey
        |> executeQueryAsync connection tableName 0
    return results |> parseGetEntityResults }

let getPartitionRowsAsync (partitionKey:string) connection tableName = async {
    return!
        buildFilter("PartitionKey", QueryComparisons.Equal, partitionKey)
        |> executeQueryAsync connection tableName 0 } 