/// Responsible for creating members on an individual table entity.
module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open Elastacloud.FSharp.AzureTypeProvider.Utils
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let private getDistinctProperties(tableEntities: #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect(fun x -> x.Properties)
    |> Seq.distinctBy(fun x -> x.Key)
    |> Seq.map(fun x -> x.Key, x.Value)
    |> Seq.toList

/// Creates a type for a partition for a specific entity
let private createPartitionType (tableEntityType: ProvidedTypeDefinition) connection tableName = 
    let partitionType = ProvidedTypeDefinition(tableName + "Partition", Some typeof<obj>)
    partitionType.HideObjectMethods <- true
    let dataProperty = 
        ProvidedMethod
            ("GetAll", [ ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
             tableEntityType.MakeArrayType(), 
             
             InvokeCode = (fun args -> 
             <@@ getPartitionRows ((%%args.[0]: obj) :?> string) (%%args.[1]: string) tableName @@>))
    dataProperty.AddXmlDocDelayed <| fun _ -> "Eagerly returns all of the entities in this partition."
    partitionType.AddMember dataProperty
    partitionType

/// Builds a property for a single entity for a specific type
let private buildEntityProperty<'a> key = 
    let getter = 
        fun (args: Expr list) -> 
            <@@ let entity = (%%args.[0]: LightweightTableEntity)
                if entity.Values.ContainsKey(key) then entity.Values.[key] :?> 'a
                else Unchecked.defaultof<'a> @@>
    
    let prop = ProvidedProperty(key, typeof<'a>, GetterCode = getter)
    prop.AddXmlDocDelayed <| fun _ -> (sprintf "Returns the value of the %s property" key)
    prop

/// Sets the properties on a specific entity based on the inferred schema from the sample provided
let setPropertiesForEntity (entityType: ProvidedTypeDefinition) (sampleEntities: #seq<DynamicTableEntity>) = 
    let properties = sampleEntities |> getDistinctProperties
    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map(fun (key, value) -> 
               match value.PropertyType with
               | EdmType.Binary -> buildEntityProperty<byte []> key
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
let buildTableEntityMembers parentEntityType connection tableName = 
    let propertiesCreated = 
        tableName
        |> getRowsForSchema 5 connection
        |> setPropertiesForEntity parentEntityType
    
    let partitionType = createPartitionType parentEntityType connection tableName
    let queryBuilderType, childTypes = 
        TableQueryBuilder.createTableQueryType parentEntityType connection tableName propertiesCreated
    let getPartition = 
        ProvidedMethod
            ("GetPartition", [ ProvidedParameter("key", typeof<string>) ], partitionType, 
             InvokeCode = (fun args -> <@@ (%%args.[0]: string) @@>), IsStaticMethod = true)
    getPartition.AddXmlDocDelayed <| fun _ -> "Retrieves a table partition by its key."
    let executeQuery = 
        ProvidedMethod
            ("ExecuteQuery", 
             [ ProvidedParameter("rawQuery", typeof<string>)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
             (parentEntityType.MakeArrayType()), 
             InvokeCode = (fun args -> <@@ executeQuery (%%args.[1]: string) tableName (%%args.[0]: string) @@>), 
             IsStaticMethod = true)
    executeQuery.AddXmlDocDelayed 
    <| fun _ -> "Executes a weakly-type query and returns the results in the shape for this table."
    let where = 
        ProvidedMethod
            ("Where", [], queryBuilderType, InvokeCode = (fun args -> <@@ ([]: string list) @@>), IsStaticMethod = true)
    where.AddXmlDocDelayed <| fun _ -> "Begins creating a strongly-typed query against the table."
    let getEntity = 
        ProvidedMethod
            ("GetEntity", 
             [ ProvidedParameter("rowKey", typeof<string>)
               ProvidedParameter("partitionKey", typeof<string>, optionalValue = null)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
             (typeof<option<_>>).GetGenericTypeDefinition().MakeGenericType(parentEntityType), 
             
             InvokeCode = (fun args -> 
             <@@ getEntity (%%args.[0]: string) (%%args.[1]: string) (%%args.[2]: string) tableName @@>), 
             IsStaticMethod = true)
    getEntity.AddXmlDocDelayed <| fun _ -> "Gets a single entity based on the row key and optionally the partition key."
    [ partitionType;queryBuilderType ] @ childTypes, [ getPartition;executeQuery;where;getEntity ]
