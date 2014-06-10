module FSharp.Azure.StorageTypeProvider.SampleTableTypes

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage.Table
open System

let private tableClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudTableClient()
let private table = tableClient.GetTableReference("tptest")

type RandomEntity() = 
    inherit TableEntity()
    member val Name = String.Empty with get, set
    member val Count = 0 with get, set
    member val Dob = DateTime.MinValue with get, set

let insertRow (name, count, dob) = 
    RandomEntity(Name = name, Count = count, Dob = dob, PartitionKey = name, RowKey = count.ToString())
    |> TableOperation.Insert
    |> table.Execute

let resetData() =
    if table.Exists() then table.Delete()
    table.Create()

    insertRow("fred", 10, DateTime(1990, 5, 1)) |> ignore
    insertRow("fred", 35, DateTime(1980, 4, 4)) |> ignore
    insertRow("tim", 99, DateTime(2001, 10, 5)) |> ignore
    insertRow("sara", 35, DateTime(2005, 4, 30)) |> ignore
    insertRow("rachel", 20, DateTime(1965, 8, 20)) |> ignore
