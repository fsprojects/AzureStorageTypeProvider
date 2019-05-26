namespace FSharp.Azure.StorageTypeProvider.Table

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.Azure.Cosmos
open Microsoft.Azure.Cosmos.Table
open System
open System.Reflection

/// Represents a Table in Azure.
type AzureTable internal (defaultConnection, tableName) = 
    let getConnectionDetails (insertMode, connectionString) = 
        defaultArg insertMode TableInsertMode.Insert, defaultArg connectionString defaultConnection
    let getTableForConnection = getTable tableName

    let safeGetOption name = function | Some value -> Some(name, box value) | _ -> None

    let buildInsertParams insertMode connectionString (entities : seq<Partition * Row * 'b>) = 
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        let table = getTableForConnection connectionString
        let insertOp = createInsertOperation insertMode

        let propBuilders = 
            typeof<'b>.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
            |> Seq.map (fun prop entity -> prop.Name, prop.GetValue(entity, null))
            |> Seq.toArray

        let tblEntities = 
            entities
            |> Seq.map (fun (partitionKey, rowKey, entity) -> 
                let values =
                    propBuilders
                    |> Seq.map (fun builder -> builder entity)
                    |> Seq.choose(fun (name, value) ->
                        match value with
                        | :? Option<(byte [])> as value -> safeGetOption name value
                        | :? Option<string> as value -> safeGetOption name value
                        | :? Option<int> as value -> safeGetOption name value
                        | :? Option<bool> as value -> safeGetOption name value
                        | :? Option<DateTime> as value -> safeGetOption name value
                        | :? Option<double> as value -> safeGetOption name value
                        | :? Option<Guid> as value -> safeGetOption name value
                        | :? Option<int64> as value -> safeGetOption name value
                        | _ -> Some (name, value))
                    |> Map.ofSeq
                LightweightTableEntity(partitionKey, rowKey, DateTimeOffset.MinValue, values) |> buildDynamicTableEntity)
        tblEntities, insertOp, table
    
    let getSinglePartitionResult partitionKey = function
        | [| partition |] -> partition
        | _ -> partitionKey, [||]

    /// Gets a handle to the Azure SDK client for this table.
    member __.AsCloudTable(?connectionString) = getTableForConnection (defaultArg connectionString defaultConnection)

    /// Inserts a batch of entities into the table, using all public properties on the object as fields.
    member this.Insert(entities : seq<Partition * Row * _>, ?insertMode, ?connectionString) =
        match insertMode, connectionString with
        | Some insertMode, Some connectionString -> this.InsertAsync(entities, insertMode, connectionString)
        | Some insertMode, None -> this.InsertAsync(entities, insertMode)
        | None, Some connectionString -> this.InsertAsync(entities, connectionString = connectionString)
        | None, None -> this.InsertAsync(entities)
        |> Async.RunSynchronously

    /// Inserts a batch of entities into the table, using all public properties on the object as fields.
    member __.InsertAsync(entities : seq<Partition * Row * _>, ?insertMode, ?connectionString) = async {
        let tblEntities, insertOp, table  = buildInsertParams insertMode connectionString entities
        return! tblEntities |> executeBatchOperationAsync insertOp table }
    
    /// Inserts a single entity into the table, using public properties on the object as fields.
    member this.Insert(partitionKey, rowKey, entity, ?insertMode, ?connectionString) = 
        match insertMode, connectionString with
        | Some insertMode, Some connectionString -> this.InsertAsync(partitionKey, rowKey, entity, insertMode, connectionString)
        | Some insertMode, None -> this.InsertAsync(partitionKey, rowKey, entity, insertMode)
        | None, Some connectionString -> this.InsertAsync(partitionKey, rowKey, entity, connectionString = connectionString)
        | None, None -> this.InsertAsync(partitionKey, rowKey, entity)
        |> Async.RunSynchronously

    /// Inserts a single entity into the table asynchronously, using public properties on the object as fields.
    member this.InsertAsync(partitionKey, rowKey, entity, ?insertMode, ?connectionString) = async {
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        let! insertRes = this.InsertAsync([ partitionKey, rowKey, entity ], insertMode, connectionString)
        return
            insertRes
            |> Seq.head
            |> snd
            |> Seq.head }
    
    /// Deletes a batch of entities from the table using the supplied pairs of Partition and Row keys.  
    member this.Delete(entities, ?connectionString) =
        match connectionString with
        | Some connectionString -> this.DeleteAsync(entities, connectionString)
        | None -> this.DeleteAsync entities
        |> Async.RunSynchronously

    /// Asynchronously deletes a batch of entities from the table using the supplied pairs of Partition and Row keys.
    member __.DeleteAsync(entities, ?connectionString) = async {
        let table = getTableForConnection (defaultArg connectionString defaultConnection)
        return! entities
        |> Seq.map (fun entityId -> 
            let Partition(partitionKey), Row(rowKey) = entityId
            DynamicTableEntity(partitionKey, rowKey, ETag = "*"))
        |> executeBatchOperationAsync TableOperation.Delete table }
    
    /// Deletes an entire partition from the table
    member this.DeletePartition(partitionKey, ?connectionString) = 
        match connectionString with
        | Some connectionString -> this.DeletePartitionAsync(partitionKey, connectionString)
        | None -> this.DeletePartitionAsync(partitionKey)
        |> Async.RunSynchronously
 
    /// Asynchronously deletes an entire partition from the table
    member __.DeletePartitionAsync(partitionKey, ?connectionString) = async {
        let table = getTableForConnection (defaultArg connectionString defaultConnection)
        let connectionString = defaultArg connectionString defaultConnection
        let filter = Table.TableQuery.GenerateFilterCondition ("PartitionKey", Table.QueryComparisons.Equal, partitionKey)
        let! queryResponse = executeGenericQueryAsync connectionString table.Name Int32.MaxValue filter (fun e -> (Partition e.PartitionKey, Row e.RowKey))
        let! deleteResponse = queryResponse |> __.DeleteAsync
        return deleteResponse |> getSinglePartitionResult partitionKey }
    
    /// Gets the name of the table.
    member __.Name = tableName

/// [omit]
module TableBuilder = 
    /// Creates an Azure Table object.
    let createAzureTable connectionString tableName = AzureTable(connectionString, tableName)
    let createAzureTableRoot connectionString = TableRepository.getTableClient connectionString