module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open System
open System.Reflection
open Samples.FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Elastacloud.FSharp.AzureTypeProvider.AzureRepository

let private createFileProperty fileName = 
    ProvidedProperty
        (propertyName = fileName, propertyType = typeof<obj>, IsStatic = true, 
         GetterCode = (fun args -> <@@ null @@>))

/// Generates a property type for a specific container
let createContainerType (container : LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        container.GetFiles()
        |> Seq.map createFileProperty
        |> Seq.toList)
    individualContainerType
