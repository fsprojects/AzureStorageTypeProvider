module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.BlobDownloadFunctionFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Xml.Linq

let private generateDownload<'a> (name, comment, verb) (methodBody, isAsync) = 
    let name = match name with
               | Some name -> name
               | None -> "Read"
    
    let style, name, returnType = 
        if isAsync then "asynchronously", name + "Async", typeof<Async<'a>>
        else "synchronously", name, typeof<'a>
    
    let verb = match verb with
               | Some verb -> verb
               | None -> "Read"

    let output = 
        ProvidedMethod
            (methodName = name, parameters = [], returnType = returnType, InvokeCode = methodBody, IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> sprintf "%s this blob %s as %s." verb style comment)
    output

let private getName named text = 
    if named then (Some text)
    else None

let generateDownloadAsTextFunctions(fileDetails, named) = 
    let connection, container, file = fileDetails
    let textDownload = generateDownload<string>(getName named "ReadAsString", "a string", None)
    [ textDownload((fun _ -> <@@ BlobRepository.downloadTextAsync connection container file @@>), true)
      textDownload((fun _ -> <@@ BlobRepository.downloadText connection container file @@>), false) ]

let generateDownloadAsXmlFunctions(fileDetails, named) = 
    let connection, container, file = fileDetails
    let xmlDownload = generateDownload<XDocument>(getName named "ReadAsXml", "an XDocument", None)
    [ xmlDownload((fun _ -> <@@ async { let! text = BlobRepository.downloadTextAsync connection container file
                                        return XDocument.Parse text } @@>), true)
      xmlDownload((fun _ -> <@@ (BlobRepository.downloadText connection container file) |> XDocument.Parse @@>), false) ]

let generateOpenFunctions(fileDetails) = 
    let connection, container, file = fileDetails
    [ generateDownload<IO.Stream> (Some "OpenStream", "a stream", Some "Opens") 
          ((fun _ -> <@@ BlobRepository.downloadStream connection container file @@>), false) ]
