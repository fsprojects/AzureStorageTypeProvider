///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage.Table.Queryable

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

let private getTable table connection = 
    let client = getTableClient connection
    client.GetTableReference table

/// Gets all tables
let getTables connection = 
    let client = getTableClient connection
    client.ListTables() |> Seq.map (fun table -> table.Name)

/// Gets the top n rows from a table
let getTopRows rowCount tableName connection = 
    try 
        let table = getTable tableName connection
        
//        let tableQuery = 
//            query { 
//                for row in table.CreateQuery<DynamicTableEntity>() do
//                    take rowCount
//                    select row
//            }
        table.CreateQuery<DynamicTableEntity>()
        |> Seq.map (fun row -> 
                    row.PartitionKey, row.RowKey, row.Timestamp,
                    row.Properties
                    |> Seq.filter (fun p -> p.Key <> "Token")
                    |> Seq.map (fun p -> p.Key, p.Value.PropertyAsObject)
                    |> Seq.toList)
        |> Seq.toList
    with ex -> 
        [ "Exception", "0", System.DateTimeOffset(), 
          [ "Type", ex.GetType().FullName :> obj
            "Message", ex.Message :> obj ] ]