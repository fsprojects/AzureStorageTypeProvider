namespace Elastacloud.FSharp.AzureTypeProvider

/// The different types of insertion mechanism to use.
type TableInsertMode =
/// Insert if the entity does not already exist.
| Insert = 0
/// Insert if the entity does not already exist; otherwise overwrite with this entity.
| InsertOrReplace = 1