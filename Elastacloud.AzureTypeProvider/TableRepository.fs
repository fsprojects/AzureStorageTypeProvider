///Contains helper functions for accessing tables
module Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

open Microsoft.WindowsAzure.Storage

let private getTableClient connection = CloudStorageAccount.Parse(connection).CreateCloudTableClient()

let getTables connection =
    let client = getTableClient connection
    client.ListTables()
    |> Seq.map(fun table -> table.Name)