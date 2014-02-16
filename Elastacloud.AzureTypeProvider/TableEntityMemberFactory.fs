/// Responsible for creating members on an individual table entity.
module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open Elastacloud.FSharp.AzureTypeProvider.Utils
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let private getDistinctProperties (tableEntities : #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect (fun x -> x.Properties)
    |> Seq.distinctBy (fun x -> x.Key)
    |> Seq.map(fun x -> x.Key, x.Value)

/// Creates a type for a partition for a specific entity
let createPartitionType (tableEntityType : ProvidedTypeDefinition) connectionString tableName = 
    let partitionType = ProvidedTypeDefinition(tableName + "Partition", Some typeof<obj>)
    partitionType.HideObjectMethods <- true
    let dataProperty = ProvidedMethod("GetAll", [], tableEntityType.MakeArrayType())
    dataProperty.InvokeCode <- (fun args -> <@@ getPartitionRows ((%%args.[0] : obj) :?> string) connectionString tableName @@>)
    partitionType.AddMember dataProperty
    partitionType

let setPropertiesForEntity (entityType : ProvidedTypeDefinition) (tableEntities : #seq<DynamicTableEntity>) = 
    let properties = tableEntities |> getDistinctProperties

    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map (fun (key,value) -> 
               match value.PropertyType with
               | EdmType.Binary -> 
                   ProvidedProperty(key, typeof<byte [] option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> byte [])
                                        else None @@>))
               | EdmType.Boolean -> 
                   ProvidedProperty(key, typeof<bool option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> bool)
                                        else None @@>))
               | EdmType.DateTime -> 
                   ProvidedProperty(key, typeof<System.DateTime option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> System.DateTime)
                                        else None @@>))
               | EdmType.Double -> 
                   ProvidedProperty(key, typeof<double option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> float)
                                        else None @@>))
               | EdmType.Guid -> 
                   ProvidedProperty(key, typeof<System.Guid option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> System.Guid)
                                        else None @@>))
               | EdmType.Int32 -> 
                   ProvidedProperty(key, typeof<int option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> int)
                                        else None @@>))
               | EdmType.Int64 -> 
                   ProvidedProperty(key, typeof<int64 option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> int64)
                                        else None @@>))
               | EdmType.String -> 
                   ProvidedProperty(key, typeof<string option>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key] :?> string)
                                        else None @@>))
               | _ -> 
                   ProvidedProperty(key, typeof<obj>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            Some((%%args.[0] : LightweightTableEntity).Values.[key])
                                        else None @@>)))
        |> Seq.toList)
    properties
