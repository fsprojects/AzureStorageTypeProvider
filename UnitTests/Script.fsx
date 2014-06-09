// This script sets up local azure storage to a well-known state for tests.
#r @"..\packages\WindowsAzure.Storage.3.0.2.0\lib\net40\Microsoft.WindowsAzure.Storage.dll"

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage.Table
open System

// blob data
let blobClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudBlobClient()
let container = blobClient.GetContainerReference("tp-test")

if container.Exists() then container.Delete()
container.Create()

let createBlob fileName contents = 
    let blob = container.GetBlockBlobReference(fileName)
    blob.UploadText(contents)

createBlob "file1.txt" "stuff"
createBlob "file2.txt" "stuff"
createBlob "file3.txt" "stuff"
createBlob "folder/childFile.txt" "stuff"
createBlob "sample.txt" "the quick brown fox jumped over the lazy dog
bananas"
createBlob "data.xml" "<data><items><item>thing</item></items></data>"

// table data
let tableClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudTableClient()
let table = tableClient.GetTableReference("tptest")

type RandomEntity() = 
    inherit TableEntity()
    member val Name = String.Empty with get, set
    member val Count = 0 with get, set
    member val Dob = DateTime.MinValue with get, set

let insertRow (name, count, dob) = 
    RandomEntity(Name = name, Count = count, Dob = dob, PartitionKey = name, RowKey = count.ToString())
    |> TableOperation.Insert
    |> table.Execute

if table.Exists() then table.Delete()
table.Create()

insertRow("fred", 10, DateTime(1990, 5, 1))
insertRow("fred", 35, DateTime(1980, 4, 4))
insertRow("tim", 99, DateTime(2001, 10, 5))
insertRow("sara", 35, DateTime(2005, 4, 30))
insertRow("rachel", 20, DateTime(1965, 8, 20))

