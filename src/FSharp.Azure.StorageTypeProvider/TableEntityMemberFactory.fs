/// Responsible for creating members on an individual table entity.
module internal FSharp.Azure.StorageTypeProvider.Table.TableEntityMemberFactory

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open System

let private getDistinctProperties (tableEntities : #seq<DynamicTableEntity>) = 
    tableEntities
    |> Seq.collect (fun x -> x.Properties)
    |> Seq.distinctBy (fun x -> x.Key)
    |> Seq.map (fun x -> x.Key, x.Value)
    |> Seq.toList

/// Builds a property for a single entity for a specific type
let private buildEntityProperty<'a> key = 
    let getter = 
        fun (args : Expr list) -> 
            <@@ let entity = (%%args.[0] : LightweightTableEntity)
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
let setPropertiesForEntity (entityType : ProvidedTypeDefinition) (sampleEntities : #seq<DynamicTableEntity>) = 
    let properties = sampleEntities |> getDistinctProperties
    entityType.AddMembersDelayed(fun _ -> 
        properties
        |> Seq.map (fun (key, value) -> 
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
    entityType.AddMemberDelayed(fun () -> 
        let parameters = 
            [ ProvidedParameter("PartitionKey", typeof<string>)
              ProvidedParameter("RowKey", typeof<string>) ] 
            @ [ for (name, entityProp) in properties |> Seq.sortBy fst -> buildEdmParameter entityProp.PropertyType (buildParameter name) ]
        ProvidedConstructor(parameters, 
                            InvokeCode = fun args -> 
                                let fieldNames = 
                                    properties
                                    |> Seq.map fst
                                    |> Seq.toList
                                
                                let fieldValues = 
                                    args
                                    |> Seq.skip 2
                                    |> Seq.map (fun arg -> Expr.Coerce(arg, typeof<obj>))
                                    |> Seq.toList
                                
                                <@@ buildTableEntity (%%args.[0] : string) (%%args.[1] : string) fieldNames (%%(Expr.NewArray(typeof<obj>, fieldValues))) @@>))
    properties

/// Gets all the members for a Table Entity type
let buildTableEntityMembers (parentTableType:ProvidedTypeDefinition, parentTableEntityType, domainType:ProvidedTypeDefinition, connection, tableName) = 
    parentTableType.AddMembersDelayed(fun () ->
        let propertiesCreated = 
            tableName
            |> getRowsForSchema 5 connection
            |> setPropertiesForEntity parentTableEntityType
        match propertiesCreated with
        | [] -> []
        | _ -> 
            let getPartition = 
                ProvidedMethod
                    ("GetPartition", 
                     [ ProvidedParameter("key", typeof<string>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], parentTableEntityType.MakeArrayType(), 
                     InvokeCode = (fun args -> <@@ getPartitionRows %%args.[1] %%args.[2] tableName @@>))
            getPartition.AddXmlDocDelayed <| fun _ -> "Eagerly retrieves all entities in a table partition by its key."
            let queryBuilderType, childTypes = TableQueryBuilder.createTableQueryType parentTableEntityType connection tableName propertiesCreated
        
            let executeQuery = 
                ProvidedMethod
                    ("Query", 
                     [ ProvidedParameter("rawQuery", typeof<string>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], (parentTableEntityType.MakeArrayType()), 
                     InvokeCode = (fun args -> <@@ executeQuery (%%args.[2] : string) tableName 0 (%%args.[1] : string) @@>))
            executeQuery.AddXmlDocDelayed <| fun _ -> "Executes a weakly-type query and returns the results in the shape for this table."
            let buildQuery = ProvidedMethod("Query", [], queryBuilderType, InvokeCode = (fun args -> <@@ ([] : string list) @@>))
            buildQuery.AddXmlDocDelayed <| fun _ -> "Creates a strongly-typed query against the table."
            let getEntity = 
                ProvidedMethod
                    ("Get", 
                     [ ProvidedParameter("rowKey", typeof<string>)
                       ProvidedParameter("partitionKey", typeof<string>, optionalValue = null)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
                     (typeof<option<_>>).GetGenericTypeDefinition().MakeGenericType(parentTableEntityType), 
                     InvokeCode = (fun args -> <@@ getEntity (%%args.[1] : string) (%%args.[2] : string) (%%args.[3] : string) tableName @@>))
            getEntity.AddXmlDocDelayed 
            <| fun _ -> "Gets a single entity based on the row key and optionally the partition key. If more than one entity is returned, an exception is raised."
            let deleteEntity = 
                ProvidedMethod
                    ("Delete", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<TableResponse>, 
                     InvokeCode = (fun args -> <@@ deleteEntity %%args.[2] tableName %%args.[1] @@>))
            deleteEntity.AddXmlDocDelayed <| fun _ -> "Deletes a single entity from the table."
            let deleteEntities = 
                ProvidedMethod
                    ("Delete", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<(string * TableResponse []) []>, 
                     InvokeCode = (fun args -> <@@ deleteEntities %%args.[2] tableName %%args.[1] @@>))
            deleteEntities.AddXmlDocDelayed <| fun _ -> "Deletes a batch of entities from the table."
            let insertEntity = 
                ProvidedMethod
                    ("Insert", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<TableResponse>, 
                     InvokeCode = (fun args -> <@@ insertEntity (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity) @@>))
            insertEntity.AddXmlDocDelayed <| fun _ -> "Inserts a single entity with the inferred table schema into the table."
            let insertEntities = 
                ProvidedMethod
                    ("Insert", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<(string * TableResponse [])[]>, 
                     InvokeCode = (fun args -> <@@ insertEntityBatch (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity []) @@>))
            insertEntities.AddXmlDocDelayed <| fun _ -> "Inserts a batch of entities with the inferred table schema into the table."
            domainType.AddMembers(queryBuilderType :: childTypes)
            [ getPartition; getEntity; buildQuery; executeQuery; deleteEntity; deleteEntities; insertEntity; insertEntities ])