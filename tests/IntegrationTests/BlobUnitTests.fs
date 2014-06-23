module FSharp.Azure.StorageTypeProvider.``Blob Unit Tests``

open FSharp.Azure.StorageTypeProvider
open Xunit
open System.Linq

type Local = AzureTypeProvider<"DevStorageAccount", "">

let container = Local.Containers.``tp-test``

[<Fact>]
let ``Correctly identifies blob containers``() =
    // compiles!
    Local.Containers.``tp-test``

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
    Assert.Equal(52L, container .``sample.txt``.Size)

[<Fact>]
let ``Reads a text file as text``() =
    let text = container .``sample.txt``.ReadAsString()
    Assert.Equal<string>("the quick brown fox jumped over the lazy dog\nbananas", text)

[<Fact>]
let ``Streams a text file line-by-line``() =
    let textStream = container .``sample.txt``.OpenStreamAsText()
    let text = seq { while not textStream.EndOfStream do
                        yield textStream.ReadLine() }
               |> Seq.toArray
    Assert.Equal<string>("the quick brown fox jumped over the lazy dog", text.[0])
    Assert.Equal<string>("bananas", text.[1])
    Assert.Equal(2, text.Length)

[<Fact>]
let ``Opens a file with xml extension as an XML document``() =
    let document = container.``data.xml``.ReadAsXDocument()
    let value = document.Elements().First()
                        .Elements().First()
                        .Value
    Assert.Equal<string>("thing", value)