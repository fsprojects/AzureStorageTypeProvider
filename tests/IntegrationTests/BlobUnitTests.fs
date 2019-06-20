module BlobTests

open Expecto
open FSharp.Azure.StorageTypeProvider
open FSharp.Azure.StorageTypeProvider.Blob
open Microsoft.Azure.Storage.Blob
open System
open System.Linq
open System.IO
open FSharp.Control.Tasks.ContextInsensitive

type Local = BlobTypeProvider<"UseDevelopmentStorage=true", "">
type BlobSchema = BlobTypeProvider<blobSchema = "BlobSchema.json">

let container = Local.Containers.samples

[<Tests>]
let blobCompilationTests =
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
          FileInfo >> fun fi -> fi.Length = blobFile.Size() ]
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
let blobMainTests =
    testList "Blob Main Tests" [
        testCase "Correctly gets size of a blob" (fun _ -> container .``sample.txt``.Size() |> shouldEqual 190L)
        testCase "Correctly gets metadata for a blob" (fun _ ->
            let metadata = container .``sample.txt``.GetProperties() |> Async.RunSynchronously
            metadata.Size |> shouldEqual 190L)
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
            Expect.contains (Local.Containers.CloudBlobClient.ListContainersSegmentedAsync(null)
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> fun r -> r.Results
            |> Seq.map(fun c -> c.Name)) "samples" "")

        testCase "Cloud Blob Container relates to the same data as the type provider" (fun _ -> 
            let client = container.AsCloudBlobContainer()
            let blobs = client.ListBlobsSegmentedAsync(null) |> Async.AwaitTask |> Async.RunSynchronously |> fun x -> x.Results |> Seq.choose(function | :? CloudBlockBlob as b -> Some b | _ -> None) |> Seq.map(fun c -> c.Name) |> Seq.toList
            blobs |> shouldEqual [ "data.xml"; "file1.txt"; "file2.txt"; "file3.txt"; "sample.txt" ])

        testCase "CloudBlockBlob relates to the same data as the type provider" (fun _ ->
            let blob = container.``data.xml``.AsCloudBlockBlob()
            blob.Name |> shouldEqual "data.xml")

        testCase "Page Blobs support streams" (fun _ -> Expect.stringStarts (container.``pageData.bin``.OpenStreamAsText().ReadToEnd()) "hello from page blob" "")

        testCase "CloudPageBlob relates to the same data as the type provider" (fun _ ->
            let blob = container.``pageData.bin``.AsCloudPageBlob()
            blob.Name |> shouldEqual "pageData.bin")

        testCase "Correctly transforms metadata for a blob container" (fun _ ->
            let underlyingContainer = container.AsCloudBlobContainer()
            underlyingContainer.FetchAttributesAsync() |> Async.AwaitTask |> Async.RunSynchronously
            
            let metadata = container.GetProperties() |> Async.RunSynchronously
            metadata.LastModified |> shouldEqual (underlyingContainer.Properties.LastModified |> Option.ofNullable)
        )

        testCase "Page Blobs calculate size correctly" (fun _ -> container.``pageData.bin``.Size() |> shouldEqual 512L)
        testCase "Can correctly download a block blob" (fun _ -> testFileDownload container.``file1.txt``)
        testCase "Can correctly download a page blob" (fun _ -> testFileDownload container.``pageData.bin``)
        testCase "Can correctly download a folder" (fun _ -> testFolderDownload container.``folder/``.Download 2 0)
        testCase "Can correctly download a container" (fun _ -> testFolderDownload container.Download 12 5)

        testCase "Can access Path property on a folder" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            childFolder.Path |> shouldEqual "folder2/child/")

        testCase "ListBlobs method returns correct number of blobs" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            let allBlobs = childFolder.ListBlobs() |> Async.RunSynchronously
            Seq.length allBlobs |> shouldEqual 1)

        testCase "Can access List blobs method on a folder" (fun _ -> 
            let childFolder = Local.Containers.samples.``folder2/``.``child/``
            let allBlobs = childFolder.ListBlobs true |> Async.RunSynchronously
            let count = allBlobs |> Seq.length
            count |> shouldEqual 4)

        testCase "Container name is correct" (fun _ -> Local.Containers.samples.Name |> shouldEqual "samples")
        
        testCase "Sets Content Type on upload" (fun _ ->
            let testContent extension mimeType =
                let filename = sprintf "test.%s" extension
                File.WriteAllText(filename, "foo")
                Local.Containers.samples.Upload filename |> Async.RunSynchronously
                File.Delete filename

                let blob = Local.Containers.samples.[filename].AsCloudBlockBlob()
                blob.FetchAttributesAsync() |> Async.AwaitTask |> Async.RunSynchronously
                blob.DeleteAsync() |> Async.AwaitTask |> Async.RunSynchronously

                blob.Properties.ContentType |> shouldEqual mimeType
            testContent "txt" "text/plain"
            testContent "swf" "application/x-shockwave-flash"
            testContent "jpg" "image/jpeg")

        testCase "Retrieves blobs with prefix" (fun _ ->
            let blobs = Local.Containers.samples.``folder2/``.ListBlobs(prefix = "child/grandchild2/")  |> Async.RunSynchronously |> Seq.map(fun b -> b.Name) |> Seq.toArray
            blobs |> shouldEqual [| "folder2/child/grandchild2/descedant3.txt" |])

        testCase "Retrieves blobs with prefix and subfolders" (fun _ ->
            let blobs = Local.Containers.samples.``folder2/``.ListBlobs(includeSubfolders = true, prefix = "child/")  |> Async.RunSynchronously|> Seq.map(fun b -> b.Name) |> Seq.sort |> Seq.toArray 
            blobs.Length |> shouldEqual 4)
         ]

