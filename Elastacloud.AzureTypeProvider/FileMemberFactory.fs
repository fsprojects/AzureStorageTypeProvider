module internal Elastacloud.FSharp.AzureTypeProvider.FileMemberFactory

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
    
    let methodBody, returnType = 
        match fileName with
        | Text -> 
            let methodBody = 
                (fun _ -> <@@ AzureRepository.downloadText connectionString container fileName @@>)
            methodBody, typeof<Async<string>>
        | XML -> 
            let methodBody = (fun _ -> <@@ async { let! text = AzureRepository.downloadText 
                                                                   connectionString container 
                                                                   fileName
                                                   return XDocument.Parse text } @@>)
            methodBody, typeof<Async<XDocument>>
        | Binary -> 
            let methodBody = 
                (fun (args : Expr list) -> 
                <@@ AzureRepository.downloadData connectionString container fileName @@>)
            methodBody, typeof<Async<Byte []>>
    ProvidedMethod
        (methodName = "Download", parameters = [], returnType = returnType, InvokeCode = methodBody, 
         IsStaticMethod = true)

let createDownloadFileFunction fileDetails = 
    let connectionString, container, fileName = fileDetails
    ProvidedMethod
        (methodName = "DownloadToFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
         returnType = typeof<Async<unit>>, 
         
         InvokeCode = (fun (args : Expr list) -> 
         <@@ AzureRepository.downloadToFile connectionString container fileName %%args.[0] @@>), 
         IsStaticMethod = true)

let CreateCopyStatusProperty fileDetails = 
    let connectionString, container, fileName = fileDetails
    let uri, status = AzureRepository.getDetails connectionString container fileName
    ProvidedProperty
        (sprintf "Copy Status: %s" status, typeof<string>, GetterCode = (fun args -> <@@ status @@>), 
         IsStatic = true)
