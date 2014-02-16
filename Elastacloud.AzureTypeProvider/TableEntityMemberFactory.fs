/// Responsible for creating members on an individual table entity.
module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open Elastacloud.FSharp.AzureTypeProvider.Utils
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection
open Microsoft.FSharp.Quotations

let private getDistinctProperties (tableEntities : #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect (fun x -> x.Properties)
    |> Seq.distinctBy (fun x -> x.Key)
    |> Seq.map(fun x -> x.Key, x.Value)
    |> Seq.toList

/// Creates a type for a partition for a specific entity
let private createPartitionType (tableEntityType : ProvidedTypeDefinition) connectionString tableName = 
    let partitionType = ProvidedTypeDefinition(tableName + "Partition", Some typeof<obj>)
    partitionType.HideObjectMethods <- true
    let dataProperty = ProvidedMethod("GetAll", [], tableEntityType.MakeArrayType())
    dataProperty.InvokeCode <- (fun args -> <@@ getPartitionRows ((%%args.[0] : obj) :?> string) connectionString tableName @@>)
    partitionType.AddMember dataProperty
    partitionType

/// Builds a property for a single entity for a specific type
let private buildEntityProperty<'a> key =
    let getter = fun (args:Expr list) -> <@@ let entity = (%%args.[0] : LightweightTableEntity)
                                             if entity.Values.ContainsKey(key) then entity.Values.[key] :?> 'a
                                             else Unchecked.defaultof<'a> @@>
    ProvidedProperty(key, typeof<'a>, GetterCode = getter)

/// Sets the properties on a specific entity based on the inferred schema from the sample provided
let setPropertiesForEntity (entityType : ProvidedTypeDefinition) (sampleEntities : #seq<DynamicTableEntity>) = 
    let properties = sampleEntities |> getDistinctProperties
    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map (fun (key,value) -> 
               match value.PropertyType with
               | EdmType.Binary -> buildEntityProperty<byte[]> key
               | EdmType.Boolean -> buildEntityProperty<bool> key
               | EdmType.DateTime -> buildEntityProperty<DateTime> key
               | EdmType.Double -> buildEntityProperty<float> key
               | EdmType.Guid -> buildEntityProperty<Guid> key
               | EdmType.Int32 -> buildEntityProperty<int> key
               | EdmType.Int64 -> buildEntityProperty<int64> key
               | EdmType.String -> buildEntityProperty<string> key
               | _ -> buildEntityProperty<obj> key)
        |> Seq.toList)
    properties

/// Gets all the members for a Table Entity type
let buildTableEntityMembers parentEntityType connectionString tableName =
    let propertiesCreated = 
        tableName
        |> getRowsForSchema 5 connectionString
        |> setPropertiesForEntity parentEntityType

    let partitionType = createPartitionType parentEntityType connectionString tableName
    let queryBuilderType, childTypes = TableQueryBuilder.createTableQueryType parentEntityType connectionString tableName propertiesCreated

    [ partitionType; queryBuilderType ] @ childTypes,
    [ ProvidedMethod
        ("GetPartition", [ ProvidedParameter("key", typeof<string>) ], partitionType, 
        InvokeCode = (fun args -> <@@ (%%args.[0] : string) @@>), IsStaticMethod = true)
          
      ProvidedMethod
        ("ExecuteQuery", [ ProvidedParameter("rawQuery", typeof<string>) ], (parentEntityType.MakeArrayType()), 
        InvokeCode = (fun args -> <@@ executeQuery connectionString tableName (%%args.[0] : string) @@>), 
        IsStaticMethod = true)
          
      ProvidedMethod
        ("Where", [], queryBuilderType, InvokeCode = (fun args -> <@@ ([] : string list) @@>), IsStaticMethod = true)
        
      ProvidedMethod
        ("GetEntity", 
        [ ProvidedParameter("key", typeof<string>)
          ProvidedParameter("partition", typeof<string>, optionalValue = null) ], (typeof<option<_>>).GetGenericTypeDefinition().MakeGenericType(parentEntityType), InvokeCode = (fun args -> <@@ getEntity (%%args.[0] : string) (%%args.[1] : string) connectionString tableName @@>), IsStaticMethod = true) ]
