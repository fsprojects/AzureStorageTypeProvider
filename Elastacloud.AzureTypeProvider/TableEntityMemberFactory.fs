/// Responsible for creating members on an individual table entity.
module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableEntityMemberFactory

open Elastacloud.FSharp.AzureTypeProvider
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

/// builds a single EDM parameter
let private buildEdmParameter edmType builder = 
    match edmType with
    | EdmType.Binary -> builder typeof<byte []>
    | EdmType.Boolean -> builder typeof<bool>
    | EdmType.DateTime -> builder typeof<DateTime>
    | EdmType.Double -> builder typeof<float>
    | EdmType.Guid -> builder typeof<Guid>
    | EdmType.Int32 -> builder typeof<int>
    | EdmType.Int64 -> builder typeof<int64>
    | EdmType.String -> builder typeof<string>
    | _ -> builder typeof<obj>

let buildParameter name buildType = ProvidedParameter(name, buildType)

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
    entityType.AddMemberDelayed
        (fun () -> 
        let parameters = 
            [ ProvidedParameter("PartitionKey", typeof<string>)
              ProvidedParameter("RowKey", typeof<string>) ] 
            @ [ for (name, entityProp) in properties |> Seq.sortBy fst -> buildEdmParameter entityProp.PropertyType (buildParameter name) ]
        ProvidedConstructor
            (parameters,              
             InvokeCode = fun args -> 
                 let fieldNames = 
                     properties
                     |> Seq.map fst
                     |> Seq.toList
                 
                 let fieldValues = 
                     args
                     |> Seq.skip 2
                     |> Seq.map(fun arg -> Expr.Coerce(arg, typeof<obj>))
                     |> Seq.toList
                 
                 <@@ buildTableEntity (%%args.[0]: string) (%%args.[1]: string) fieldNames 
                         (%%(Expr.NewArray(typeof<obj>, fieldValues))) @@>))
    properties

/// Gets all the members for a Table Entity type
let buildTableEntityMembers parentEntityType connection tableName = 
    let propertiesCreated = 
        tableName
        |> getRowsForSchema 5 connection
        |> setPropertiesForEntity parentEntityType
    
    let getPartition = 
        ProvidedMethod
            ("GetPartition", [ ProvidedParameter("key", typeof<string>); ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], parentEntityType.MakeArrayType(),
             InvokeCode = (fun args -> <@@ getPartitionRows %%args.[0] %%args.[1] tableName @@>), IsStaticMethod = true)
    getPartition.AddXmlDocDelayed <| fun _ -> "Eagerly retrieves all entities in a table partition by its key."
    let queryBuilderType, childTypes =  TableQueryBuilder.createTableQueryType parentEntityType connection tableName propertiesCreated
    let executeQuery = 
        ProvidedMethod
            ("Query", 
             [ ProvidedParameter("rawQuery", typeof<string>)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
             (parentEntityType.MakeArrayType()), 
             InvokeCode = (fun args -> <@@ executeQuery (%%args.[1]: string) tableName 0 (%%args.[0]: string) @@>), 
             IsStaticMethod = true)
    executeQuery.AddXmlDocDelayed <| fun _ -> "Executes a weakly-type query and returns the results in the shape for this table."
    let where = 
        ProvidedMethod
            ("Query", [], queryBuilderType, InvokeCode = (fun args -> <@@ ([]: string list) @@>), IsStaticMethod = true)
    where.AddXmlDocDelayed <| fun _ -> "Creates a strongly-typed query against the table."
    let getEntity = 
        ProvidedMethod
            ("Get", 
             [ ProvidedParameter("rowKey", typeof<string>)
               ProvidedParameter("partitionKey", typeof<string>, optionalValue = null)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
             (typeof<option<_>>).GetGenericTypeDefinition().MakeGenericType(parentEntityType),             
             InvokeCode = (fun args -> <@@ getEntity (%%args.[0]: string) (%%args.[1]: string) (%%args.[2]: string) tableName @@>), 
             IsStaticMethod = true)
    getEntity.AddXmlDocDelayed <| fun _ -> "Gets a single entity based on the row key and optionally the partition key. If more than one entity is returned, an exception is raised."
    let insertEntity = 
        ProvidedMethod
            ("Insert", 
             [ ProvidedParameter("entity", parentEntityType)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection)
               ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert) ], 
             returnType = typeof<TableResult>, 
             InvokeCode = (fun args -> <@@ insertEntity (%%args.[1]: string) tableName %%args.[2] (%%args.[0]: LightweightTableEntity) @@>),
             IsStaticMethod = true)
    insertEntity.AddXmlDocDelayed <| fun _ -> "Inserts a single entity with the inferred table schema into the table."

    let insertEntityObject = 
        ProvidedMethod
            ("Insert", 
             [ ProvidedParameter("partitionKey", typeof<string>)
               ProvidedParameter("rowKey", typeof<string>)
               ProvidedParameter("entity", typeof<obj>)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection)
               ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert) ], 
             returnType = typeof<TableResult>, InvokeCode = (fun args -> <@@ insertEntityObject %%args.[3] tableName %%args.[0] %%args.[1] %%args.[4] %%args.[2] @@>), IsStaticMethod = true)
    insertEntity.AddXmlDocDelayed <| fun _ -> "Inserts a single entity into the table, using public properties on the object as fields."
    
    let insertEntitiesObject = 
        ProvidedMethod
            ("Insert", 
             [ ProvidedParameter("entities", typeof<(string * string * obj) seq>)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection)
               ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert) ], 
             returnType = typeof<TableResult []>, InvokeCode = (fun args -> <@@ insertEntityObjectBatch %%args.[1] tableName %%args.[2] %%args.[0] @@>), IsStaticMethod = true)
    insertEntitiesObject.AddXmlDocDelayed <| fun _ -> "Inserts a batch of entities into the table, using all public properties on the object as fields."

    let deleteEntity =
        ProvidedMethod
            ("Delete",
             [ ProvidedParameter("entity", parentEntityType)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
             returnType = typeof<TableResult>, InvokeCode = (fun args -> <@@ deleteEntity %%args.[1] tableName %%args.[0] @@>), IsStaticMethod = true)
    deleteEntity.AddXmlDocDelayed <| fun _ -> "Deletes a single entity from the table."

    let deleteEntities =
        ProvidedMethod
            ("Delete",
             [ ProvidedParameter("entities", parentEntityType.MakeArrayType())
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
             returnType = typeof<TableResult seq>, InvokeCode = (fun args -> <@@ deleteEntities %%args.[1] tableName %%args.[0] @@>), IsStaticMethod = true)
    deleteEntities.AddXmlDocDelayed <| fun _ -> "Deletes a batch of entities from the table."

    let deleteEntitiesObject =
        ProvidedMethod
            ("Delete",
             [ ProvidedParameter("entities", typeof<(string * string) seq>)
               ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
             returnType = typeof<TableResult seq>, InvokeCode = (fun args -> <@@ deleteEntitiesTuple %%args.[1] tableName %%args.[0] @@>), IsStaticMethod = true)
    deleteEntitiesObject.AddXmlDocDelayed <| fun _ -> "Deletes a batch of entities from the table using the supplied pairs of Partition and Row keys."

    // Return back out all types and methods generated.
    queryBuilderType :: childTypes,
    [ getPartition; executeQuery; where; getEntity; insertEntity; insertEntityObject; insertEntitiesObject; deleteEntity; deleteEntities; deleteEntitiesObject ]