namespace FSharp.Azure.StorageTypeProvider.Table

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open System

/// Represents a Table in Azure.
type AzureTable internal (defaultConnection, tableName) = 
    let getConnectionDetails (insertMode, connectionString) = 
        defaultArg insertMode TableInsertMode.Insert, defaultArg connectionString defaultConnection
    let getTableForConnection = getTable tableName
    
    /// Inserts a batch of entities into the table, using all public properties on the object as fields.
    member x.Insert(entities : seq<Partition * Row * 'b>, ?insertMode, ?connectionString) = 
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        let table = getTableForConnection connectionString
        let insertOp = createInsertOperation insertMode
        
        let propBuilders = 
            typeof<'b>.GetProperties(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Instance)
            |> Seq.map (fun prop entity -> prop.Name, prop.GetValue(entity, null))
            |> Seq.toArray
        entities
        |> Seq.map (fun (partitionKey, rowKey, entity) -> 
               LightweightTableEntity(partitionKey, rowKey, DateTimeOffset.MinValue, 
                                      propBuilders
                                      |> Seq.map (fun builder -> builder (entity))
                                      |> Map.ofSeq))
        |> Seq.map buildDynamicTableEntity
        |> executeBatchOperation insertOp table
    
    /// Inserts a single entity into the table, using public properties on the object as fields.
    member x.Insert(partitionKey, rowKey, entity, ?insertMode, ?connectionString) = 
        let insertMode, connectionString = getConnectionDetails (insertMode, connectionString)
        x.Insert([ partitionKey, rowKey, entity ], insertMode, connectionString)
        |> Seq.head
        |> snd
        |> Seq.head
    
    /// Deletes a batch of entities from the table using the supplied pairs of Partition and Row keys.
    member x.Delete(entities, ?connectionString) = 
        let table = getTableForConnection (defaultArg connectionString defaultConnection)
        entities
        |> Seq.map (fun entityId -> 
               let Partition(partitionKey), Row(rowKey) = entityId
               DynamicTableEntity(partitionKey, rowKey, ETag = "*"))
        |> executeBatchOperation TableOperation.Delete table
    
    /// Gets the name of the table.
    member __.Name = tableName

module TableBuilder = 
    /// Creates an Azure Table object.
    let createAzureTable connectionString tableName = AzureTable(connectionString, tableName)
