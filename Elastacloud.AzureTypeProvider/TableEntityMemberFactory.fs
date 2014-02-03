/// Responsible for creating members on an individual table entity.
module Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

let private getDistinctProperties (tableEntities : #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect (fun x -> x.Properties |> Seq.map (fun p -> p.Key))
    |> Seq.distinct

let setPropertiesForEntity (entityType:ProvidedTypeDefinition) (tableEntities:#seq<DynamicTableEntity>) = 
    let properties = tableEntities |> getDistinctProperties
    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map (fun key -> ProvidedProperty(key, typeof<obj>, GetterCode = (fun args -> <@@ (%%args.[0]:LightweightTableEntity).Values.[key] @@>)))
        |> Seq.toList)
        //(%%args.[0]).GetType().Name
        //((%%(args.[0]) :> obj) :?> LightweightTableEntity).Values.[key]