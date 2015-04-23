(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.

#r @"..\..\bin\Microsoft.Data.OData.dll"
#r @"..\..\bin\Microsoft.Data.Services.Client.dll"
#r @"..\..\bin\Microsoft.Data.Edm.dll"
#r @"..\..\bin\System.Spatial.dll"
#r @"..\..\bin\Newtonsoft.Json.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Configuration.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Storage.dll"
#r @"System.Xml.Linq.dll"
#r @"..\..\bin\FSharp.Azure.StorageTypeProvider.dll"

(**
FSharp.Azure.StorageTypeProvider
================================

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The FSharp.Azure.StorageTypeProvider library can be <a href="https://nuget.org/packages/FSharp.Azure.StorageTypeProvider">installed from NuGet</a>:
      <pre>PM> Install-Package FSharp.Azure.StorageTypeProvider</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates using a function defined in this sample library.

*)

open FSharp.Azure.StorageTypeProvider

/// Get a handle to my local storage emulator
type StorageEmulator = AzureTypeProvider<"DevStoreAccount=true", "">

/// Navigate through the containers to a specific file and read the contents.
let childFileContents =
    StorageEmulator.Containers.``tp-test``.``folder/``.``childFile.txt``.Read()

//printfn "hello = %i" <| Library.hello 0
//
(**
Some more info

Samples & documentation
-----------------------

The library comes with comprehensible documentation. 
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content]. 
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

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
