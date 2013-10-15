module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open Elastacloud.FSharp.AzureTypeProvider.AzureRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let private createFileProvidedType fileDetails = 
    let _, _, fileName = fileDetails
    let fileProp = ProvidedTypeDefinition(fileName, Some typeof<obj>)
    fileProp.AddMembersDelayed(fun _ -> [ MemberFactory.createDownloadFunction fileDetails :> MemberInfo
                                          MemberFactory.createDownloadFileFunction fileDetails :> MemberInfo
                                          MemberFactory.createCopyStatusProperty fileDetails :> MemberInfo ])
    fileProp

/// Generates a property type for a specific container
let createContainerType (connectionString, (container : LightweightContainer)) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        container.GetFiles()
        |> Seq.map (fun file -> createFileProvidedType (connectionString, container.Name, file))
        |> Seq.toList)
    individualContainerType.AddMember (MemberFactory.createUploadFileFunction(connectionString, container.Name))
    individualContainerType
