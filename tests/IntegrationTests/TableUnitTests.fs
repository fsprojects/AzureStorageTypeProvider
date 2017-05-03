module TableTests

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table
open Swensen.Unquote
open System
open Expecto

type Local = AzureTypeProvider<"DevStorageAccount">
type Humanized = AzureTypeProvider<"DevStorageAccount", humanize = true>
let table = Local.Tables.employee

let tableSafe = beforeAfter TableHelpers.resetData TableHelpers.resetData
let tableSafeAsync = beforeAfterAsync TableHelpers.resetData TableHelpers.resetData

[<Tests>]
let compilationTests =
    testList "Table Compilation Tests" [
        testCase "Correctly identifies tables" (fun _ ->
            Local.Tables.large |> ignore
            Local.Tables.employee |> ignore)
        testCase "Missing fields are correctly shown as optionals" (fun _ ->
            let row = Local.Tables.optionals.Get(Row "1", Partition "partition")
            Expect.isSome row ""

            // Compiles!
            let row = row.Value
            ignore row.IsAnimal.IsSome
            ignore row.YearsWorking.IsSome
            ignore row.Dob)
        testCase "Correctly humanizes column names" (fun _ ->
            let entity = Humanized.Tables.optionals.Get(Row "1", Partition "partition").Value
            ignore <| entity.Dob
            ignore <| entity.``Is Animal``
            ignore <| entity.``Is Manager``
            ignore <| entity.Name
            ignore <| entity.``PartitionKey``
            ignore <| entity.``RowKey``
            ignore <| entity.Salary
            ignore <| entity.``Years Working``)
    ]


type MatchingTableRow =
    { Name : string
      YearsWorking : int 
      Dob : DateTime }

[<Tests>]
let readOnlyTableTests =
    TableHelpers.resetData()
    testList "Read-only Table Tests" [
        testCase "Table name is correctly identified" <| (fun _ -> table.Name |> shouldEqual "employee")
        testCase "Matching row and partition key returns Some row" (fun _ ->
            match table.Get(Row "2", Partition "men") with
            | Some row ->
                 row.Name |> shouldEqual "fred"
                 row.YearsWorking |> shouldEqual 35
                 row.Dob |> shouldEqual <| DateTime(1980, 4, 4, 0, 0, 0, DateTimeKind.Utc)
            | None -> failwith "could not locate row")
        testCaseAsync "Matching row and partition key returns Some row when using GetAsync" <| async {
            let! res = table.GetAsync(Row "2", Partition "men")
            match res with
            | Some row ->
                row.Name         |> shouldEqual "fred"
                row.YearsWorking |> shouldEqual 35
                row.Dob          |> shouldEqual <| DateTime(1980, 4, 4, 0, 0, 0, DateTimeKind.Utc)
            | None -> failwith "could not locate row" }
        testCase "Non matching partition key returns None" (fun _ -> Expect.isNone (table.Get(Row "35", Partition "random")) "")
        testCase "Non matching row key returns None" (fun _ -> Expect.isNone (table.Get(Row "random", Partition "fred")) "")
        testCase "Delete called with empty sequence should not fail" <| tableSafe (fun _ -> table.Delete [] |> ignore)
        testCase "Gets all rows in a table" (fun _ -> table.Query().Execute().Length |> shouldEqual 5)
        testCase "Gets all rows in a partition" (fun _ -> table.GetPartition("men").Length |> shouldEqual 3)
        testCaseAsync "Gets all rows in a partition asynchronously" <| async {
            let! partition = table.GetPartitionAsync "men"
            partition.Length |> shouldEqual 3 }
        testCase "Query without arguments brings back all rows" (fun _ -> table.Query().Execute().Length |> shouldEqual 5)
        testCase "Query with single query part brings back correct rows" (fun _ -> table.Query().``Where Name Is``.``Equal To``("fred").Execute().Length |> shouldEqual 2 )
        testCase "Query with many query parts brings back correct rows" (fun _ -> table.Query().``Where Name Is``.``Equal To``("fred").``Where Years Working Is``.``Equal To``(35).Execute().Length |> shouldEqual 1)
        testCase "Query conditions on floats are correctly generated" (fun _ ->
            let query = table.Query().``Where Salary Is``.``Greater Than``(1.0)
            query.Execute().Length |> shouldEqual 4)
        testCase "Query conditions are correctly mapped" (fun _ ->
            let baseQuery = table.Query().``Where Name Is``
            baseQuery.``Equal To``("fred").ToString()                 |> shouldEqual "[Name eq 'fred']" 
            baseQuery.``Greater Than``("fred").ToString()             |> shouldEqual "[Name gt 'fred']" 
            baseQuery.``Greater Than Or Equal To``("fred").ToString() |> shouldEqual "[Name ge 'fred']" 
            baseQuery.``Less Than``("fred").ToString()                |> shouldEqual "[Name lt 'fred']" 
            baseQuery.``Less Than Or Equal To``("fred").ToString()    |> shouldEqual "[Name le 'fred']" 
            baseQuery.``Not Equal To``("fred").ToString()             |> shouldEqual "[Name ne 'fred']")
        testCase "Query restricts maximum results" (fun _ -> table.Query().Execute(maxResults = 1).Length |> shouldEqual 1)
        testCase "Cloud Table Client relates to the same data as the type provider" (fun _ ->
            Expect.contains (Local.Tables.CloudTableClient.ListTables() |> Seq.map(fun c -> c.Name)) "employee" "")
        testCaseAsync "Async query without arguments brings back all rows" <| async {
            let! results = table.Query().ExecuteAsync()
            return results.Length |> shouldEqual 5 }
    ]

