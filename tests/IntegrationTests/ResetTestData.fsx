﻿// This script sets up local azure storage to a well-known state for integration tests.
#I @"..\..\packages\WindowsAzure.Storage\lib\net45\"
#r @"Microsoft.WindowsAzure.Storage.dll"
#r @"Newtonsoft.Json.dll"

open Microsoft.WindowsAzure.Storage
open System.Text

let logWith entity func =
    printfn "Resetting %s data..." entity
    func()
    printfn "Done!"

let createData _ =
    let blobClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudBlobClient()
    let container = blobClient.GetContainerReference "samples"

    if container.Exists() then container.Delete()
    container.Create()

    let createBlockBlob fileName contents =
        let blob = container.GetBlockBlobReference fileName
        blob.UploadText contents

    let createPageBlob fileName (contents:string) =
        let blob = container.GetPageBlobReference fileName

        let bytes =
            let data = contents |> Encoding.UTF8.GetBytes |> ResizeArray
            let output = Array.init (data.Count - (data.Count % 512) + 512) (fun _ -> 0uy)
            data.CopyTo output
            output
        blob.UploadFromByteArray(bytes, 0, bytes.Length)

    createBlockBlob "file1.txt" "stuff"
    createBlockBlob "file2.txt" "more stuff"
    createBlockBlob "file3.txt" "even more stuff"
    createBlockBlob "folder/childFile.txt" "child file stuff"
    createBlockBlob "sample.txt" "the quick brown fox jumps over the lazy dog\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada.\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor."
    createBlockBlob "data.xml" "<data><items><item>thing</item></items></data>"
    createBlockBlob "folder2/child/grandchild1/descedant1.txt" "not important"
    createBlockBlob "folder2/child/grandchild1/descedant2.txt" "not important"
    createBlockBlob "folder2/child/grandchild2/descedant3.txt" "not important"
    createBlockBlob "folder2/child/descedant4.txt" "not important"
    createBlockBlob "folder/pageDataChild.txt" "hello from child page blob"
    createPageBlob "pageData.bin" "hello from page blob"
    
createData |> logWith "blob"

#load "TableHelpers.fs"
#load "QueueHelpers.fs"

open FSharp.Azure.StorageTypeProvider
TableHelpers.resetData |> logWith "table"
QueueHelpers.resetData |> logWith "queue"
