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
let internal getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map (fun table -> table.Name)

type DynamicQuery = TableQuery<DynamicTableEntity>

let internal getRowsForSchema (rowCount:int) connection tableName = 
    let table = getTable connection tableName
    table.ExecuteQuery(DynamicQuery().Take(Nullable<_>(rowCount)))
    |> Seq.truncate rowCount
    |> Seq.toArray

let executeQuery connection tableName filterString =
    (getTable connection tableName).ExecuteQuery(DynamicQuery()
                                   .Where(filterString))
                                   |> Seq.map(fun dte -> { PartitionKey = dte.PartitionKey
                                                           RowKey = dte.RowKey
                                                           Timestamp = dte.Timestamp
                                                           Values = dte.Properties |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject) |> Map.ofSeq })
                                   |> Seq.toArray    

let composeAllFilters filters =
    match filters with
    | [] -> String.Empty
    | _ -> filters |> List.rev |> List.reduce(fun acc filter -> TableQuery.CombineFilters(acc, TableOperators.And, filter))                    

let buildFilter(propertyName,comparison,value) =
    match box value with
    | :? string as value -> TableQuery.GenerateFilterCondition(propertyName, comparison, value)
    | :? int as value -> TableQuery.GenerateFilterConditionForInt(propertyName, comparison, value)
    | :? int64 as value -> TableQuery.GenerateFilterConditionForLong(propertyName, comparison, value)
    | :? float as value -> TableQuery.GenerateFilterConditionForDouble(propertyName, comparison, value)
    | :? bool as value -> TableQuery.GenerateFilterConditionForBool(propertyName, comparison, value)
    | :? DateTime as value -> TableQuery.GenerateFilterConditionForDate(propertyName, comparison, DateTimeOffset(value))
    | :? Guid as value -> TableQuery.GenerateFilterConditionForGuid(propertyName, comparison, value)
    | _ -> TableQuery.GenerateFilterCondition(propertyName, comparison, value.ToString())

let getEntity entityKey partitionKey connection tableName =
    match partitionKey with
    | null -> [("RowKey", entityKey)]
    | partitionKey -> [("RowKey", entityKey); ("PartitionKey", partitionKey)]
    |> List.map(fun (prop,value) -> buildFilter(prop, QueryComparisons.Equal, value))
    |> composeAllFilters
    |> executeQuery connection tableName
    |> Seq.tryFind (fun x -> true)

let getPartitionRows partitionKey connection tableName = 
    buildFilter("PartitionKey", QueryComparisons.Equal, partitionKey)
    |> executeQuery connection tableName