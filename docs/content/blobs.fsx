(*** hide ***)

#r @"..\..\bin\FSharp.Azure.StorageTypeProvider.dll"
#r @"..\..\bin\Microsoft.Data.OData.dll"
#r @"..\..\bin\Microsoft.Data.Services.Client.dll"
#r @"..\..\bin\Microsoft.Data.Edm.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Configuration.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Storage.dll"
#r @"..\..\bin\Newtonsoft.Json.dll"
#r @"..\..\bin\System.Spatial.dll"
#r @"System.Xml.Linq.dll"

open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Blob
open System.Xml.Linq
open System

type Azure = AzureTypeProvider<"UseDevelopmentStorage=true">

(**
Working with Blobs
==================

For more information on Blobs in general, please see some of the many articles on
[MSDN](https://msdn.microsoft.com/en-us/library/microsoft.windowsazure.storage.blob.aspx) or the [Azure](http://azure.microsoft.com/en-us/documentation/services/storage/) [documentation](http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/). Some of the core features of the Blob provider are: -

## Rapid navigation

You can easily move between containers, folders and blobs. Simply dotting into a container
or folder will automatically request the children of that node from Azure. This allows
easy exploration of your blob assets, directly from within the REPL.
*)

(*** define-output: blobStats ***)
let container = Azure.Containers.``tp-test``
let theBlob = container.``folder/``.``childFile.txt``
printfn "Blob '%s' is %d bytes big." theBlob.Name theBlob.Size
(*** include-output: blobStats ***)

(**
## Shared types
Individual files, folders and containers share a common base type so list operations are possible e.g.   
*)

(*** define-output: sumOfSizes ***)
let totalSize =
    [ container.``file1.txt``
      container.``file2.txt``
      container.``file3.txt``
      container.``sample.txt`` ]
    |> List.sumBy(fun blob -> blob.Size)

printfn "These files take up %d bytes." totalSize
(*** include-output: sumOfSizes ***)

(**
## Flexible API for read operations
You can quickly read the contents of a blob synchronously or asynchronously.
*)

(*** define-output: blobRead ***)
// sync read
let contents = theBlob.Read()
printfn "sync contents = '%s'" contents

// async read
async {
    let! contentsAsync = theBlob.ReadAsync()
    printfn "async contents = '%s'" contentsAsync
} |> Async.RunSynchronously
(*** include-output: blobRead ***)

(**
In addition, the provider has support for custom methods for different document types e.g. XML.
*)

(*** define-output: xmlBlobs ***)
let (contentsAsText:string) = container.``data.xml``.Read()
// only available on XML documents
let (contentsAsXml:XDocument) = container.``data.xml``.ReadAsXDocument()

printfn "text output = '%O'" contentsAsText
printfn "xml output = '%O'" contentsAsXml
(*** include-output: xmlBlobs ***)

(**
## Streaming support
The provider exposes the ability to easily open a stream to a document for sequential reading.
This is extremely useful for previewing large files etc.
*)
(*** define-output: streaming ***)
let streamReader = container.``sample.txt``.OpenStreamAsText()
while (not streamReader.EndOfStream) do
    printfn "LINE: '%s'" (streamReader.ReadLine())
(*** include-output: streaming ***)

(**

Again, since files share a common type, you can easily merge multiple sequential streams into one: -

*)

(*** define-output: streaming-fancy ***)
let filesToSplice =
    [ container.``file1.txt``
      container.``file2.txt``
      container.``file3.txt``
      container.``sample.txt`` ]

let lines =
    seq { for file in filesToSplice do
            printfn "Reading file '%s'" file.Name
            let sr = file.OpenStreamAsText()
            while (not sr.EndOfStream) do
                yield sr.ReadLine() }
      
printfn "starting to read all lines"
for line in lines do
    printfn "%s" line
printfn "finished reading all lines"
(*** include-output: streaming-fancy ***)

(**

## Download assets
You can quickly and easily download files, folders or entire containers to local disk.
*)

// download a single file into "C:\temp\files"
let asyncFileDownload = container.``file1.txt``.Download(@"C:\temp\files\")

(**
##Shared Access Signature generation

The type provider exposes a simple method for generating time-dependant SAS codes for
single files.
*)

(*** define-output: sas ***)
let duration = TimeSpan.FromMinutes 37.
printfn "Current time: %O" DateTime.UtcNow
printfn "SAS expiry: %O" (DateTime.UtcNow.Add duration)
let sasCode = container.``file1.txt``.GenerateSharedAccessSignature duration
printfn "SAS URI: %O" sasCode
(*** include-output: sas ***)
