module FSharp.Azure.StorageTypeProvider.``Table Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table
open Xunit
open System
open System.Linq

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
    Assert.Equal<string>("tptest", table.Name)

[<Fact>]
let ``Matching row and partition key returns Some row``() =
    match table.Get(Row "35", Partition "fred") with
    | Some row -> Assert.Equal<string>("fred", row.Name)
                  Assert.Equal(35, row.Count)
                  Assert.Equal(DateTime(1980, 4, 4), row.Dob)
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
    Assert.Equal(5, table.Query().Execute().Length)

[<Fact>]
let ``Gets all rows in a partition``() =
    Assert.Equal(2, table.GetPartition("fred").Length)

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
    Assert.Equal(DateTime.MaxValue, table.Get(Row "35", Partition "fred").Value.Dob)

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
    Assert.Equal<string>("sample", result.PartitionKey)
    Assert.Equal<string>("x", result.RowKey)
    Assert.Equal(1, result.Count)
    Assert.Equal(DateTime.MaxValue, result.Dob)
    Assert.Equal<string>("Hello", result.Name)
    Assert.Equal(6.1, result.Score)

[<Fact>]
[<ResetTableData>]
let ``Inserts many rows using provided types correctly``() =
    table.Insert [| Local.Domain.tptestEntity(Partition "sample", Row "x", 1, DateTime.MaxValue, "Hello", 5.2)
                    Local.Domain.tptestEntity(Partition "sample", Row "y", 1, DateTime.MaxValue, "Hello", 1.8) |] |> ignore
    Assert.Equal(2, table.GetPartition("sample").Length)

[<Fact>]
let ``Query without arguments brings back all rows``() =
    Assert.Equal(5, table.Query().Execute().Length)

[<Fact>]
let ``Query with single query part brings back correct rows``() =
    Assert.Equal(2, table.Query().``Where Name Is``.``Equal To``("fred").Execute().Length)

[<Fact>]
let ``Query with many query parts brings back correct rows``() =
    Assert.Equal(1, table.Query().``Where Name Is``.``Equal To``("fred")
                                 .``Where Count Is``.``Equal To``(35)
                                 .Execute().Length)

[<Fact>]
let ``Query conditions are correctly mapped``() =
    let baseQuery = table.Query().``Where Name Is``

    Assert.Equal<string>("[Name eq 'fred']", baseQuery.``Equal To``("fred").ToString())
    Assert.Equal<string>("[Name gt 'fred']", baseQuery.``Greater Than``("fred").ToString())
    Assert.Equal<string>("[Name ge 'fred']", baseQuery.``Greater Than Or Equal To``("fred").ToString())
    Assert.Equal<string>("[Name lt 'fred']", baseQuery.``Less Than``("fred").ToString())
    Assert.Equal<string>("[Name le 'fred']", baseQuery.``Less Than Or Equal To``("fred").ToString())
    Assert.Equal<string>("[Name ne 'fred']", baseQuery.``Not Equal To``("fred").ToString())

[<Fact>]
let ``Query restricts maximum results``() =
    Assert.Equal(1, table.Query().Execute(maxResults = 1).Length)