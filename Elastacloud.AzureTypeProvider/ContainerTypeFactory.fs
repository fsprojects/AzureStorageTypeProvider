module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open System
open System.Reflection
open Samples.FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Elastacloud.FSharp.AzureTypeProvider.AzureRepository

let (|Text|Binary|) = 
    let textFileExtensions = [ ".xml"; ".txt"; ".csv" ]
    function 
    | (name : string) when textFileExtensions |> Seq.exists name.EndsWith -> Text
    | _ -> Binary

let private createFileProvidedType connectionString container fileName = 
    let fileProp = ProvidedTypeDefinition(fileName, Some typeof<obj>)
           
    fileProp.AddMember(match fileName with
                       | Text -> 
                           let methodBody = (fun _ -> <@@ AzureRepository.downloadText connectionString container fileName @@>)
                           ProvidedMethod
                               (methodName = "Download", parameters = [], returnType = typeof<Async<string>>, 
                                InvokeCode = methodBody, IsStaticMethod = true)
                       | Binary -> 
                           let methodBody = 
                               (fun (args : Expr list) -> <@@ AzureRepository.downloadData connectionString container fileName %%args.[0] @@>)
                           ProvidedMethod
                               (methodName = "Download", 
                                parameters = [ ProvidedParameter("destinationArray", typeof<byte []>) ], 
                                returnType = typeof<Async<int>>, InvokeCode = methodBody, IsStaticMethod = true))
    fileProp.AddMember
        (ProvidedMethod
             (methodName = "DownloadToFile", 
              parameters = [ ProvidedParameter("path", typeof<string>) ], returnType = typeof<Async<unit>>, 
              
              InvokeCode = (fun (args : Expr list) -> 
              <@@ AzureRepository.downloadToFile connectionString container fileName %%args.[0] @@>), IsStaticMethod = true))

    let uri, status = AzureRepository.getDetails connectionString container fileName
    fileProp.AddMember(ProvidedProperty(sprintf "Copy Status: %s" status, typeof<string>, GetterCode = (fun args -> <@@ status @@>), IsStatic = true))
    fileProp

/// Generates a property type for a specific container
let createContainerType (connectionString, (container : LightweightContainer)) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        container.GetFiles()
        |> Seq.map (fun file -> createFileProvidedType connectionString container.Name file)
        |> Seq.toList)
    individualContainerType
