///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable
open System
open System.Linq

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

type LightweightTableEntity = 
    { PartitionKey : string
      RowKey : string
      Timestamp : System.DateTimeOffset
      Values : Map<string,obj> }

let private getTable connection tableName = 
    let client = getTableClient connection
    client.GetTableReference tableName

/// Gets all tables
let getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map (fun table -> table.Name)

type DynamicQuery = TableQuery<DynamicTableEntity>

let getRowsForSchema (rowCount:int) connection tableName = 
    let table = getTable connection tableName
    table.ExecuteQuery(DynamicQuery().Take(Nullable<_>(rowCount)))
    |> Seq.take rowCount
    |> Seq.toArray

let executeWeakQuery queryPairs connection tableName =
    let filterString = (None, queryPairs)
                       ||> Seq.fold(fun state (key,value) ->
                                   let newQuery = DynamicQuery.GenerateFilterCondition(key, QueryComparisons.Equal, value)
                                   match state with
                                   | Some filter -> Some <| DynamicQuery.CombineFilters(filter, TableOperators.And, newQuery)
                                   | None -> Some newQuery)

    match filterString with
    | Some filterString -> (getTable connection tableName).ExecuteQuery(DynamicQuery().Where(filterString))
                           |> Seq.map(fun dte -> { PartitionKey = dte.PartitionKey
                                                   RowKey = dte.RowKey
                                                   Timestamp = dte.Timestamp
                                                   Values = dte.Properties |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject) |> Map.ofSeq })
                           |> Seq.toArray
    | None -> failwith "no query pairs supplied to build a filter"

let getEntity entityKey partitionKey connection tableName =
    let queryParts = match partitionKey with
                     | null -> [("RowKey", entityKey)]
                     | partitionKey -> [("RowKey", entityKey); ("PartitionKey", partitionKey)]

    executeWeakQuery queryParts connection tableName
    |> Seq.tryFind (fun x -> true)

let getPartitionRows partitionKey connection tableName = 
    executeWeakQuery [("PartitionKey", partitionKey)] connection tableName