module internal FSharp.Azure.StorageTypeProvider.Table.TableQueryBuilder

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.Azure.Cosmos.Table
open ProviderImplementation.ProvidedTypes
open System
open System.Reflection
open System.Text.RegularExpressions

let private queryComparisons = 
    typeof<QueryComparisons>.GetFields()
    |> Seq.map(fun field ->
        let name = field.Name
        (if name.EndsWith("Equal") then name + "To" else name), field.GetValue(null) :?> string)
    |> Seq.cache
    
let private splitOnCaps text =
    Regex.Replace(text, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1").Trim()

let private buildGenericProp<'a> parentQueryType propertyName = 
    [ for compName, compValue in queryComparisons ->
          let invokeCode = fun (args: Expr list) -> <@@ buildFilter(propertyName, compValue, (%%args.[1]: 'a)) :: ((%%args.[0]: obj) :?> string list) @@>
          let providedMethod = ProvidedMethod(compName |> splitOnCaps, [ ProvidedParameter(propertyName.ToLower(), typeof<'a>) ], parentQueryType, invokeCode = invokeCode)
          providedMethod.AddXmlDocDelayed <| fun _ -> sprintf "Compares the %s property against the supplied value using the '%s' operator" propertyName compValue
          providedMethod ]

let private buildCustomProp parentQueryType propertyName methodName documentation exectedResult = 
    let invoker = fun (args: Expr list) -> <@@ buildFilter(propertyName, QueryComparisons.Equal, exectedResult) :: ((%%args.[0]: obj) :?> string list) @@>
    let providedMethod = ProvidedMethod(methodName, [], parentQueryType, invokeCode = invoker)
    providedMethod.AddXmlDocDelayed <| fun _ -> documentation
    providedMethod

/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType tableName propertyName propertyType parentQueryType = 
    let propertyOperatorsType = ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<obj>, hideObjectMethods = true)
    propertyOperatorsType.AddMembersDelayed(fun () -> 
        match propertyType with
        | EdmType.String -> buildGenericProp<string> parentQueryType propertyName
        | EdmType.Boolean -> 
            let buildDescription = sprintf "Tests whether %s is %s." propertyName
            [ buildCustomProp parentQueryType propertyName "True" (buildDescription "true") true
              buildCustomProp parentQueryType propertyName "False" (buildDescription "false") false ]
        | EdmType.DateTime -> buildGenericProp<DateTime> parentQueryType propertyName
        | EdmType.Double -> buildGenericProp<float> parentQueryType propertyName
        | EdmType.Int32 -> buildGenericProp<int> parentQueryType propertyName
        | EdmType.Int64 -> buildGenericProp<int64> parentQueryType propertyName
        | EdmType.Guid -> buildGenericProp<Guid> parentQueryType propertyName
        | _ -> [])
    propertyOperatorsType

/// Creates a query property (and child methods etc.) for a given entity
let createTableQueryType (tableEntityType: ProvidedTypeDefinition) connection tableName (columnDefinitions: ColumnDefinition seq) =
    let tableQueryType = ProvidedTypeDefinition(tableName + "QueryBuilder", Some typeof<obj>, hideObjectMethods = true)
    let operatorTypes = [ "PartitionKey", buildPropertyOperatorsType tableName "PartitionKey" EdmType.String tableQueryType
                          "RowKey", buildPropertyOperatorsType tableName "RowKey" EdmType.String tableQueryType
                          "Timestamp", buildPropertyOperatorsType tableName "Timestamp" EdmType.DateTime tableQueryType ] @
                        [ for cd in columnDefinitions -> cd.Name, buildPropertyOperatorsType tableName cd.Name cd.ColumnType tableQueryType ]
    
    tableQueryType.AddMembersDelayed(fun () ->
        let executeQueryMethodAsync =
            ProvidedMethod
                ("ExecuteAsync", [ ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                                   ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 typeof<Async<_>>.GetGenericTypeDefinition().MakeGenericType(tableEntityType.MakeArrayType()),
                 invokeCode = (fun args -> <@@ executeQueryAsync (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) @@>))
        executeQueryMethodAsync.AddXmlDocDelayed <| fun _ -> "Executes the current query asynchronously."

        let executeQueryMethod =
            ProvidedMethod
                ("Execute", [ ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                              ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 tableEntityType.MakeArrayType(), 
                 invokeCode = (fun args -> <@@ executeQueryAsync (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) |> Async.RunSynchronously @@>))
        executeQueryMethod.AddXmlDocDelayed <| fun _ -> "Executes the current query."
        
        let customQueryProperties = 
            [ for (name, operatorType) in operatorTypes -> 
                  let queryProperty = ProvidedProperty("Where" + name + "Is" |> splitOnCaps, operatorType, getterCode = (fun args -> <@@ (%%args.[0]: obj) :?> string list @@>))
                  queryProperty.AddXmlDocDelayed <| fun _ -> sprintf "Creates a query part for the %s property." name
                  queryProperty :> MemberInfo ]

        (executeQueryMethodAsync :> MemberInfo) ::
        (executeQueryMethod :> MemberInfo) ::
        customQueryProperties)
        
    tableQueryType, operatorTypes |> List.unzip |> snd