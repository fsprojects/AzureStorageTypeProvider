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
                   ProvidedProperty(key, typeof<byte []>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> byte []
                                        else Unchecked.defaultof<byte[]> @@>))
               | EdmType.Boolean -> 
                   ProvidedProperty(key, typeof<bool>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> bool
                                        else Unchecked.defaultof<bool> @@>))
               | EdmType.DateTime -> 
                   ProvidedProperty(key, typeof<System.DateTime>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> System.DateTime
                                        else Unchecked.defaultof<System.DateTime> @@>))
               | EdmType.Double -> 
                   ProvidedProperty(key, typeof<double>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> float
                                        else Unchecked.defaultof<float> @@>))
               | EdmType.Guid -> 
                   ProvidedProperty(key, typeof<System.Guid>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> System.Guid
                                        else Unchecked.defaultof<System.Guid> @@>))
               | EdmType.Int32 -> 
                   ProvidedProperty(key, typeof<int>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> int
                                        else Unchecked.defaultof<int> @@>))
               | EdmType.Int64 -> 
                   ProvidedProperty(key, typeof<int64>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> int64
                                        else Unchecked.defaultof<int64> @@>))
               | EdmType.String -> 
                   ProvidedProperty(key, typeof<string>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key] :?> string
                                        else Unchecked.defaultof<string> @@>))
               | _ -> 
                   ProvidedProperty(key, typeof<obj>, 
                                    GetterCode = (fun args -> 
                                    <@@ if (%%args.[0] : LightweightTableEntity).Values.ContainsKey(key) then 
                                            (%%args.[0] : LightweightTableEntity).Values.[key]
                                        else Unchecked.defaultof<obj> @@>)))
        |> Seq.toList)
    properties
