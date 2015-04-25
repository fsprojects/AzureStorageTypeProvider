(*** hide ***)
#load @"..\tools\references.fsx"

(**
About the Azure Storage Type Provider
=====================================
The F# Azure Storage Type Provider allows quick and easy exploration of your
Azure Storage assets (Blobs, Tables and Queues) through the F# type system,
allowing for rapid access to large amounts of data cheaply, both through
scripts and applications. A fall-back to the standard .NET Azure SDK is also
provided.

Example
-------

This example illustrates some of the features available from the type provider.

*)

open FSharp.Azure.StorageTypeProvider

// Get a handle to my local storage emulator
type Azure = AzureTypeProvider<"UseDevelopmentStorage=true">

// Navigate through the containers to a specific file and read the contents.
let blobContents =
    Azure.Containers.samples.``folder/``.``childFile.txt``.Read()

// Perform a strongly-typed query against a table with automatic schema generation.
let results =
    Azure.Tables.employee.Query()
         .``Where Name Is``.``Equal To``("fred")
         .Execute()
         |> Array.map(fun row -> row.Name, row.Dob)

// Navigate through storage queues and get messages
let queueMessage = Azure.Queues.``sample-queue``.Dequeue()

(**

Samples & documentation
-----------------------

The library comes with comprehensible documentation.

 * The [quickstart](quickstart.html) contains further examples of how to get up and running as well the guiding principles for the library.

 * There are detailed tutorials for the [Blob](blobs.html), [Table](tables.html) and [Queue](queues.html)
   APIs.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/FSharp.ProjectScaffold/tree/master/docs/content
  [gh]: https://github.com/fsprojects/FSharp.ProjectScaffold
  [issues]: https://github.com/fsprojects/FSharp.ProjectScaffold/issues
  [readme]: https://github.com/fsprojects/FSharp.ProjectScaffold/blob/master/README.md
  [license]: https://github.com/fsprojects/FSharp.ProjectScaffold/blob/master/LICENSE.txt
*)
