module BlobTests

open FSharp.Azure.StorageTypeProvider
open Microsoft.WindowsAzure.Storage.Blob
open System.Linq
open System.IO
open FSharp.Azure.StorageTypeProvider.Blob
open Expecto

type Local = AzureTypeProvider<"DevStorageAccount", "">

type BlobSchema = AzureTypeProvider<blobSchema = "BlobSchema.txt">

let container = Local.Containers.samples
[<Tests>]
let compilationTests =
    testList "Blob Compilation Tests" [
        testCase "Correctly identifies blob containers" (fun _ -> Local.Containers.samples |> ignore)
        
        testCase "Correctly identifies blobs in a container" (fun _ ->
            [ container.``file1.txt``
              container.``file2.txt``
              container.``file3.txt`` ] |> ignore)
        
        testCase "Correctly identifies blobs in a subfolder" (fun _ -> container .``folder/``.``childFile.txt`` |> ignore)
        testCase "Page Blobs are listed" (fun _ -> container.``pageData.bin`` |> ignore) ]

let testFileDownload (blobFile:BlobFile) =
    let filename = Path.GetTempFileName()
    File.Delete filename
    blobFile.Download filename |> Async.RunSynchronously
    let predicates = 
        [ File.Exists
          FileInfo >> fun fi -> fi.Length = blobFile.Size ]
        |> List.map(fun pred -> pred filename)
    File.Delete filename
    predicates |> List.iter(fun item -> Expect.isTrue item "")

let testFolderDownload download expectedFiles expectedFolders =
    let tempFolder = Path.Combine(Path.GetTempPath(), sprintf "tpTestFolder_%O" (System.Guid.NewGuid()))
    if Directory.Exists tempFolder then Directory.Delete(tempFolder, true)
    download tempFolder |> Async.RunSynchronously
    let files = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories) |> Seq.length
    let folders = Directory.GetDirectories(tempFolder, "*", SearchOption.AllDirectories) |> Seq.length
    Directory.Delete(tempFolder, true)
    files |> shouldEqual expectedFiles
    folders |> shouldEqual expectedFolders

[<Tests>]
let mainTests =
    testList "Main Blob Tests" [
        testCase "Correctly gets size of a blob" (fun _ -> container .``sample.txt``.Size |> shouldEqual 190L)

        testCase "Reads a text file as text" (fun _ ->
            let text = container .``sample.txt``.Read()
            text |> shouldEqual "the quick brown fox jumps over the lazy dog\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada.\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor." )
       
        testCase "Streams a text file line-by-line" (fun _ ->
            let text = container .``sample.txt``.ReadLines() |> Seq.toArray
        
            text.[0] |> shouldEqual "the quick brown fox jumps over the lazy dog"
            text.[1] |> shouldEqual "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada."
            text.[2] |> shouldEqual "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor."
            text.Length |> shouldEqual 3)
 
        testCase "Opens a file with xml extension as an XML document" (fun _ ->
            let document = container.``data.xml``.ReadAsXDocument()
            let value = document.Elements().First()
                                .Elements().First()
                                .Value
            value |> shouldEqual "thing")

        testCase "Cloud Blob Client relates to the same data as the type provider" (fun _ ->
            Expect.contains (Local.Containers.CloudBlobClient.ListContainers() |> Seq.map(fun c -> c.Name)) "samples" "")

        testCase "Cloud Blob Container relates to the same data as the type provider" (fun _ ->
            let client = container.AsCloudBlobContainer()
            let blobs = client.ListBlobs() |> Seq.choose(function | :? CloudBlockBlob as b -> Some b | _ -> None) |> Seq.map(fun c -> c.Name) |> Seq.toList
            blobs |> shouldEqual [ "data.xml"; "file1.txt"; "file2.txt"; "file3.txt"; "sample.txt" ])

        testCase "CloudBlockBlob relates to the same data as the type provider" (fun _ ->
            let blob = container.``data.xml``.AsCloudBlockBlob()
            blob.Name |> shouldEqual "data.xml")

        testCase "Page Blobs support streams" (fun _ -> Expect.stringStarts (container.``pageData.bin``.OpenStreamAsText().ReadToEnd()) "hello from page blob" "")

        testCase "CloudPageBlob relates to the same data as the type provider" (fun _ ->
            let blob = container.``pageData.bin``.AsCloudPageBlob()
            blob.Name |> shouldEqual "pageData.bin")

        testCase "Page Blobs calculate size correctly" (fun _ -> container.``pageData.bin``.Size |> shouldEqual 512L)
        testCase "Can correctly download a block blob" (fun _ -> testFileDownload container.``file1.txt``)
        testCase "Can correctly download a page blob" (fun _ -> testFileDownload container.``pageData.bin``)
        testCase "Can correctly download a folder" (fun _ -> testFolderDownload container.``folder/``.Download 2 0)
        testCase "Can correctly download a container" (fun _ -> testFolderDownload container.Download 12 5)

        testCase "Can access Path property on a folder" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            childFolder.Path |> shouldEqual "folder2/child/")

        testCase "ListBlobs method returns correct number of blobs" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            let allBlobs = childFolder.ListBlobs()
            Seq.length allBlobs |> shouldEqual 1)

        testCase "Can access List blobs method on a folder" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            let allBlobs = childFolder.ListBlobs(true)
            let count = allBlobs |> Seq.length
            count |> shouldEqual 4)

        testCase "Container name is correct" (fun _ -> Local.Containers.samples.Name |> shouldEqual "samples")

        testCase "Correct container name from a static schema" (fun _ ->
            let container = BlobSchema.Containers.container2
            container.Name |> shouldEqual "container2")

        testCase "Correct folder path from a static schema" (fun _ ->
            let folder = BlobSchema.Containers.container1.``folder2/``.``subfolder/``
            folder.Path |> shouldEqual "folder2/subfolder/")
        
        testCase "Correct blob name from a static schema" (fun _ ->
            let blob = BlobSchema.Containers.container1.``folder1/``.item3
            blob.Name |> shouldEqual "item3")
    ]
