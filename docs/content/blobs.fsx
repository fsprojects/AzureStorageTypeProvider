(*** hide ***)
#load @"..\tools\references.fsx"
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
easy exploration of your blob assets, directly from within the REPL. Support exists for both
page and block blobs.
*)

(*** define-output: blobStats ***)
let container = Azure.Containers.samples
let theBlob = container.``folder/``.``childFile.txt``
printfn "Blob '%s' is %d bytes big." theBlob.Name (theBlob.Size())
(*** include-output: blobStats ***)

(** 
You can also perform useful helper actions on folders, such as pulling back all blobs in a folder.
*)

(*** define-output: folders ***)
let folder = container.``folder2/``
let blobs = folder.ListBlobs(true)
printfn "Folder '%s' has the following blobs: %A" folder.Path blobs
(*** include-output: folders ***)

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
    |> List.sumBy(fun blob -> blob.Size())

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
let lines =
    [ container.``file1.txt``
      container.``file2.txt``
      container.``file3.txt``
      container.``sample.txt`` ]
    |> Seq.collect(fun file -> file.ReadLines()) // could also use yield! syntax within a seq { }
      
printfn "starting to read all lines"
for line in lines do
    printfn "%s" line
printfn "finished reading all lines"
(*** include-output: streaming-fancy ***)

(**

## Offline development
In addition to using the Azure Storage Emulator, you can also simply provide the type provider
with a JSON file containing the list of blob containers, folders and files. This is particularly
useful within the context of a CI process, or when you know a specific "known good" structure of
blobs within a storage account.

You can still access blobs using the compile-time storage connection string if provided, or
override as normal at runtime.

*)

type BlobSchema = AzureTypeProvider<blobSchema = "BlobSchema.json">
let fileFromSchema = BlobSchema.Containers.samples.``file3.txt``

(**
The contents of `BlobSchema.json` looks as follows: -

*)


(*** hide ***)
let blobSchemaValue = IO.File.ReadAllText "BlobSchema.json"

(*** include-value: blobSchemaValue ***)

(**

Note that folder names must end with a forward slash e.g. `myfolder/`. Also observe that you can
specify the `Type` of blob as either `pageblob` or `blockblob`. If not specified, this defaults
to `blockblob`. You can leave "empty" values as either `null` or `{ }`.

## Programmatic access
There are times when working with blobs (particularly when working with an offline schema) that you
need to access blobs using "stringly typed" access. There are three ways you can do this within the
type provider.

### Safe support
For read access to blobs, you can use the Try... methods that are available on containers and
folders. These asynchronously check if the blob exists, before returning an optional handle to it.

*)

(*** define-output: programmatic-access ***)
let fileAsBlockBlob = container.TryGetBlockBlob "file1.txt" |> Async.RunSynchronously
printfn "Does file1.txt exist as a block blob? %b" (Option.isSome fileAsBlockBlob)
let fileAsPageBlob = container.TryGetPageBlob "file1.txt" |> Async.RunSynchronously
printfn "Does file1.txt exist as a page blob? %b" (Option.isSome fileAsPageBlob)
let doesNotExist = container.TryGetBlockBlob "doesNotExist" |> Async.RunSynchronously
printfn "Does doesNotExist exist as a block blob? %b" (Option.isSome doesNotExist)

(*** include-output: programmatic-access ***)

(**

### Unsafe support for block blob access
You can also "unsafely" access a block blob using indexers. This returns a blob reference which may or
may not exist but can be used quickly and easily - especially useful if you want to create a blob that
does not yet exist. However, be aware that any attempts to access a blob that does not exist will throw
an Azure SDK exception.

*)

(*** define-output: unsafe-blob ***)
let newBlob = container.["doesNotExist"]
newBlob.AsCloudBlockBlob().UploadText "hello"
printfn "Contents of blob: %s" (newBlob.Read())
newBlob.AsCloudBlockBlob().Delete()

(*** include-output: unsafe-blob ***)

(**

### Fallback to basic Azure SDK
Lastly, you can always fall back to the raw .NET Azure SDK (which the type provider sits on top of).

*)

// Access the 'samples' container using the raw SDK.
let rawContainer = Azure.Containers.samples.AsCloudBlobContainer()

// All blobs can be referred to as an ICloudBlob
let iCloudBlob = Azure.Containers.samples.``file1.txt``.AsICloudBlob()

// Only available to CloudBlockBlobs.
let blockBlob = Azure.Containers.samples.``file1.txt``.AsCloudBlockBlob()

// Only available to PageBlockBlobs.
let pageBlob = Azure.Containers.samples.``pageData.bin``.AsCloudPageBlob()

(**

## Download assets
You can quickly and easily download files, folders or entire containers to local disk.
*)

// download file1.txt asynchronously into "C:\temp\files"
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
