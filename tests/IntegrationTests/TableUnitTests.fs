module FSharp.Azure.StorageTypeProvider.``Table Unit Tests``

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table
open Microsoft.WindowsAzure.Storage.Table
open Swensen.Unquote
open System
open System.Linq
open Xunit

type Local = AzureTypeProvider<"DevStorageAccount", "">

let table = Local.Tables.employee
let lgeTable = Local.Tables.large

type ResetTableDataAttribute() =
    inherit BeforeAfterTestAttribute()
    override x.Before(methodUnderTest) = TableHelpers.resetData()
    override x.After(methodUnderTest) = TableHelpers.resetData()

[<Fact>]
let ``Correctly identifies tables``() =
    // compiles!
    Local.Tables.large |> ignore
    Local.Tables.employee

[<Fact>]
let ``Table name is correctly identified``() =
    test <@ table.Name = "employee" @>

[<Fact>]
let ``Matching row and partition key returns Some row``() =
    match table.Get(Row "2", Partition "men") with
    | Some row ->
        test <@ row.Name = "fred" @>
        test <@ row.YearsWorking = 35 @>
        test <@ row.Dob = DateTime(1980, 4, 4, 0, 0, 0, DateTimeKind.Utc) @>
    | None -> failwith "could not locate row"

[<Fact>]
let ``Non matching partition key returns None``() =
    test <@ table.Get(Row "35", Partition "random") = None @>

[<Fact>]
let ``Non matching row key returns None``() =
    test <@ table.Get(Row "random", Partition "fred") = None @>

[<Fact>]
[<ResetTableData>]
let ``Gets all rows in a table``() =
    test <@ table.Query().Execute().Length = 5 @>

[<Fact>]
[<ResetTableData>]
let ``Gets all rows in a partition``() =
    test <@ table.GetPartition("men").Length = 3 @>

type MatchingTableRow =
    { Name : string
      YearsWorking : int 
      Dob : DateTime }

[<Fact>]
[<ResetTableData>]
let ``Inserts and deletes a row using lightweight syntax correctly``() =
    let result = table.Insert(Partition "isaac", Row "500", { Name = "isaac"; YearsWorking = 500; Dob = DateTime.UtcNow })
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
    let result = table.Insert( [ Partition "men", Row "5", { Name = "isaac"; YearsWorking = 500; Dob = DateTime.UtcNow }
                                 Partition "men", Row "6", { Name = "isaac"; YearsWorking = 250; Dob = DateTime.UtcNow }
                               ])
    match result with
    | [| "men", [| SuccessfulResponse _; SuccessfulResponse _ |] |] ->
        let deleteResponse = table.Delete([ Partition "men", Row "5"; Partition "men", Row "6" ] )
        match deleteResponse with
        | [| "men", [| SuccessfulResponse _; SuccessfulResponse _ |] |] -> ()
        | res -> failwith <| sprintf "error deleting %A" res
    | res -> failwith <| sprintf "error inserting: %A" res

[<Fact>]
[<ResetTableData>]
let ``Updates an existing row``() =
    table.Insert(Partition "men", Row "1", { Name = "fred"; YearsWorking = 35; Dob = DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, TableInsertMode.Upsert) |> ignore
    test <@ table.Get(Row "1", Partition "men").Value.Dob = DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) @>

[<Fact>]
[<ResetTableData>]
let ``Inserting an existing row returns an error``() =
    test <@ table.Insert(Partition "men", Row "1", { Name = "fred"; YearsWorking = 35; Dob = DateTime.MaxValue })
             = EntityError((Partition "men", Row "1"), 409, "EntityAlreadyExists") @>

[<Fact>]
[<ResetTableData>]
let ``Inserts a row using provided types correctly``() =
    table.Insert(Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), true, "Hello", 6.1, 1)) |> ignore
    let result = table.Get(Row "x", Partition "sample").Value
    
    test <@ result.PartitionKey = "sample" @>
    test <@ result.RowKey = "x" @>
    test <@ result.YearsWorking = 1 @>
    test <@ result.Dob = DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) @>
    test <@ result.Name = "Hello" @>
    test <@ result.Salary = 6.1 @>
    test <@ result.IsManager = true @>