[<Tests>]
let blobContainerTests =
    testList "Blob Container Tests" [
        testCase "Can list all blobs in a container" (fun _ ->
            let blobs = Local.Containers.samples.ListBlobs true |> Async.RunSynchronously |> Seq.length
            blobs |> shouldEqual 12)
        testCase "Container supports unsafe blob access" (fun _ ->
            let b = Local.Containers.samples.["file1.txt"]
            b.Size() |> shouldEqual 5L)
        testCase "Container supports safe blob access" (fun _ ->
            let b = Local.Containers.samples.TryGetBlockBlob "file1.txt" |> Async.RunSynchronously
            Expect.isSome b "Should have returned a blob")
    ]

[<Tests>]
let blobStaticSchemaTests =
    testList "Blob Static Schema Tests" [
        testCase "Correct container name from a static schema" (fun _ ->
            let container = BlobSchema.Containers.samples
            container.Name |> shouldEqual "samples")

        testCase "Correct folder path from a static schema" (fun _ ->
            let folder = BlobSchema.Containers.samples.``folder2/``.``child/``
            folder.Path |> shouldEqual "folder2/child/")
        
        testCase "Correct blob name from a static schema" (fun _ ->
            let blob = BlobSchema.Containers.samples.``folder/``.``childFile.txt``
            blob.Name |> shouldEqual "folder/childFile.txt")

        testCase "Can access a real file using static schema" (fun _ ->
            let blob = BlobSchema.Containers.samples.``file1.txt``
            blob.Size() |> shouldEqual 5L)

        testCase "Compiles with a non-existant file" (fun _ ->
            BlobSchema.Containers.random.``file.txt``
            |> ignore) // compiles!

        testCase "Compiles with folder-only paths" (fun _ ->
            BlobSchema.Containers.random.``folder/``.``emptyFolder/``
            |> ignore) //compiles!

        testCase "Compiles with empty container" (fun _ ->
            BlobSchema.Containers.emptyContainer
            |> ignore) //compiles!

        testCase "Default to block blob if not specified" (fun _ ->
            BlobSchema.Containers.samples.``file2.txt``.AsICloudBlob().BlobType
            |> shouldEqual BlobType.BlockBlob)

        testCase "Sets as block blob if specified" (fun _ ->
            BlobSchema.Containers.samples.``file1.txt``.AsICloudBlob().BlobType
            |> shouldEqual BlobType.BlockBlob)

        testCase "Sets as page blob if specified" (fun _ ->
            BlobSchema.Containers.samples.``file3.txt``.AsICloudBlob().BlobType
            |> shouldEqual BlobType.PageBlob)
    ]

[<Tests>]
let blobProgrammaticTests =
    testList "Blob Folder Tests" [
        testCase "Can return an unsafe handle to a blob" <| fun _ ->
            let blob = Local.Containers.samples.``folder/``.["childFile.txt"]
            blob.Name |> shouldEqual "folder/childFile.txt"
            blob.Size() |> shouldEqual 16L
        testCase "Safe handle to an existing block blob returns Some" <| fun _ -> 
            let blob = Local.Containers.samples.``folder/``.TryGetBlockBlob "childFile.txt" |> Async.RunSynchronously
            Expect.isSome blob ""
        testCase "Safe handle to a non-existant block blob returns None" <| fun _ -> 
            let blob = Local.Containers.samples.``folder/``.TryGetBlockBlob "childFilexxx.txt" |> Async.RunSynchronously
            Expect.isNone blob ""
        testCase "Safe handle to a non-existant page blob returns None" <| fun _ -> 
            let blob = Local.Containers.samples.``folder/``.TryGetBlockBlob "childFilexxx.txt" |> Async.RunSynchronously
            Expect.isNone blob ""
        testCase "Safe handle to the wrong blob type returns None" <| fun _ -> 
            let blob = Local.Containers.samples.``folder/``.TryGetPageBlob "childFile.txt" |> Async.RunSynchronously
            Expect.isNone blob ""
    ]

[<Tests>]
let sasTokenTests =
    testList "SAS Token Tests" [
        testCase "Generates token with default (full-access) blob permissions" (fun _ ->
            let sas = container.``file1.txt``.GenerateSharedAccessSignature (TimeSpan.FromDays 7.)
            Expect.stringContains (sas.ToString()) "sp=rwdl" "Invalid permissions"
        )

        testCase "Generates token with specific blob permissions" (fun _ ->
            let sas = container.``file1.txt``.GenerateSharedAccessSignature(TimeSpan.FromDays 7., permissions = (SharedAccessBlobPermissions.Read ||| SharedAccessBlobPermissions.List))
            Expect.stringContains (sas.ToString()) "sp=rl" "Invalid permissions"
        )
    ]