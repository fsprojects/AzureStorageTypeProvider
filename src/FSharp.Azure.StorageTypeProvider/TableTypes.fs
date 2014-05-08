namespace FSharp.Azure.StorageTypeProvider.Table

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage
open System

/// Represents a Table in Azure.
type AzureTable internal (defaultConnection, tableName) = 
    let getConnectionDetails (insertMode, connectionString) = defaultArg insertMode TableInsertMode.Insert, defaultArg connectionString defaultConnection
    let getTableForConnection = getTable tableName

    /// Inserts a batch of entities into the table, using all public properties on the object as fields.
    member x.Insert(entities, ?insertMode, ?connectionString) = 
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        let table = getTableForConnection connectionString
        let insertOp = createInsertOperation insertMode
        entities
        |> Seq.map (fun (partitionKey, rowKey, entity) -> 
               { PartitionKey = partitionKey
                 RowKey = rowKey
                 Timestamp = DateTimeOffset.MinValue
                 Values = 
                     entity.GetType().GetProperties(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Instance)
                     |> Seq.map (fun prop -> prop.Name, prop.GetValue(entity, null))
                     |> Map.ofSeq })
        |> Seq.map buildDynamicTableEntity
        |> executeBatchOperation insertOp table
    
    /// Inserts a single entity into the table, using public properties on the object as fields.
    member x.Insert(partitionKey, rowKey, entity, ?insertMode, ?connectionString) = 
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        x.Insert([ partitionKey, rowKey, entity ], insertMode, connectionString) |> Seq.head |> snd |> Seq.head

    ///Deletes a batch of entities from the table using the supplied pairs of Partition and Row keys.
    member x.Delete(entities, ?connectionString) = 
        let table = getTableForConnection (defaultArg connectionString defaultConnection)
        entities
        |> Seq.map (fun (partitionKey, rowKey) -> DynamicTableEntity(partitionKey, rowKey, ETag = "*"))
        |> executeBatchOperation TableOperation.Delete table

module TableBuilder = 
    /// Creates an Azure Table object.
    let createAzureTable connectionString tableName = AzureTable(connectionString, tableName)