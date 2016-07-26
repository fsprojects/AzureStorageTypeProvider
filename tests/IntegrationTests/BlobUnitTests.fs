module ``Blob Unit Tests``

open FSharp.Azure.StorageTypeProvider
open Microsoft.WindowsAzure.Storage.Blob
open Swensen.Unquote
open System.Linq
open Xunit
open System.IO
open FSharp.Azure.StorageTypeProvider.Blob

type Local = AzureTypeProvider<"DevStorageAccount", "">

let container = Local.Containers.samples

[<Fact>]
let ``Correctly identifies blob containers``() =
    // compiles!
    Local.Containers.samples

[<Fact>]
let ``Correctly identifies blobs in a container``() =
    // compiles!
    [ container .``file1.txt``
      container .``file2.txt``
      container .``file3.txt`` ]

[<Fact>]
let ``Correctly identifies blobs in a subfolder``() =
    // compiles!
    container .``folder/``.``childFile.txt``

[<Fact>]
let ``Correctly gets size of a blob``() =
    test <@ container .``sample.txt``.Size = 190L @>

[<Fact>]
let ``Reads a text file as text``() =
    let text = container .``sample.txt``.Read()
    test <@ text = "the quick brown fox jumps over the lazy dog\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada.\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor." @>

[<Fact>]
let ``Streams a text file line-by-line``() =
    let text = container .``sample.txt``.ReadLines() |> Seq.toArray

    test <@ text.[0] = "the quick brown fox jumps over the lazy dog" @>
    test <@ text.[1] = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada." @>
    test <@ text.[2] = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor." @>
    test <@ text.Length = 3 @>

[<Fact>]
let ``Opens a file with xml extension as an XML document``() =
    let document = container.``data.xml``.ReadAsXDocument()
    let value = document.Elements().First()
                        .Elements().First()
                        .Value
    test <@ value = "thing" @>

[<Fact>]
let ``Cloud Blob Client relates to the same data as the type provider``() =
   test <@ Local.Containers.CloudBlobClient.ListContainers()
           |> Seq.map(fun c -> c.Name)
           |> Set.ofSeq
           |> Set.contains "samples" @>

[<Fact>]
let ``Cloud Blob Container relates to the same data as the type provider``() =
    let client = container.AsCloudBlobContainer()
    let blobs = client.ListBlobs() |> Seq.choose(function | :? CloudBlockBlob as b -> Some b | _ -> None) |> Seq.map(fun c -> c.Name) |> Seq.toList
    test <@ blobs = [ "data.xml"; "file1.txt"; "file2.txt"; "file3.txt"; "sample.txt" ] @>

[<Fact>]
let ``CloudBlockBlob relates to the same data as the type provider``() =
    let blob = container.``data.xml``.AsCloudBlockBlob()
    test <@ blob.Name = "data.xml" @>

[<Fact>]
let ``Page Blobs are listed``() =
    container.``pageData.bin`` // compiles!

[<Fact>]
let ``Page Blobs support streams``() =
    test <@ container.``pageData.bin``.OpenStreamAsText().ReadToEnd().StartsWith "hello from page blob" @>

[<Fact>]
let ``CloudPageBlob relates to the same data as the type provider``() =
    let blob = container.``pageData.bin``.AsCloudPageBlob()
    test <@ blob.Name = "pageData.bin" @>

[<Fact>]
let ``Page Blobs calculate size correctly``() =
    test <@ container.``pageData.bin``.Size = 512L @>

let testFileDownload (blobFile:BlobFile) =
    let filename = Path.GetTempFileName()
    File.Delete filename
    blobFile.Download filename |> Async.RunSynchronously
    let predicates = 
        [ File.Exists
          FileInfo >> fun fi -> fi.Length = blobFile.Size ]
        |> List.map(fun p -> p filename)
    File.Delete filename
    predicates |> List.iter(fun item -> test <@ item @>)

[<Fact>]
let ``Can correctly download a block blob``() = testFileDownload container.``file1.txt``

[<Fact>]
let ``Can correctly download a page blob``() = testFileDownload container.``pageData.bin``

let testFolderDownload download expectedFiles expectedFolders =
    let tempFolder = Path.Combine(Path.GetTempPath(), "tpTestFolder")
    if Directory.Exists tempFolder then Directory.Delete(tempFolder, true)
    download tempFolder |> Async.RunSynchronously
    let files = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories) |> Seq.length
    let folders = Directory.GetDirectories(tempFolder, "*", SearchOption.AllDirectories) |> Seq.length
    Directory.Delete(tempFolder, true)
    test <@ files = expectedFiles @>
    test <@ folders = expectedFolders @>

[<Fact>]
let ``Can correctly download a folder``() = testFolderDownload container.``folder/``.Download 2 0

[<Fact>]
let ``Can correctly download a container``() = testFolderDownload container.Download 12 5

[<Fact>]
let ``Can access Path property on a folder`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    test <@ "folder2/child/" = childFolder.Path @>

[<Fact>]
let ``ListBlobs method returns correct number of blobs`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    let allBlobs = childFolder.ListBlobs()
    test <@ Seq.length allBlobs = 1 @>

[<Fact>]
let ``Can access List blobs method on a folder`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    let allBlobs = childFolder.ListBlobs(true)
    let count = allBlobs |> Seq.length
    test <@ count = 4 @>