[<Fact>]
[<ResetTableData>]
let ``Inserts many rows using provided types correctly``() =
    table.Insert [| Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime.MaxValue, true, "Hello", 5.2, 2)
                    Local.Domain.employeeEntity(Partition "sample", Row "y", DateTime.MaxValue, true, "Hello", 1.8, 2) |] |> ignore
    test <@ table.GetPartition("sample").Length = 2 @>

[<Fact>]
[<ResetTableData>]
let ``Query without arguments brings back all rows``() =
    test <@ table.Query().Execute().Length = 5 @>

[<Fact>]
[<ResetTableData>]
let ``Query with single query part brings back correct rows``() =
    test <@ table.Query().``Where Name Is``.``Equal To``("fred").Execute().Length = 2 @>

[<Fact>]
[<ResetTableData>]
let ``Query with many query parts brings back correct rows``() =
     test <@ table.Query().``Where Name Is``.``Equal To``("fred")
                          .``Where Years Working Is``.``Equal To``(35)
                          .Execute().Length = 1 @>

[<Fact>]
[<ResetTableData>]
let ``Query conditions on floats are correctly generated``() =
    let query = table.Query().``Where Salary Is``.``Greater Than``(1.0)
    test <@ query.Execute().Length = 4 @>

[<Fact>]
let ``Query conditions are correctly mapped``() =
    let baseQuery = table.Query().``Where Name Is``

    test <@ baseQuery.``Equal To``("fred").ToString()                 = "[Name eq 'fred']" @>
    test <@ baseQuery.``Greater Than``("fred").ToString()             = "[Name gt 'fred']" @>
    test <@ baseQuery.``Greater Than Or Equal To``("fred").ToString() = "[Name ge 'fred']" @>
    test <@ baseQuery.``Less Than``("fred").ToString()                = "[Name lt 'fred']" @>
    test <@ baseQuery.``Less Than Or Equal To``("fred").ToString()    = "[Name le 'fred']" @>
    test <@ baseQuery.``Not Equal To``("fred").ToString()             = "[Name ne 'fred']" @>

[<Fact>]
let ``Query restricts maximum results``() =
    test <@ table.Query().Execute(maxResults = 1).Length = 1 @>

[<Fact>]
let ``Cloud Table Client relates to the same data as the type provider``() =
    test <@ (Local.Tables.CloudTableClient.ListTables()
             |> Seq.map(fun c -> c.Name)
             |> Set.ofSeq
             |> Set.contains "employee") = true @>

[<Fact>]
[<ResetTableData>]
let ``DeletePartition deletes entries with given partition key``() =
    table.DeletePartition "men"
    test <@ table.Query().``Where Partition Key Is``.``Equal To``("men").Execute().Length = 0 @>

[<Fact>]
[<ResetTableData>]
let ``Insert suceeds for entries over 4Mb``() =
    let generateLargeEntity partitionKey rowKey =
        let byteArr20kb = [| for i in 1 .. 20000 do yield i |> byte |]
        Local.Domain.largeEntity(partitionKey,rowKey,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb)
    
    let generateBatchOfLargeEntities partitionKey size = 
        [| for i in 1 .. size do yield generateLargeEntity partitionKey (Row(Guid.NewGuid().ToString())) |]

    let resultsOfInsert = 
        generateBatchOfLargeEntities (Partition("1")) 10 
        |> lgeTable.Insert

    let failureCount = 
        resultsOfInsert        
        |> Array.collect snd
        |> Array.filter (function | SuccessfulResponse _ -> false | _ -> true)
        |> Array.length
    test <@ failureCount = 0 @>

[<Fact>]
[<ResetTableData>]
let ``Async query without arguments brings back all rows``() =
    let length = 
        async{
            let! results = table.Query().ExecuteAsync();
            return results.Length
        } 
        |> Async.RunSynchronously
    test <@ length = 5 @>
