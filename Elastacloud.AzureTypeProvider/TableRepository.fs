///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

type LightweightTableEntity = 
    { PartitionKey : string
      RowKey : string
      Timestamp : System.DateTimeOffset
      Values : Map<string,EntityProperty> }

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

let getRows connection tableName partitionKey = 
        let table = getTable tableName connection
        query { for row in table.CreateQuery<DynamicTableEntity>() do
                where (row.PartitionKey = partitionKey) }
        |> Seq.toList
        |> Seq.map(fun dte -> { PartitionKey = dte.PartitionKey
                                RowKey = dte.RowKey
                                Timestamp = dte.Timestamp
                                Values = dte.Properties |> Seq.map(fun p -> p.Key, p.Value) |> Map.ofSeq })
        |> Seq.toArray