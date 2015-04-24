(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#r @"..\..\bin\FSharp.Azure.StorageTypeProvider.dll"
#r @"..\..\bin\Microsoft.Data.OData.dll"
#r @"..\..\bin\Microsoft.Data.Services.Client.dll"
#r @"..\..\bin\Microsoft.Data.Edm.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Configuration.dll"
#r @"..\..\bin\Microsoft.WindowsAzure.Storage.dll"
#r @"..\..\bin\Newtonsoft.Json.dll"
#r @"..\..\bin\System.Spatial.dll"
#r @"System.Xml.Linq.dll"
open FSharp.Azure.StorageTypeProvider.Table

(**
Introducing the Azure Storage Type Provider
===========================================

The Azure Storage Type Provider is designed to let you rapidly access your
storage assets in Azure from F# without the need to resort to third party tools
or the server explorer in Visual Studio.

The provider is designed to be as easy as possible to consume, whilst allowing
several benefits over using the standard .NET SDK, such as removing the need for
magic strings when access tables / queues / blobs, as well as strengthening the
typing of queries over tables.

Connecting to Azure
===================

Connecting to your storage account is simple.

From within a script, first #load the "AzureStorageProvider.fsx" to reference all dependencies.
Then, you can generate a type for a storage account.

*)

open FSharp.Azure.StorageTypeProvider

// Connect to a live account using a standard Azure Storage connection string
type Live =
    AzureTypeProvider<"DefaultEndpointsProtocol=https;AccountName=name;AccountKey=key;">

// Connect to a live account using a two-part name and key.
type LiveTwoPart = AzureTypeProvider<"name", "key">

// Connect to local storage emulator
type Local = AzureTypeProvider<"UseDevelopmentStorage=true">

(**

Common Themes
=============

The API is split into three areas: **Tables**, **Queues** and **Containers**.

All three share some common themes: -

- Where possible, entities such as queues, tables, containers, folders or blobs are typed based on
live queries to the appropriate underlying storage SDK. Thus there's no requirement to enter magic
strings for blobs, tables or queues.

*)

// Reference the "tptest" table. No magic strings needed.
let tableRef = Local.Tables.tptest

// Navigation through the "tp-test" container to the "folder/childFile.txt" blob.
let blobRef = Local.Containers.``tp-test``.``folder/``.``childFile.txt``

// Reference the tp-test queue.
let queueRef = Local.Queues.tptest

(**

- Common methods and functionality are made available directly on the appropriate assets, but
there's always access to the raw Azure SDK if you need it to do anything more.

*)

// Get the raw Azure Cloud Blob Client and use it to get a ref to the same blob.
let rawBlobRef = Local.Containers.CloudBlobClient.GetContainerReference("tp-test")
                                                 .GetBlockBlobReference("folder/childFile.txt")

// Or for the Cloud Table Client
let rawTableRef = Local.Tables.CloudTableClient.GetTableReference("tptest")

// Or use a hybrid approach - use the TP to find the asset you want and then switch.
let rawBlobRefHybrid = Local.Containers.``tp-test``.``folder/``.``childFile.txt``.AsCloudBlockBlob()

(**
 
- You can override the connection to the destination asset at runtime. This is useful so that you can
use your local (free) storage emulator for development, and then move to a live account when testing
or moving to production etc.

*)

// Redirect to a blob using a different connection string.
let liveBlob = Local.Containers.``tp-test``.``file1.txt``.AsCloudBlockBlob("myOtherConnectionString")

// Get row 1A from the "tptest" table using a different connection string.
let row1A = Local.Tables.tptest.Get(Row "1", Partition "A", "myOtherConnectionString")