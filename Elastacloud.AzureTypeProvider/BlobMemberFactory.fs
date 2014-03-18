/// Responsible for generating invidual member functions for a single blob.
module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.BlobMemberFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories
open Elastacloud.FSharp.AzureTypeProvider.MemberFactories.BlobDownloadFunctionFactory
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Blob
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection
open System.Xml.Linq

let private (|Text|Binary|XML|)(name: string) = 
    let endsWith extension = name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)
    match name with
    | _ when [ ".txt"; ".csv" ] |> Seq.exists endsWith -> Text
    | _ when endsWith ".xml" -> XML
    | _ -> Binary

let createDownloadFunctions fileDetails = 
    let _, _, fileName = fileDetails
    (match fileName with
     | Text -> generateDownloadAsTextFunctions(fileDetails, false)
     | XML -> generateDownloadAsXmlFunctions(fileDetails, false)
     | Binary -> generateDownloadAsTextFunctions(fileDetails, true))
     @ generateOpenFunctions(fileDetails)
     |> Seq.cast<MemberInfo>
     |> Seq.toList

let createDownloadFileFunction fileDetails = 
    let connection, container, file = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "DownloadToFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
             returnType = typeof<Async<unit>>, 
             
             InvokeCode = (fun (args: Expr list) -> 
             <@@ BlobRepository.downloadToFile connection container file %%args.[0] @@>), IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Downloads this file to the local file system asynchronously.")
    output

let private createDownloadMultipleFilesFunction folderDetails methodName comment = 
    let connectionString, container, folderPath = folderDetails
    let output = 
        ProvidedMethod
            (methodName = methodName, parameters = [ ProvidedParameter("path", typeof<string>) ], 
             returnType = typeof<Async<unit>>, 
             
             InvokeCode = (fun (args: Expr list) -> 
             <@@ BlobRepository.downloadFolder connectionString container folderPath %%args.[0] @@>), 
             IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> comment)
    output

let createDownloadFolderFunction(folderDetails) = 
    createDownloadMultipleFilesFunction folderDetails "DownloadFolder" 
        "Downloads the entire folder contents to the local file system asynchronously."
let createDownloadContainerFunction(connectionString, container) = 
    createDownloadMultipleFilesFunction (connectionString, container, String.Empty) "DownloadContainer" 
        "Downloads the entire container contents to the local file system asynchronously."

let createUploadFileFunction fileDetails = 
    let connectionString, container = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "UploadFile", parameters = [ ProvidedParameter("path", typeof<string>) ], 
             returnType = typeof<Async<unit>>, 
             
             InvokeCode = (fun (args: Expr list) -> 
             <@@ BlobRepository.uploadFile connectionString container %%args.[0] @@>), IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Uploads a file to this container.")
    output

let createGenerateSasFunction fileDetails = 
    let connection, container, file = fileDetails
    let output = 
        ProvidedMethod
            (methodName = "GenerateSharedAccessSignature", 
             parameters = [ ProvidedParameter("duration", typeof<TimeSpan>) ], returnType = typeof<Uri>, 
             InvokeCode = (fun (args: Expr list) -> <@@ BlobRepository.getSas connection container file %%args.[0] @@>), 
             IsStaticMethod = true)
    output.AddXmlDocDelayed(fun () -> "Generates a full-access shared access signature URI for this blob.")
    output

let createFileDetailsProperty path (properties: BlobProperties) = 
    let output = 
        let unit, getSize = 
            match properties.Length with
            | _ when properties.Length < 1024L -> "B", (fun x -> x)
            | _ when properties.Length < 1048576L -> "KB", (fun x -> x / 1024.0)
            | _ when properties.Length < 1073741824L -> "MB", (fun x -> x / 1048576.0)
            | _ when properties.Length < 1099511627776L -> "GB", (fun x -> x / 1073741824.0)
            | _ -> "TB", (fun x -> x / 1099511627776.0)
        
        let sizeText = String.Format("{0:0.0} {1}", getSize(float properties.Length), unit)
        ProvidedProperty
            ((sprintf "%s (%s)" (properties.BlobType.ToString()) sizeText), typeof<string>, 
             GetterCode = (fun args -> <@@ path @@>), IsStatic = true)
    output.AddXmlDocDelayed
        (fun () -> "Gives you basic details on this file in Azure. The property evaluates to the path of this blob.")
    output
