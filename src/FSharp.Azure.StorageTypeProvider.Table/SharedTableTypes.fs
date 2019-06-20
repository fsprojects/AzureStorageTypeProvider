namespace FSharp.Azure.StorageTypeProvider.Table

open Microsoft.Azure.Cosmos.Table
open System

/// The different types of insertion mechanism to use.
type TableInsertMode = 
    /// Insert if the entity does not already exist.
    | Insert = 0
    /// Insert if the entity does not already exist; otherwise overwrite the entity.
    | Upsert = 1


/// The type of property
type internal PropertyNeed =
    /// The property is optional.
    | Optional
    /// The property is mandatory.
    | Mandatory

type internal ColumnDefinition = { Name : string; ColumnType : EdmType; PropertyNeed : PropertyNeed }

/// The name of the partition.
type Partition = | Partition of string
/// The row key.
type Row = | Row of string
/// Represents a Partition and Row combined to key a single entity.
type EntityId = Partition * Row

/// Different responses from a table operation.
type TableResponse =
    /// The operation was successful.
    | SuccessfulResponse of EntityId * HttpCode : int
    /// The operation for this specific entity failed.
    | EntityError of EntityId * HttpCode : int * ErrorCode : string
    /// The operation for this specific entity was not carried out because an operation for another entity in the same batch failed.
    | BatchOperationFailedError of EntityId
    /// An unknown error occurred in this batch.
    | BatchError of EntityId * HttpCode : int * ErrorCode : string

/// Represents a single table entity.
type LightweightTableEntity
    internal (partitionKey:Partition, rowKey:Row, timestamp:DateTimeOffset, values:Map<string,obj>) =

    let (Partition pkey) = partitionKey
    let (Row rkey) = rowKey

    /// The Partition Key of the entity.
    member __.PartitionKey with get () = pkey
    /// The Row Key of the entity.
    member __.RowKey with get () = rkey
    /// The timestamp of the entity.
    member __.Timestamp with get () = timestamp
    /// A collection of key/value pairs of all other properties on this entity.
    member __.Values with get () = values