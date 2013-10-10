module Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open System
open System.Reflection
open Samples.FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Elastacloud.FSharp.AzureTypeProvider.AzureRepository

let private createFileType fileName =
    ProvidedProperty(propertyName = fileName,
                     propertyType = typeof<string>,
                     IsStatic = false,
                     GetterCode = (fun args -> <@@ fileName @@>))

let createContainerType(assembly,namespaceName,(container:LightweightContainer)) =
    let containerType = ProvidedTypeDefinition(assembly, namespaceName, "ContainerFileListing", baseType = Some typeof<obj>)
    containerType.AddMembers(container.Files |> Seq.map createFileType |> Seq.toList)
    let containerName = container.Name
    let property = ProvidedProperty(propertyName = container.Name,
                                    propertyType = containerType,
                                    IsStatic = false,
                                    GetterCode = (fun args -> <@@ containerName @@>))
    property, containerType

