module FSharp.Azure.StorageTypeProvider.TableHelpers

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open System

let private getTable = 
    CloudStorageAccount
        .DevelopmentStorageAccount
        .CreateCloudTableClient()
        .GetTableReference

let bArr = [| for i in 1 .. 255 do yield i |> byte |]

type LargeEntity() = 
//20 byte array properties with a max size of 64kb each give this entity type a maz size of c. 1.3Mb
    inherit TableEntity()
    member val ByteArr1 = bArr with get, set
    member val ByteArr2 = bArr with get, set
    member val ByteArr3 = bArr with get, set
    member val ByteArr4 = bArr with get, set
    member val ByteArr5 = bArr with get, set
    member val ByteArr6 = bArr with get, set
    member val ByteArr7 = bArr with get, set
    member val ByteArr9 = bArr with get, set
    member val ByteArr10 = bArr with get, set
    member val ByteArr11= bArr with get, set
    member val ByteArr12 = bArr with get, set
    member val ByteArr13 = bArr with get, set
    member val ByteArr14 = bArr with get, set
    member val ByteArr15 = bArr with get, set
    member val ByteArr16 = bArr with get, set
    member val ByteArr17 = bArr with get, set
    member val ByteArr18 = bArr with get, set
    member val ByteArr19 = bArr with get, set
    member val ByteArr20 = bArr with get, set

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

let insertLargeRow (pk, rk)(table:CloudTable) =
    LargeEntity(PartitionKey = pk, RowKey = rk)
    |> TableOperation.Insert
    |> table.Execute

let resetData() =
    let recreateTable (table:CloudTable) =
        if table.Exists() then table.Delete()
        table.Create()
        table

    let lgeTable = getTable "large" |> recreateTable
    lgeTable |> insertLargeRow("1","1") |> ignore
    lgeTable |> insertLargeRow("1","2") |> ignore
    lgeTable |> insertLargeRow("2","1") |> ignore

    let employeeTable = getTable "employee" |> recreateTable
    getTable "emptytable" |> recreateTable |> ignore

    employeeTable |> insertRow("men", 1, "fred", 10, DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc), 0., true) |> ignore
    employeeTable |> insertRow("men", 2, "fred", 35, DateTime(1980, 4, 4, 0, 0, 0, DateTimeKind.Utc), 1.5, false) |> ignore
    employeeTable |> insertRow("men", 3, "tim", 99, DateTime(2001, 10, 5, 0, 0, 0, DateTimeKind.Utc), 10., false) |> ignore
    employeeTable |> insertRow("women", 1, "sara", 35, DateTime(2005, 4, 30, 0, 0, 0, DateTimeKind.Utc), 3.5, true) |> ignore
    employeeTable |> insertRow("women", 2, "rachel", 20, DateTime(1965, 8, 20, 0, 0, 0, DateTimeKind.Utc), 5.5, false) |> ignore
