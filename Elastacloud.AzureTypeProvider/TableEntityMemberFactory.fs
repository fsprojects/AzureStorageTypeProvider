/// Responsible for creating members on an individual table entity.
module Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository

let private getDistinctProperties (tableEntities : #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect (fun x -> x.Properties)
    |> Seq.distinctBy (fun x -> x.Key)

let asOption<'T when 'T:(new : unit -> 'T) and 'T:struct and 'T :> ValueType> (value:System.Nullable<'T>)  =
    if value.HasValue then Some value.Value else None
let toOption data = if data = null then None else Some data
let toDefault data defaultValue = if data = null then defaultValue else data

let setPropertiesForEntity (entityType:ProvidedTypeDefinition) (tableEntities:#seq<DynamicTableEntity>) = 
    let properties = tableEntities |> getDistinctProperties
    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map (fun prop ->
            let key = prop.Key
            match prop.Value.PropertyType with
            | EdmType.Binary -> ProvidedProperty(key, typeof<byte[] option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then Some ((%%args.[0]:LightweightTableEntity).Values.[key].BinaryValue) else None @@>))
            | EdmType.Boolean -> ProvidedProperty(key, typeof<bool option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].BooleanValue |> asOption else None @@>))
            | EdmType.DateTime -> ProvidedProperty(key, typeof<System.DateTimeOffset option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].DateTimeOffsetValue |> asOption else None @@>))
            | EdmType.Double -> ProvidedProperty(key, typeof<double option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].DoubleValue |> asOption else None @@>))
            | EdmType.Guid -> ProvidedProperty(key, typeof<System.Guid option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].GuidValue |> asOption else None @@>))
            | EdmType.Int32 -> ProvidedProperty(key, typeof<int option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].Int32Value |> asOption else None @@>))
            | EdmType.Int64 -> ProvidedProperty(key, typeof<int64 option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].Int64Value |> asOption else None @@>))
            | EdmType.String -> ProvidedProperty(key, typeof<string option>, GetterCode = (fun args -> <@@ if (%%args.[0]:LightweightTableEntity).Values.ContainsKey(key) then (%%args.[0]:LightweightTableEntity).Values.[key].StringValue |> toOption else None @@>))
            | _ -> ProvidedProperty(key, typeof<obj>, GetterCode = (fun args -> <@@ (%%args.[0]:LightweightTableEntity).Values.[key].PropertyAsObject @@>)))
        |> Seq.toList)