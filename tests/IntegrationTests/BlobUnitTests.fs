module FSharp.Azure.StorageTypeProvider.``Blob Unit Tests``

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
    container .``sample.txt``.Size =! 190L

[<Fact>]
let ``Reads a text file as text``() =
    let text = container .``sample.txt``.Read()
    text =! "the quick brown fox jumps over the lazy dog\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada.\nLorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor."

[<Fact>]
let ``Streams a text file line-by-line``() =
    let text = container .``sample.txt``.ReadLines() |> Seq.toArray

    text.[0] =! "the quick brown fox jumps over the lazy dog"
    text.[1] =! "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras malesuada."
    text.[2] =! "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nulla porttitor."
    text.Length =! 3

[<Fact>]
let ``Opens a file with xml extension as an XML document``() =
    let document = container.``data.xml``.ReadAsXDocument()
    let value = document.Elements().First()
                        .Elements().First()
                        .Value
    value =! "thing"

[<Fact>]
let ``Cloud Blob Client relates to the same data as the type provider``() =
    (Local.Containers.CloudBlobClient.ListContainers()
     |> Seq.map(fun c -> c.Name)
     |> Set.ofSeq
     |> Set.contains "samples") =! true

[<Fact>]
let ``Cloud Blob Container relates to the same data as the type provider``() =
    let client = container.AsCloudBlobContainer()
    let blobs = client.ListBlobs() |> Seq.choose(function | :? CloudBlockBlob as b -> Some b | _ -> None) |> Seq.map(fun c -> c.Name) |> Seq.toList
    blobs =! [ "data.xml"; "file1.txt"; "file2.txt"; "file3.txt"; "sample.txt" ]

[<Fact>]
let ``CloudBlockBlob relates to the same data as the type provider``() =
    let blob = container.``data.xml``.AsCloudBlockBlob()
    blob.Name =! "data.xml"

[<Fact>]
let ``Page Blobs are listed``() =
    container.``pageData.bin`` // compiles!

[<Fact>]
let ``Page Blobs support streams``() =
    container.``pageData.bin``.OpenStreamAsText().ReadToEnd().StartsWith "hello from page blob" =! true

[<Fact>]
let ``CloudPageBlob relates to the same data as the type provider``() =
    let blob = container.``pageData.bin``.AsCloudPageBlob()
    blob.Name =! "pageData.bin"

[<Fact>]
let ``Page Blobs calculate size correctly``() =
    container.``pageData.bin``.Size =! 512L

let testFileDownload (blobFile:BlobFile) =
    let filename = Path.GetTempFileName()
    File.Delete filename
    blobFile.Download filename |> Async.RunSynchronously
    let predicates = 
        [ File.Exists
          FileInfo >> fun fi -> fi.Length = blobFile.Size ]
        |> List.map(fun p -> p filename)
    File.Delete filename
    predicates |> List.iter((=!) true)

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
    files =! expectedFiles
    folders =! expectedFolders

[<Fact>]
let ``Can correctly download a folder``() = testFolderDownload container.``folder/``.Download 2 0

[<Fact>]
let ``Can correctly download a container``() = testFolderDownload container.Download 12 5

[<Fact>]
let ``Can access Path property on a folder`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    Assert.Equal<string>("folder2/child/", childFolder.Path)

[<Fact>]
let ``ListBlobs method returns correct number of blobs`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    let allBlobs = childFolder.ListBlobs()
    let count = allBlobs |> Seq.length
    Assert.Equal(1,count)

[<Fact>]
let ``Can access List blobs method on a folder`` = 
    let childFolder = Local.Containers.samples.``folder2/``.``child/``
    let allBlobs = childFolder.ListBlobs(true)
    let count = allBlobs |> Seq.length
    Assert.Equal(4,count)