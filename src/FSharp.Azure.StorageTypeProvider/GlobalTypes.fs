namespace FSharp.Azure.StorageTypeProvider.Table

/// The different types of insertion mechanism to use.
type TableInsertMode = 
    /// Insert if the entity does not already exist.
    | Insert = 0
    /// Insert if the entity does not already exist; otherwise overwrite the entity.
    | Upsert = 1

type TableResponse =
    | SuccessfulResponse of PartitionKey : string * RowKey : string * HttpCode : int
    | EntityError of PartitionKey : string * RowKey : string * HttpCode : int * ErrorCode : string
    | BatchOperationFailedError of PartitionKey : string * RowKey : string
    | BatchError of PartitionKey : string * RowKey : string * HttpCode : int * ErrorCode : string

