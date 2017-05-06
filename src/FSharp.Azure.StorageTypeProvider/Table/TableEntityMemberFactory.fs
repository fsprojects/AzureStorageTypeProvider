/// Responsible for creating members on an individual table entity.
module internal FSharp.Azure.StorageTypeProvider.Table.TableEntityMemberFactory

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open ProviderImplementation.ProvidedTypes
open System
open System.Text

let private getPropsForEntity (entity:DynamicTableEntity) =
    entity.Properties
    |> Seq.filter(fun p -> p.Value.PropertyAsObject |> isNull |> not)
    |> Seq.map(fun p -> p.Key, p.Value.PropertyType)
    |> Set

let private toDistinctColumnDefinitions tableEntities = 
    let optionalProperties, mandatoryProperties, _ =
        ((Set [], Set [], true), tableEntities)
        ||> Seq.fold(fun (optionals, mandatory, initialRun) entity ->
            if initialRun then
                optionals, entity, false
            else
                let optionals = (mandatory - entity) + (entity - mandatory) + optionals
                let mandatory = Set.intersect mandatory entity
                optionals, mandatory, false)

    (optionalProperties |> Set.map(fun (name, edmType) -> name, edmType, Optional)) +
    (mandatoryProperties |> Set.map(fun (name, edmType) -> name, edmType, Mandatory))
    |> Set.toList
    |> List.map(fun (name, edmType, need) -> { Name = name; ColumnType = edmType; PropertyNeed = need })
    |> List.sortBy(fun columnDef -> columnDef.Name)

let humanizeRegex =
    RegularExpressions.Regex (@"(?<=[A-Z])(?=[A-Z][a-z]) |(?<=[^A-Z])(?=[A-Z]) |(?<=[A-Za-z])(?=[^A-Za-z])", RegularExpressions.RegexOptions.IgnorePatternWhitespace)

