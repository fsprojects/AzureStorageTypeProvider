module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open Elastacloud.FSharp.AzureTypeProvider.AzureRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let rec private createFileItem connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, contents) -> 
        let folderProp = ProvidedTypeDefinition("/" + name, Some typeof<obj>)
        folderProp.AddMembersDelayed(fun _ -> 
            [] @ (contents()
                  |> Array.map (createFileItem connectionString containerName)
                  |> Array.toList))
        folderProp :> MemberInfo
    | Blob(path, name, properties) -> 
        let fileDetails = connectionString, containerName, path
        let fileProp = ProvidedTypeDefinition(name, Some typeof<obj>)
        fileProp.AddMembersDelayed(fun _ -> 
            [ MemberFactory.createFileDetailsProperty path properties :> MemberInfo
              MemberFactory.createDownloadFunction fileDetails :> MemberInfo
              MemberFactory.createDownloadFileFunction fileDetails :> MemberInfo
              MemberFactory.createGenerateSasFunction fileDetails :> MemberInfo ])
        fileProp :> MemberInfo

/// Generates a property type for a specific container
let createContainerType (connectionString, (container : LightweightContainer)) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        container.GetFiles()
        |> Seq.map (createFileItem connectionString container.Name)
        |> Seq.toList)
    individualContainerType.AddMember(MemberFactory.createUploadFileFunction (connectionString, container.Name))
    individualContainerType
