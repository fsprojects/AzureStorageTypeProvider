module internal FSharp.Azure.StorageTypeProvider.Table.TableQueryBuilder

open FSharp.Azure.StorageTypeProvider.Table.TableRepository
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open ProviderImplementation.ProvidedTypes
open System
open System.Reflection
open System.Text.RegularExpressions

let private queryComparisons = 
    typeof<QueryComparisons>.GetFields()
    |> Seq.map(fun f -> 
        let f, n = f.Name, f.GetValue(null) :?> string
        (if f.EndsWith("Equal") then f + "To" else f), n)
    |> Seq.cache
    
let private splitOnCaps text =
    Regex.Replace(text, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1").Trim()

let private buildGenericProp<'a> (ctx:ProvidedTypesContext) (propertyOperatorsType: ProvidedTypeDefinition) parentQueryType propertyName = 
    [ for compName, compValue in queryComparisons ->
          let invokeCode = fun (args: Expr list) -> <@@ buildFilter(propertyName, compValue, (%%args.[1]: 'a)) :: ((%%args.[0]: obj) :?> string list) @@>
          let providedMethod = ctx.ProvidedMethod(compName |> splitOnCaps, [ ctx.ProvidedParameter(propertyName.ToLower(), typeof<'a>) ], parentQueryType, invokeCode = invokeCode)
          providedMethod.AddXmlDocDelayed <| fun _ -> sprintf "Compares the %s property against the supplied value using the '%s' operator" propertyName compValue
          providedMethod ]

let private buildCustomProp (ctx:ProvidedTypesContext) (propertyOperatorsType: ProvidedTypeDefinition) parentQueryType propertyName methodName documentation exectedResult = 
    let invoker = fun (args: Expr list) -> <@@ buildFilter(propertyName, QueryComparisons.Equal, exectedResult) :: ((%%args.[0]: obj) :?> string list) @@>
    let providedMethod = ctx.ProvidedMethod(methodName, [], parentQueryType, invokeCode = invoker)
    providedMethod.AddXmlDocDelayed <| fun _ -> documentation
    providedMethod

/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType (ctx:ProvidedTypesContext) tableName propertyName propertyType parentQueryType = 
    let propertyOperatorsType = ctx.ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<obj>, hideObjectMethods = true)
    propertyOperatorsType.AddMembersDelayed(fun () -> 
        match propertyType with
        | EdmType.String -> buildGenericProp<string> ctx propertyOperatorsType parentQueryType propertyName
        | EdmType.Boolean -> 
            let buildDescription = sprintf "Tests whether %s is %s." propertyName
            [ buildCustomProp ctx propertyOperatorsType parentQueryType propertyName "True" (buildDescription "true") true
              buildCustomProp ctx propertyOperatorsType parentQueryType propertyName "False" (buildDescription "false") false ]
        | EdmType.DateTime -> buildGenericProp<DateTime> ctx propertyOperatorsType parentQueryType propertyName
        | EdmType.Double -> buildGenericProp<float> ctx propertyOperatorsType parentQueryType propertyName
        | EdmType.Int32 -> buildGenericProp<int> ctx propertyOperatorsType parentQueryType propertyName
        | EdmType.Int64 -> buildGenericProp<int64> ctx propertyOperatorsType parentQueryType propertyName
        | EdmType.Guid -> buildGenericProp<Guid> ctx propertyOperatorsType parentQueryType propertyName
        | _ -> [])
    propertyOperatorsType

/// Creates a query property (and child methods etc.) for a given entity
let createTableQueryType (tableEntityType: ProvidedTypeDefinition) (ctx:ProvidedTypesContext) connection tableName (columnDefinitions: ColumnDefinition seq) =
    let buildPropertyOperatorsType = buildPropertyOperatorsType ctx
    let tableQueryType = ctx.ProvidedTypeDefinition(tableName + "QueryBuilder", Some typeof<obj>, hideObjectMethods = true)
    let operatorTypes = [ "PartitionKey", buildPropertyOperatorsType tableName "PartitionKey" EdmType.String tableQueryType
                          "RowKey", buildPropertyOperatorsType tableName "RowKey" EdmType.String tableQueryType
                          "Timestamp", buildPropertyOperatorsType tableName "Timestamp" EdmType.DateTime tableQueryType ] @
                        [ for cd in columnDefinitions -> cd.Name, buildPropertyOperatorsType tableName cd.Name cd.ColumnType tableQueryType ]
    
    tableQueryType.AddMembersDelayed(fun () ->
        let executeQueryMethodAsync =
            ctx.ProvidedMethod
                ("ExecuteAsync", [ ctx.ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                                   ctx.ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 typeof<Async<_>>.GetGenericTypeDefinition().MakeGenericType(tableEntityType.MakeArrayType()),
                 invokeCode = (fun args -> <@@ executeQueryAsync (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) @@>))
        executeQueryMethodAsync.AddXmlDocDelayed <| fun _ -> "Executes the current query asynchronously."

        let executeQueryMethod =
            ctx.ProvidedMethod
                ("Execute", [ ctx.ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                              ctx.ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 tableEntityType.MakeArrayType(), 
                 invokeCode = (fun args -> <@@ executeQuery (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) @@>))
        executeQueryMethod.AddXmlDocDelayed <| fun _ -> "Executes the current query."
        
        let customQueryProperties = 
            [ for (name, operatorType) in operatorTypes -> 
                  let queryProperty = ctx.ProvidedProperty("Where" + name + "Is" |> splitOnCaps, operatorType, getterCode = (fun args -> <@@ (%%args.[0]: obj) :?> string list @@>))
                  queryProperty.AddXmlDocDelayed <| fun _ -> sprintf "Creates a query part for the %s property." name
                  queryProperty :> MemberInfo ]

        (executeQueryMethodAsync :> MemberInfo) ::
        (executeQueryMethod :> MemberInfo) ::
        customQueryProperties)
        
    tableQueryType, operatorTypes |> List.unzip |> snd