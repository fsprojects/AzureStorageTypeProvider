(*** hide ***)
#load @"..\tools\references.fsx"
open Deedle
open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Table
open Microsoft.WindowsAzure.Storage.Table
open System

type Azure = AzureTypeProvider<"UseDevelopmentStorage=true">

(**
Working with Tables
===================

For more information on Tables in general, please see some of the many articles on
[MSDN](https://msdn.microsoft.com/en-us/library/microsoft.windowsazure.storage.table.aspx) or the [Azure](http://azure.microsoft.com/en-us/documentation/services/storage/) [documentation](http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-tables/). Some of the core features of the Tables provider are: -

##Rapid navigation

You can easily move between tables within a storage account. Simply dotting into a Tables property
will automatically retrieve the list of tables in the storage account. This allows
easy exploration of your table assets, directly from within the REPL.
*)

(*** define-output: tableStats ***)
let table = Azure.Tables.tptest
printfn "The table is called '%s'." table.Name
(*** include-output: tableStats ***)

(**

##Automatic schema inference
Unlike some non-relational data stores, Azure Table Storage does maintain schema information at the
row level in the form of EDM metadata. This can be interrogated in order to infer a table's shape,
as well as help query data. Let's look at the schema of the row ``fred.10`` in the table ``tptest``.
*)

(*** hide ***)

let theData =
    let keys = [ "Column Name"; "EDM Data Type"; "Value" ]
    let series = 
        [ [ "Partition Key"; "Row Key"; "Count"; "Dob"; "Name"; "Score" ]
          [ "string"; "string"; "int"; "datetime"; "string"; "double"; ]
          [ "fred"; "10"; "10"; "01/05/1990"; "fred"; "0"] ]
        |> List.map Series.ofValues
        |> List.map (fun s -> s :> ISeries<_>)
    Frame(keys, series) |> Frame.indexRowsString "Column Name"

(*** include-value: theData ***)

(**
Based on this EDM metadata, an appropriate .NET type can be generated and used within the provider.
*)

(*** hide ***)

let fred10 = table.Get(Row "10", Partition "fred").Value

(** *)

(*** define-output: fred ***)
printfn "Fred has Partition Key of %s and Row Key of %s" fred10.PartitionKey fred10.RowKey
printfn "Fred has Name '%s', Count '%d', Dob '%O' and Score '%f'" fred10.Name fred10.Count fred10.Dob fred10.Score
(*** include-output: fred ***)

(** 
In addition, an extra "Values" property is available which exposes all properties on the entity in a
key/value collection - this is useful for binding scenarios or e.g. mapping in Deedle frames.
*)

let frame =
    [ fred10 ]
    |> Frame.ofRecords
    |> Frame.expandCols [ "Values" ] // expand the "Values" nested column
    |> Frame.indexRowsUsing(fun c -> sprintf "%O.%O" c.["PartitionKey"] c.["RowKey"]) // Generate a useful row index
    |> Frame.dropCol "PartitionKey"
    |> Frame.dropCol "RowKey"
    |> fun f -> f |> Frame.indexColsWith (f.ColumnKeys |> Seq.map(fun c -> c.Replace("Values.", ""))) // rename columns to omit "Values."
(*** include-value: frame ***)

(**

##Querying data
The storage provider has an easy-to-use query API that is also flexble and powerful, and uses the 
inferred schema to generate the appropriate query functionality. Data can be queried in several ways: -

###Key Lookups
These are the simplest (and best performing) queries, based on a partition / row key combination,
returning an optional result. You can also retrieve an entire partition.
*)
let tim = table.Get(Row "99", Partition "tim") // try to get a single row
let allFredRows = table.GetPartition("fred") // get all rows in the fred partition

(**
###Plain Text Queries
If you need to search for a set of entities, you can enter a plain text search, either manually or using
the Azure SDK query builder.

*)
(*** define-output: query2 ***)
// create the query string by hand
let someData = table.Query("Count eq 35")
printfn "The query returned %d rows." someData.Length

// generate a query string programmatically.
let filterQuery = TableQuery.GenerateFilterConditionForInt("Count", QueryComparisons.Equal, 35)
printfn "Generated query is '%s'" filterQuery
(*** include-output: query2 ***)

(**

###Query DSL
A third alternative to querying with the Azure SDK is to use the LINQ IQueryable implementation. This
works in F# as well using the ``query { }`` computation expression. However, this implementation has two main
limitations: -
    - You need to manually create a type to handle the result
    - IQueryable does not guarantee runtime safety for a query that compiles. This is particularly evident
    with the Azure Storage Tables, which allow a very limited set of queries. The Table provider allows an
    alternative that has the same sort of composability of IQueryable, yet is strong typed at compile- and
    runtime, whilst being extremely easy to use. *)

let tpQuery = table.Query().``Where Count Is``.``Equal To``(35)

(*** include-value: tpQuery ***)

(** These can be composed and chained. When you have completed building, simply call ``Execute()``. *)

let longerQuery = table.Query()
                       .``Where Count Is``.``Greater Than``(14)
                       .``Where Name Is``.``Equal To``("Fred")

(*** include-value: longerQuery ***)

(**
Query operators are strongly typed, so ``Equal To`` on ``Count`` takes in an int, whereas on ``Name``
it takes in a string. For boolean properties, you simple have ``IsTrue`` and ``IsFalse`` as query operators.

##Inserting data

##Deleting data
*)