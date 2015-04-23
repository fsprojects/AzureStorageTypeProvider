module FSharp.Azure.StorageTypeProvider.``Table Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table
open Microsoft.WindowsAzure.Storage.Table
open Swensen.Unquote
open System
open System.Linq
open Xunit

type Local = AzureTypeProvider<"DevStorageAccount", "">

let table = Local.Tables.tptest

type ResetTableDataAttribute() =
    inherit BeforeAfterTestAttribute()
    override x.Before(methodUnderTest) = TableHelpers.resetData()
    override x.After(methodUnderTest) = TableHelpers.resetData()

[<Fact>]
let ``Correctly identifies tables``() =
    // compiles!
    Local.Tables.tptest

[<Fact>]
let ``Table name is correctly identified``() =
    table.Name =? "tptest"

[<Fact>]
let ``Matching row and partition key returns Some row``() =
    match table.Get(Row "35", Partition "fred") with
    | Some row -> row.Name =? "fred"
                  row.Count =? 35
                  row.Dob =? DateTime(1980, 4, 4)
    | None -> failwith "could not locate row"

[<Fact>]
let ``Non matching partition key returns None``() =
    match table.Get(Row "35", Partition "random") with
    | Some _ -> failwith "located a row that shouldn't have"
    | None -> ()

[<Fact>]
let ``Non matching row key returns None``() =
    match table.Get(Row "random", Partition "fred") with
    | Some _ -> failwith "located a row that shouldn't have"
    | None -> ()

[<Fact>]
let ``Gets all rows in a table``() =
    table.Query().Execute().Length =? 5

[<Fact>]
let ``Gets all rows in a partition``() =
    table.GetPartition("fred").Length =? 2

type MatchingTableRow =
    { Name : string
      Count : int 
      Dob : DateTime }

[<Fact>]
[<ResetTableData>]
let ``Inserts and deletes a row using lightweight syntax correctly``() =
    let result = table.Insert(Partition "isaac", Row "500", { Name = "isaac"; Count = 500; Dob = DateTime.Now })
    match result with
    | SuccessfulResponse ((Partition "isaac", Row "500"), 204) ->
        let deleteResponse = table.Delete([ Partition "isaac", Row "500"] )
        match deleteResponse with
        | [| "isaac", [| SuccessfulResponse((Partition "isaac", Row "500"), 204) |] |] -> ()
        | _ -> failwith "error deleting"
    | _ -> failwith "error inserting"
    
[<Fact>]
[<ResetTableData>]
let ``Inserts and deletes a batch on same partition using lightweight syntax correctly``() =
    let result = table.Insert( [ Partition "isaac", Row "500", { Name = "isaac"; Count = 500; Dob = DateTime.Now }
                                 Partition "isaac", Row "250", { Name = "isaac"; Count = 250; Dob = DateTime.Now }
                               ])
    match result with
    | [| "isaac", [| SuccessfulResponse _; SuccessfulResponse _ |] |] ->
        let deleteResponse = table.Delete([ Partition "isaac", Row "500"; Partition "isaac", Row "250" ] )
        match deleteResponse with
        | [| "isaac", [| SuccessfulResponse _; SuccessfulResponse _ |] |] -> ()
        | _ -> failwith "error deleting"
    | _ -> failwith "error inserting"

[<Fact>]
[<ResetTableData>]
let ``Updates an existing row``() =
    table.Insert(Partition "fred", Row "35", { Name = "fred"; Count = 35; Dob = DateTime.MaxValue }, TableInsertMode.Upsert) |> ignore
    table.Get(Row "35", Partition "fred").Value.Dob =? DateTime.MaxValue

[<Fact>]
[<ResetTableData>]
let ``Inserting an existing row returns an error``() =
    let result = table.Insert(Partition "fred", Row "35", { Name = "fred"; Count = 35; Dob = DateTime.MaxValue })
    match result with
    | EntityError((Partition "fred", Row "35"), 409, "EntityAlreadyExists") -> ()
    | _ -> failwith "Should have failed to insert"

[<Fact>]
[<ResetTableData>]
let ``Inserts a row using provided types correctly``() =
    table.Insert(Local.Domain.tptestEntity(Partition "sample", Row "x", 1, DateTime.MaxValue, "Hello", 6.1)) |> ignore
    let result = table.Get(Row "x", Partition "sample").Value
    result.PartitionKey =? "sample"
    result.RowKey =? "x"
    result.Count =? 1
    result.Dob =? DateTime.MaxValue
    result.Name =? "Hello"
    result.Score =? 6.1

[<Fact>]
[<ResetTableData>]
let ``Inserts many rows using provided types correctly``() =
    table.Insert [| Local.Domain.tptestEntity(Partition "sample", Row "x", 1, DateTime.MaxValue, "Hello", 5.2)
                    Local.Domain.tptestEntity(Partition "sample", Row "y", 1, DateTime.MaxValue, "Hello", 1.8) |] |> ignore
    table.GetPartition("sample").Length =? 2

[<Fact>]
let ``Query without arguments brings back all rows``() =
    table.Query().Execute().Length =? 5

[<Fact>]
let ``Query with single query part brings back correct rows``() =
    table.Query().``Where Name Is``.``Equal To``("fred").Execute().Length =? 2

[<Fact>]
let ``Query with many query parts brings back correct rows``() =
     table.Query().``Where Name Is``.``Equal To``("fred")
                  .``Where Count Is``.``Equal To``(35)
                  .Execute().Length =? 1

//[<Fact>]
//let ``Query conditions on floats are correctly generated``() =
//     table.Query().``Where Score Is``.``Greater Than``(1.0)
//                  .Execute().Length =? 5

[<Fact>]
let ``Query conditions are correctly mapped``() =
    let baseQuery = table.Query().``Where Name Is``

    baseQuery.``Equal To``("fred").ToString()                 =? "[Name eq 'fred']"
    baseQuery.``Greater Than``("fred").ToString()             =? "[Name gt 'fred']"
    baseQuery.``Greater Than Or Equal To``("fred").ToString() =? "[Name ge 'fred']"
    baseQuery.``Less Than``("fred").ToString()                =? "[Name lt 'fred']"
    baseQuery.``Less Than Or Equal To``("fred").ToString()    =? "[Name le 'fred']"
    baseQuery.``Not Equal To``("fred").ToString()             =? "[Name ne 'fred']"

[<Fact>]
let ``Query restricts maximum results``() =
    table.Query().Execute(maxResults = 1).Length =? 1

[<Fact>]
let ``Cloud Table Client relates to the same data as the type provider``() =
    (Local.Tables.CloudTableClient.ListTables()
     |> Seq.map(fun c -> c.Name)
     |> Set.ofSeq
     |> Set.contains "tptest") =? true