///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable
open System.Linq

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

type LightweightTableEntity = 
    { PartitionKey : string
      RowKey : string
      Timestamp : System.DateTimeOffset
      Values : Map<string,obj> }

let private getTable table connection = 
    let client = getTableClient connection
    client.GetTableReference table

/// Gets all tables
let getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map (fun table -> table.Name)

let getRowsForSchema rowCount connection tableName = 
    let table = getTable tableName connection
    let query = table.CreateQuery<DynamicTableEntity>()
    query.TakeCount <- System.Nullable<int>(rowCount)
    query.Execute()
    |> Seq.take rowCount
    |> Seq.toArray

let executeQuery (query:IQueryable<DynamicTableEntity>) =
    query
    |> Seq.toList
    |> Seq.map(fun dte -> { PartitionKey = dte.PartitionKey
                            RowKey = dte.RowKey
                            Timestamp = dte.Timestamp
                            Values = dte.Properties |> Seq.map(fun p -> p.Key, p.Value.PropertyAsObject) |> Map.ofSeq })
    |> Seq.toArray    

let getEntity connection tableName entityKey partitionKey =
    let table = getTable tableName connection
    let entityQuery = query { for row in table.CreateQuery<DynamicTableEntity>() do
                              where (row.RowKey = entityKey) }
    match partitionKey with
    | null -> entityQuery
    | partitionKey -> query { for row in entityQuery do
                              where (row.PartitionKey = partitionKey) }
    |> executeQuery
    |> Seq.tryFind (fun x -> true)

let getRows connection tableName partitionKey = 
    let table = getTable tableName connection
    query { for row in table.CreateQuery<DynamicTableEntity>() do
            where (row.PartitionKey = partitionKey) }
    |> executeQuery