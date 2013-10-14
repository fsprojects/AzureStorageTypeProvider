module internal Elastacloud.FSharp.AzureTypeProvider.FileMemberFactory

open Elastacloud.FSharp.AzureTypeProvider.AzureRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let private (|Text|Binary|) = 
    let textFileExtensions = [ ".xml"; ".txt"; ".csv" ]
    function 
    | (name : string) when textFileExtensions |> Seq.exists name.EndsWith -> Text
    | _ -> Binary

let createDownloadFunction fileDetails = 
    let connectionString,container,fileName = fileDetails
    match fileName with
        | Text -> 
            let methodBody = (fun _ -> <@@ AzureRepository.downloadText connectionString container fileName @@>)
            ProvidedMethod
                (methodName = "Download", parameters = [], returnType = typeof<Async<string>>, InvokeCode = methodBody, 
                IsStaticMethod = true)
        | Binary -> 
            let methodBody = 
                (fun (args : Expr list) -> 
                <@@ AzureRepository.downloadData connectionString container fileName @@>)
            ProvidedMethod
                (methodName = "Download", parameters = [], 
                returnType = typeof<Async<Byte[]>>, InvokeCode = methodBody, IsStaticMethod = true)

let createDownloadFileFunction fileDetails = 
    let connectionString,container,fileName = fileDetails
    ProvidedMethod
        (methodName = "DownloadToFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
        returnType = typeof<Async<unit>>, 
              
        InvokeCode = (fun (args : Expr list) -> 
        <@@ AzureRepository.downloadToFile connectionString container fileName %%args.[0] @@>), 
        IsStaticMethod = true)

let CreateCopyStatusProperty fileDetails =
    let connectionString,container,fileName = fileDetails
    let uri, status = AzureRepository.getDetails connectionString container fileName
    ProvidedProperty
            (sprintf "Copy Status: %s" status, typeof<string>, GetterCode = (fun args -> <@@ status @@>), 
            IsStatic = true)