[<Tests>]
let detailedTableTests =
    testSequenced <| testList "Write Table Tests" [
        testCase "Inserts and deletes a row using lightweight syntax correctly" <| tableSafe (fun _ ->
            let result = table.Insert(Partition "isaac", Row "500", { Name = "isaac"; YearsWorking = 500; Dob = DateTime.UtcNow })
            result |> shouldEqual <| SuccessfulResponse ((Partition "isaac", Row "500"), 204)
            let deleteResponse = table.Delete([ Partition "isaac", Row "500"])
            deleteResponse |> shouldEqual [| "isaac", [| SuccessfulResponse((Partition "isaac", Row "500"), 204) |] |])
        
        testCaseAsync "Inserts and deletes a row asynchronously using lightweight syntax correctly" <| tableSafeAsync (async {
            let! result = table.InsertAsync(Partition "isaac", Row "500", { Name = "isaac"; YearsWorking = 500; Dob = DateTime.UtcNow })
            result |> shouldEqual <| SuccessfulResponse ((Partition "isaac", Row "500"), 204)
            let! deleteResponse = table.DeleteAsync([ Partition "isaac", Row "500"] )
            deleteResponse |> shouldEqual <| [| "isaac", [| SuccessfulResponse((Partition "isaac", Row "500"), 204) |] |] })
        
        testCase "Inserts and deletes a batch on same partition using lightweight syntax correctly" <| tableSafe (fun _ ->
            let result = table.Insert([ Partition "men", Row "5", { Name = "isaac"; YearsWorking = 500; Dob = DateTime.UtcNow }
                                        Partition "men", Row "6", { Name = "isaac"; YearsWorking = 250; Dob = DateTime.UtcNow } ])
            result |> shouldEqual <| [| "men", [| SuccessfulResponse((Partition "men", Row "6"), 204); SuccessfulResponse((Partition "men", Row "5"), 204) |] |]
            let deleteResponse = table.Delete([ Partition "men", Row "5"; Partition "men", Row "6" ] )
            deleteResponse |> shouldEqual <| [| "men", [| SuccessfulResponse((Partition "men", Row "6"), 204); SuccessfulResponse((Partition "men", Row "5"), 204) |] |] )
        
        testCaseAsync "Deletes row asynchronously using overload for single entity" <| tableSafeAsync (async {
            let entityToDelete = table.Get(Row "1", Partition "men")
            do! table.DeleteAsync(entityToDelete.Value) |> Async.Ignore
            Expect.isNone (table.Get(Row "1", Partition "men")) "" })
        
        testCaseAsync "Deletes rows asynchronously using overload for multiple entities" <| tableSafeAsync (async {
            let entitiesToDelete = table.GetPartition "men"
            do! table.DeleteAsync(entitiesToDelete) |> Async.Ignore
            table.GetPartition("men").Length |> shouldEqual 0 })
        
        testCase "Deleting a non-existant row raises an error" <| tableSafe (fun _ ->
            let deleteResponse = table.Delete [ Partition "blah", Row "500"]
            deleteResponse |> shouldEqual [| "blah", [| EntityError((Partition "blah", Row "500"), 404, "ResourceNotFound") |] |])
        
        testCaseAsync "Deleting a non-existant row asynchronously raises an error" <| tableSafeAsync (async {
            let! deleteResponse = table.DeleteAsync [ Partition "blah", Row "500"]
            deleteResponse |> shouldEqual <| [| "blah", [| EntityError((Partition "blah", Row "500"), 404, "ResourceNotFound") |] |] })
        
        testCase "Updates an existing row" <| tableSafe (fun _ ->
            table.Insert(Partition "men", Row "1", { Name = "fred"; YearsWorking = 35; Dob = DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, TableInsertMode.Upsert) |> ignore
            table.Get(Row "1", Partition "men").Value.Dob |> shouldEqual <| DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        
        testCase "Inserting an existing row returns an error" <| tableSafe (fun _ ->            
            table.Insert(Partition "men", Row "1", { Name = "fred"; YearsWorking = 35; Dob = DateTime.MaxValue }) |> shouldEqual <| EntityError((Partition "men", Row "1"), 409, "EntityAlreadyExists"))
        
        testCaseAsync "Inserting an existing row asynchronously returns an error" <| tableSafeAsync (async {
            let! result = table.InsertAsync(Partition "men", Row "1", { Name = "fred"; YearsWorking = 35; Dob = DateTime.MaxValue })
            result |> shouldEqual <| EntityError((Partition "men", Row "1"), 409, "EntityAlreadyExists") })
        
        testCase "Inserts a row using provided types correctly" <| tableSafe (fun _ ->
            table.Insert(Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), true, "Hello", 6.1, 1)) |> ignore
            let result = table.Get(Row "x", Partition "sample").Value
            
            result.PartitionKey |> shouldEqual "sample"
            result.RowKey       |> shouldEqual "x"
            result.YearsWorking |> shouldEqual 1
            result.Dob          |> shouldEqual <| DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            result.Name         |> shouldEqual "Hello"
            result.Salary       |> shouldEqual 6.1
            Expect.isTrue result.IsManager "")
        
        testCaseAsync "Inserts a row asynchronously using provided types correctly" <| tableSafeAsync (async {
            do! table.InsertAsync(Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), true, "Hello", 6.1, 1)) |> Async.Ignore
            let result = table.Get(Row "x", Partition "sample").Value
            
            result.PartitionKey |> shouldEqual "sample"
            result.RowKey       |> shouldEqual "x"
            result.YearsWorking |> shouldEqual 1
            result.Dob          |> shouldEqual <| DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            result.Name         |> shouldEqual "Hello"
            result.Salary       |> shouldEqual 6.1
            Expect.isTrue result.IsManager "" })
        
        testCase "Inserts many rows using provided types correctly" <| tableSafe (fun _ ->
            table.Insert [| Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime.MaxValue, true, "Hello", 5.2, 2)
                            Local.Domain.employeeEntity(Partition "sample", Row "y", DateTime.MaxValue, true, "Hello", 1.8, 2) |] |> ignore
            table.GetPartition("sample").Length |> shouldEqual 2)
        
        testCaseAsync "Inserts many rows asynchronously using provided types correctly" <| tableSafeAsync (async {
            do!
                table.InsertAsync [| Local.Domain.employeeEntity(Partition "sample", Row "x", DateTime.MaxValue, true, "Hello", 5.2, 2)
                                     Local.Domain.employeeEntity(Partition "sample", Row "y", DateTime.MaxValue, true, "Hello", 1.8, 2) |] 
                |> Async.Ignore
            2 |> shouldEqual <| table.GetPartition("sample").Length })
        
        testCaseAsync "Query with multiple batches returns all results in the correct order" <| tableSafeAsync (async {
            do!
                [| 1 .. 1001 |]
                |> Array.map(fun i ->
                    let i = sprintf "%04i" i
                    Local.Domain.employeeEntity(Partition "bulk insert", Row i, DateTime.MaxValue, true, "Hello", 100.0, 10))
                |> table.InsertAsync
                |> Async.Ignore
        
            let! retrievedEntries = table.GetPartitionAsync "bulk insert"
            retrievedEntries.Length |> shouldEqual 1001
            retrievedEntries.[0].RowKey |> shouldEqual "0001" 
            retrievedEntries.[1000].RowKey |> shouldEqual "1001"  })
        
        testCase "DeletePartition deletes entries with given partition key" <| tableSafe (fun _ ->
            let pKey, rows = table.DeletePartition "men"
            pKey |> shouldEqual "men"
            rows |> shouldEqual [| SuccessfulResponse((Partition "men", Row "3"), 204)
                                   SuccessfulResponse((Partition "men", Row "2"), 204)
                                   SuccessfulResponse((Partition "men", Row "1"), 204) |]
            table.Query().``Where Partition Key Is``.``Equal To``("men").Execute().Length |> shouldEqual 0)
        
        testCaseAsync "DeletePartitionAsync deletes entries with given partition key" <| tableSafeAsync (async {
            let! pKey, rows = table.DeletePartitionAsync "men"
            pKey |> shouldEqual "men"
            rows |> shouldEqual [| SuccessfulResponse((Partition "men", Row "3"), 204)
                                   SuccessfulResponse((Partition "men", Row "2"), 204)
                                   SuccessfulResponse((Partition "men", Row "1"), 204) |]
            table.Query().``Where Partition Key Is``.``Equal To``("men").Execute().Length |> shouldEqual 0 })
        
        testCase "Insert suceeds for entries over 4Mb" <| tableSafe (fun _ ->
            let generateLargeEntity partitionKey rowKey =
                let byteArr20kb = [| for i in 1 .. 20000 do yield i |> byte |]
                Local.Domain.largeEntity(partitionKey,rowKey,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb,byteArr20kb)
            
            let generateBatchOfLargeEntities partitionKey size = 
                [| for _ in 1 .. size -> generateLargeEntity partitionKey (Row(Guid.NewGuid().ToString())) |]

            let resultsOfInsert = generateBatchOfLargeEntities (Partition "1") 10 |> Local.Tables.large.Insert

            let failureCount = 
                resultsOfInsert        
                |> Array.collect snd
                |> Array.filter (function | SuccessfulResponse _ -> false | _ -> true)
                |> Array.length
            failureCount |> shouldEqual 0)
        
        testCase "Can insert an row with optionals filled in" <| tableSafe (fun _ ->
            let entity = Local.Domain.optionalsEntity(Partition "foo", Row "1", DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), true, "bar", 1., Some true, Some 10)
            Local.Tables.optionals.Insert entity |> ignore
            let entity = Local.Tables.optionals.Get(Row "1", Partition "foo")
            Expect.isSome entity ""
            let entity = entity.Value
            entity.Dob          |> shouldEqual <| DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            entity.IsAnimal     |> shouldEqual <| Some true
            entity.Name         |> shouldEqual "bar"
            entity.PartitionKey |> shouldEqual "foo"
            entity.RowKey       |> shouldEqual "1"
            entity.Salary       |> shouldEqual 1.
            entity.YearsWorking |> shouldEqual <| Some 10
            Expect.isTrue entity.IsManager "")
        
        testCase "Can insert an row with optionals omitted in" <| tableSafe (fun _ ->
            let entity = Local.Domain.optionalsEntity(Partition "foo", Row "1", DateTime(2000, 1, 1), true, "bar", 1.)
            Local.Tables.optionals.Insert entity |> ignore
            let entity = Local.Tables.optionals.Get(Row "1", Partition "foo")
            Expect.isSome entity ""
            Expect.isNone entity.Value.IsAnimal ""
            Expect.isNone entity.Value.YearsWorking "")
    ]

type StaticSchema = AzureTypeProvider<"UseDevelopmentStorage=true", tableSchema = "TableSchema.json">

[<Tests>]
let staticTableSchemaTests =
    testSequenced <| testList "Static Schema Table Tests" [
        testCase "Can correctly parse and integrate a schema file" <| (fun _ ->
            let table1 = StaticSchema.Tables.MyTable // compiles
            ())
        testCase "Works with multiple tables" <| (fun _ ->
            let table1 = StaticSchema.Tables.MyTable // compiles
            let table2 = StaticSchema.Tables.YourTable // compiles
            ())
        testCase "Can read and write data to a storage account" <| (fun _ ->
            let table1 = StaticSchema.Tables.MyTable
            table1.AsCloudTable().CreateIfNotExists() |> ignore
            let response = table1.Insert(StaticSchema.Domain.MyTableEntity(Partition "A", Row "1", true, Some "Test", Some (DateTime(2000,1,1))))
            let rows = table1.Query().``Where Partition Key Is``.``Equal To``("A").Execute()
            rows.Length |> shouldEqual 1
            table1.DeletePartition "A" |> ignore)
    ]