module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactory

open Elastacloud.FSharp.AzureTypeProvider.AzureRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection
open System.Xml.Linq

let private (|Text|Binary|XML|) (name : string) = 
    let endsWith extension = name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)
    match name with
    | _ when [ ".txt"; ".csv" ] |> Seq.exists endsWith -> Text
    | _ when endsWith ".xml" -> XML
    | _ -> Binary

let createDownloadFunction fileDetails = 
    let connectionString, container, fileName = fileDetails
    
    let methodBody, returnType, comment = 
        match fileName with
        | Text -> 
            let methodBody = (fun _ -> <@@ AzureRepository.downloadText connectionString container fileName @@>)
            methodBody, typeof<Async<string>>, "a string"
        | XML -> 
            let methodBody = (fun _ -> <@@ async { let! text = AzureRepository.downloadText connectionString container 
                                                                   fileName
                                                   return XDocument.Parse text } @@>)
            methodBody, typeof<Async<XDocument>>, "an XDocument"
        | Binary -> 
            let methodBody = 
                (fun (args : Expr list) -> <@@ AzureRepository.downloadData connectionString container fileName @@>)
            methodBody, typeof<Async<Byte []>>, "a byte array"
    
    let output = 
        ProvidedMethod
            (methodName = "Download", parameters = [], returnType = returnType, InvokeCode = methodBody, 
             IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> sprintf "Downloads this file asynchronously as %s." comment)
    output

let createDownloadFileFunction fileDetails = 
    let connectionString, container, fileName = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "DownloadToFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
             returnType = typeof<Async<unit>>, 
             
             InvokeCode = (fun (args : Expr list) -> 
             <@@ AzureRepository.downloadToFile connectionString container fileName %%args.[0] @@>), 
             IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Downloads this file to the local file system asynchronously.")
    output

let createUploadFileFunction fileDetails = 
    let connectionString, container = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "UploadFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
             returnType = typeof<Async<unit>>, 
             
             InvokeCode = (fun (args : Expr list) -> 
             <@@ AzureRepository.uploadFile connectionString container %%args.[0] @@>), IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Uploads a file to this container.")
    output

let createGenerateSasFunction fileDetails = 
    let connectionString, container, fileName = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "GenerateSharedAccessSignature", 
             parameters = [ ProvidedParameter("duration", typeof<TimeSpan>) ], returnType = typeof<Uri>, 
             
             InvokeCode = (fun (args : Expr list) -> 
             <@@ AzureRepository.getSas connectionString container fileName %%args.[0] @@>), IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Generates a full-access shared access signature URI for this blob.")
    output

let getFolderDetailsProperties folderDetails = 
    let connectionString, container, folder = folderDetails
    let files, folderSize = 123, 1200
    ProvidedProperty
         ((sprintf "Files: %d, Folder size: %dgb" files folderSize), typeof<int * int>, IsStatic = true, 
         GetterCode = (fun args -> <@@ files, folderSize @@>))

let createFileDetailsProperty fileDetails = 
    let connectionString, container, fileName = fileDetails
    let uri, properties = AzureRepository.getFileDetails connectionString container fileName
    let output = 
        let unit,getSize = match properties.Length with
                           | _ when properties.Length < 1024L -> "B", (fun x -> x)
                           | _ when properties.Length < 1048576L -> "KB", (fun x -> x / 1024.0)
                           | _ when properties.Length < 1073741824L -> "MB", (fun x -> x / 1048576.0)
                           | _ when properties.Length < 1099511627776L -> "GB", (fun x -> x / 1073741824.0)
                           | _ -> "TB", (fun x -> x / 1099511627776.0)
        let sizeText = String.Format("{0:0.0} {1}", getSize (float properties.Length), unit)
        ProvidedProperty
            ((sprintf "%s (%s)" (properties.BlobType.ToString()) sizeText), typeof<string>, GetterCode = (fun args -> <@@ uri @@>), 
             IsStatic = true)
    output.AddXmlDocDelayed(fun () -> "Gives you basic details on this file in Azure. The property evaluates to the full URI of this blob.")
    output