/// Builds a property for a single entity for a specific type
let private buildEntityProperty<'a> humanize key need = 
    let getter, propType = 
        match need with
        | Mandatory ->
            fun (args : Expr list) -> 
                <@@ let entity = (%%args.[0] : LightweightTableEntity)
                    if entity.Values.ContainsKey key then entity.Values.[key] :?> 'a
                    else Unchecked.defaultof<'a> @@>
            , typeof<'a>
        | Optional ->
            fun (args : Expr list) -> 
                <@@ let entity = (%%args.[0] : LightweightTableEntity)
                    if entity.Values.ContainsKey key then Some(entity.Values.[key] :?> 'a)
                    else None @@>
            , typeof<Option<'a>>
    
    let propName = if humanize then humanizeRegex.Replace(key, " ") else key
    let prop = ProvidedProperty(propName, propType, GetterCode = getter)
    prop.AddXmlDocDelayed <| fun _ -> (sprintf "Returns the value of the '%s' property" propName)
    prop

/// builds a single EDM parameter
let private buildEdmParameter edmType builder = 
    match edmType with
    | EdmType.String -> builder typeof<string>
    | EdmType.Binary -> builder typeof<byte []>
    | EdmType.Boolean -> builder typeof<bool>
    | EdmType.DateTime -> builder typeof<DateTime>
    | EdmType.Double -> builder typeof<float>
    | EdmType.Guid -> builder typeof<Guid>
    | EdmType.Int32 -> builder typeof<int>
    | EdmType.Int64 -> builder typeof<int64>
    | _ -> builder typeof<obj>

/// Sets the properties on a specific entity based on the inferred schema from the sample provided
let setPropertiesForEntity humanize (entityType : ProvidedTypeDefinition) columnDefinitions = 
    entityType.AddMembersDelayed(fun _ -> 
        columnDefinitions
        |> Seq.map (fun { Name = name; ColumnType = columnType; PropertyNeed = propertyNeed } -> 
               match columnType with
               | EdmType.String -> buildEntityProperty<string> humanize name propertyNeed
               | EdmType.Binary -> buildEntityProperty<byte []> humanize name propertyNeed
               | EdmType.Boolean -> buildEntityProperty<bool> humanize name propertyNeed
               | EdmType.DateTime -> buildEntityProperty<DateTime> humanize name propertyNeed
               | EdmType.Double -> buildEntityProperty<float> humanize name propertyNeed
               | EdmType.Guid -> buildEntityProperty<Guid> humanize name propertyNeed
               | EdmType.Int32 -> buildEntityProperty<int> humanize name propertyNeed
               | EdmType.Int64 -> buildEntityProperty<int64> humanize name propertyNeed
               | _ -> buildEntityProperty<obj> humanize name propertyNeed)
        |> Seq.toList)
    
    let buildParameter name need buildType =
        match need with
        | Mandatory -> ProvidedParameter(name, buildType)
        | Optional ->
            let optionOfBuildType = typeof<Option<_>>.GetGenericTypeDefinition().MakeGenericType(buildType)
            ProvidedParameter(name, optionOfBuildType, optionalValue = None)

    // Build a constructor as well.
    entityType.AddMemberDelayed(fun () -> 
        // Split into mandatory and optional parameters - we add the mandatory ones first.
        let mandatoryParams, optionalParams = columnDefinitions |> List.partition(fun r -> r.PropertyNeed = Mandatory)
        let parameters = 
            [ ProvidedParameter("PartitionKey", typeof<Partition>)
              ProvidedParameter("RowKey", typeof<Row>) ] 
            @ [ for { Name = name; ColumnType = columnType; PropertyNeed = need } in mandatoryParams -> buildEdmParameter columnType (buildParameter name need) ]
            @ [ for { Name = name; ColumnType = columnType; PropertyNeed = need } in optionalParams -> buildEdmParameter columnType (buildParameter name need) ]
        ProvidedConstructor(
            parameters, 
            InvokeCode = fun args ->
                let fieldValues = 
                    args
                    |> Seq.skip 2
                    |> Seq.map (fun arg -> Expr.Coerce(arg, typeof<obj>))
                    |> Seq.toList

                let fieldNames =
                    (mandatoryParams @ optionalParams)
                    |> Seq.take fieldValues.Length
                    |> Seq.map(fun r -> r.Name)
                    |> Seq.toList

                <@@ buildTableEntity (%%args.[0] : Partition) (%%args.[1] : Row) fieldNames (%%(Expr.NewArray(typeof<obj>, fieldValues))) @@>))

let generateSchema tableName schemaInferenceRowCount connection =
    let sampleEntities = tableName |> getRowsForSchema schemaInferenceRowCount connection
    sampleEntities
    |> Seq.map getPropsForEntity
    |> toDistinctColumnDefinitions

/// Gets all the members for a Table Entity type
let buildTableEntityMembers columnDefinitions humanize (parentTableType:ProvidedTypeDefinition, parentTableEntityType:ProvidedTypeDefinition, domainType:ProvidedTypeDefinition, connection, tableName) = 
    columnDefinitions |> setPropertiesForEntity humanize parentTableEntityType
    
    parentTableType.AddMembersDelayed(fun () ->
        match columnDefinitions with
        | [] -> []
        | _ -> 
            let getPartition = 
                ProvidedMethod
                    ("GetPartition", 
                     [ ProvidedParameter("key", typeof<string>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], parentTableEntityType.MakeArrayType(), 
                     InvokeCode = (fun args -> <@@ getPartitionRows %%args.[1] %%args.[2] tableName @@>))
            getPartition.AddXmlDocDelayed <| fun _ -> "Eagerly retrieves all entities in a table partition by its key."
            
            let getPartitionAsync = 
                ProvidedMethod
                    ("GetPartitionAsync", 
                     [ ProvidedParameter("key", typeof<string>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                       typeof<Async<_>>.GetGenericTypeDefinition().MakeGenericType(parentTableEntityType.MakeArrayType()),
                       InvokeCode = (fun args -> <@@ getPartitionRowsAsync %%args.[1] %%args.[2] tableName @@>))
            getPartitionAsync.AddXmlDocDelayed <| fun _ -> "Asynchronously retrieves all entities in a table partition by its key."
            
            let queryBuilderType, childTypes = TableQueryBuilder.createTableQueryType parentTableEntityType connection tableName columnDefinitions
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
                     [ ProvidedParameter("rowKey", typeof<Row>)
                       ProvidedParameter("partitionKey", typeof<Partition>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
                     (typeof<Option<_>>).GetGenericTypeDefinition().MakeGenericType(parentTableEntityType), 
                     InvokeCode = (fun args -> <@@ getEntity (%%args.[1] : Row) (%%args.[2] : Partition) (%%args.[3] : string) tableName @@>))
            getEntity.AddXmlDocDelayed <| fun _ -> "Gets a single entity based on the row and partition key."
            let getEntityAsync = 
                let returnType =
                    let optionType = typeof<Option<_>>.GetGenericTypeDefinition().MakeGenericType(parentTableEntityType)
                    typeof<Async<_>>.GetGenericTypeDefinition().MakeGenericType(optionType)
                ProvidedMethod
                    ("GetAsync", 
                     [ ProvidedParameter("rowKey", typeof<Row>)
                       ProvidedParameter("partitionKey", typeof<Partition>)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], 
                     returnType, 
                     InvokeCode = (fun args -> <@@ getEntityAsync (%%args.[1] : Row) (%%args.[2] : Partition) (%%args.[3] : string) tableName @@>))
            getEntityAsync.AddXmlDocDelayed <| fun _ -> "Gets a single entity based on the row and partition key asynchronously."
            let deleteEntity = 
                ProvidedMethod
                    ("Delete", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<TableResponse>, 
                     InvokeCode = (fun args -> <@@ deleteEntity %%args.[2] tableName %%args.[1] @@>))
            deleteEntity.AddXmlDocDelayed <| fun _ -> "Deletes a single entity from the table."
            let deleteEntityAsync = 
                ProvidedMethod
                    ("DeleteAsync", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<Async<TableResponse>>, 
                     InvokeCode = (fun args -> <@@ deleteEntityAsync %%args.[2] tableName %%args.[1] @@>))
            deleteEntityAsync.AddXmlDocDelayed <| fun _ -> "Deletes a single entity from the table asynchronously."
            let deleteEntities = 
                ProvidedMethod
                    ("Delete", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<(string * TableResponse []) []>, 
                     InvokeCode = (fun args -> <@@ deleteEntities %%args.[2] tableName %%args.[1] @@>))
            deleteEntities.AddXmlDocDelayed <| fun _ -> "Deletes a batch of entities from the table."
            let deleteEntitiesAsync = 
                ProvidedMethod
                    ("DeleteAsync", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<Async<(string * TableResponse []) []>>, 
                     InvokeCode = (fun args -> <@@ deleteEntitiesAsync %%args.[2] tableName %%args.[1] @@>))
            deleteEntitiesAsync.AddXmlDocDelayed <| fun _ -> "Deletes a batch of entities from the table asynchronously."
            let insertEntity = 
                ProvidedMethod
                    ("Insert", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<TableResponse>, 
                     InvokeCode = (fun args -> <@@ insertEntity (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity) @@>))
            insertEntity.AddXmlDocDelayed <| fun _ -> "Inserts a single entity with the inferred table schema into the table."
            let insertEntityAsync = 
                ProvidedMethod
                    ("InsertAsync", 
                     [ ProvidedParameter("entity", parentTableEntityType)
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<Async<TableResponse>>, 
                     InvokeCode = (fun args -> <@@ insertEntityAsync (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity) @@>))
            insertEntityAsync.AddXmlDocDelayed <| fun _ -> "Asynchronously inserts a single entity with the inferred table schema into the table."
            let insertEntities = 
                ProvidedMethod
                    ("Insert", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<(string * TableResponse [])[]>, 
                     InvokeCode = (fun args -> <@@ insertEntityBatch (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity []) @@>))
            insertEntities.AddXmlDocDelayed <| fun _ -> "Inserts a batch of entities with the inferred table schema into the table."
            let insertEntitiesAsync = 
                ProvidedMethod
                    ("InsertAsync", 
                     [ ProvidedParameter("entities", parentTableEntityType.MakeArrayType())
                       ProvidedParameter("insertMode", typeof<TableInsertMode>, optionalValue = TableInsertMode.Insert)
                       ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ], returnType = typeof<Async<(string * TableResponse [])[]>>, 
                     InvokeCode = (fun args -> <@@ insertEntityBatchAsync (%%args.[3] : string) tableName %%args.[2] (%%args.[1] : LightweightTableEntity []) @@>))
            insertEntitiesAsync.AddXmlDocDelayed <| fun _ -> "Asynchronously inserts a batch of entities with the inferred table schema into the table."
            
            domainType.AddMembers(queryBuilderType :: childTypes)
            
            [ getPartition; getPartitionAsync
              getEntity; getEntityAsync
              buildQuery
              executeQuery
              deleteEntity; deleteEntityAsync
              deleteEntities; deleteEntitiesAsync
              insertEntity; insertEntityAsync
              insertEntities; insertEntitiesAsync ])