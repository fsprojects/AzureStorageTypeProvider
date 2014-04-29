namespace FSharp.Azure.StorageTypeProvider.Types

open FSharp.Azure.StorageTypeProvider.Repositories.TableRepository
open FSharp.Azure.StorageTypeProvider
open Microsoft.WindowsAzure.Storage.Table
open System

/// Represents a Table in Azure.
type AzureTable internal (defaultConnection, tableName) =
    let getConnectionDetails(insertMode, connectionString) =
        defaultArg insertMode TableInsertMode.Insert,
        defaultArg connectionString defaultConnection

    /// Inserts a batch of entities into the table, using all public properties on the object as fields.
    member x.Insert(entities, ?insertMode, ?connectionString) =
        let insertMode, connectionString = getConnectionDetails(insertMode, connectionString)
        let table = getTable connectionString tableName
        let insertOp = createInsertOperation insertMode

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
        |> executeBatchOperation insertOp table
        |> Seq.map(fun result -> result.HttpStatusCode)
        |> Seq.toArray

    /// Inserts a single entity into the table, using public properties on the object as fields.
    member x.Insert(partitionKey, rowKey, entity, ?insertMode, ?connectionString) =
        let insertMode, connectionString = getConnectionDetails(insertMode, connectionString)
        x.Insert([ partitionKey, rowKey, entity ], insertMode, connectionString)
        |> Seq.head

/// Represents a table in Azure which has some existing data in it from which a schema can be inferred for querying.
type QueryableAzureTable internal (defaultConnection, tableName) =
    inherit AzureTable(defaultConnection, tableName)

    ///Deletes a batch of entities from the table using the supplied pairs of Partition and Row keys.
    member x.Delete(entities, ?connectionString) =
        let connectionString = defaultArg connectionString defaultConnection
        let table = getTable connectionString tableName
        entities
        |> Seq.map (fun (partitionKey, rowKey) -> DynamicTableEntity(partitionKey, rowKey, ETag = "*"))
        |> executeBatchOperation TableOperation.Delete table
        |> Seq.map(fun result -> result.HttpStatusCode)
        |> Seq.toArray

module TableBuilder = 
    /// Creates an Azure Table object.
    let createAzureTable(connectionString,tableName) = AzureTable(connectionString, tableName)
    
    /// Creates a Queryable Azure Table.
    let createQueryableAzureTable(connectionString,tableName) = QueryableAzureTable(connectionString, tableName)