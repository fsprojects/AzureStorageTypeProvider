// This script sets up local azure storage to a well-known state for integration tests.
#r @"..\..\packages\WindowsAzure.Storage\lib\net40\Microsoft.WindowsAzure.Storage.dll"

open Microsoft.WindowsAzure.Storage

let logWith entity func =
    printfn "Resetting %s data..." entity
    func()
    printfn "Done!"

fun () ->
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
    createBlob "sample.txt" "the quick brown fox jumped over the lazy dog\nbananas"
    createBlob "data.xml" "<data><items><item>thing</item></items></data>"
|> logWith "blob"

#load "TableHelpers.fs"
#load "QueueHelpers.fs"

open FSharp.Azure.StorageTypeProvider
TableHelpers.resetData |> logWith "table"
QueueHelpers.resetData |> logWith "queue"
