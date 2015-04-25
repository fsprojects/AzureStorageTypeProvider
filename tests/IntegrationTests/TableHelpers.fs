module FSharp.Azure.StorageTypeProvider.TableHelpers

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open System

let private getTable = 
    CloudStorageAccount
        .DevelopmentStorageAccount
        .CreateCloudTableClient()
        .GetTableReference

type RandomEntity() = 
    inherit TableEntity()
    member val Name = String.Empty with get, set
    member val YearsWorking = 0 with get, set
    member val Dob = DateTime.MinValue with get, set
    member val Salary = 0.0 with get, set
    member val IsManager = false with get, set

let insertRow (pk, rk, name, yearsWorking, dob, salary, isManager) (table:CloudTable) = 
    RandomEntity(Name = name, YearsWorking = yearsWorking, Salary = salary, Dob = dob, PartitionKey = pk, RowKey = rk.ToString(), IsManager = isManager)
    |> TableOperation.Insert
    |> table.Execute

let resetData() =
    let recreateTable (table:CloudTable) =
        if table.Exists() then table.Delete()
        table.Create()
        table

    let employeeTable = getTable "employee" |> recreateTable
    getTable "emptytable" |> recreateTable |> ignore

    employeeTable |> insertRow("men", 1, "fred", 10, DateTime(1990, 5, 1), 0., true) |> ignore
    employeeTable |> insertRow("men", 2, "fred", 35, DateTime(1980, 4, 4), 1.5, false) |> ignore
    employeeTable |> insertRow("men", 3, "tim", 99, DateTime(2001, 10, 5), 10., false) |> ignore
    employeeTable |> insertRow("women", 1, "sara", 35, DateTime(2005, 4, 30), 3.5, true) |> ignore
    employeeTable |> insertRow("women", 2, "rachel", 20, DateTime(1965, 8, 20), 5.5, false) |> ignore
