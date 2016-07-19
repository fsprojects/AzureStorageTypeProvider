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
    |> Seq.map(fun f -> f.Name, f.GetValue(null) :?> string)
    |> Seq.map(fun (f, n) -> (if f.EndsWith("Equal") then f + "To" else f), n)
    |> Seq.cache
    
let private splitOnCaps text =
    Regex.Replace(text, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1").Trim()

let private buildGenericProp<'a> (propertyOperatorsType: ProvidedTypeDefinition) parentQueryType propertyName = 
    [ for compName, compValue in queryComparisons ->
          let invokeCode = fun (args: Expr list) -> <@@ buildFilter(propertyName, compValue, (%%args.[1]: 'a)) :: ((%%args.[0]: obj) :?> string list) @@>
          let providedMethod = ProvidedMethod(compName |> splitOnCaps, [ ProvidedParameter(propertyName.ToLower(), typeof<'a>) ], parentQueryType, InvokeCode = invokeCode)
          providedMethod.AddXmlDocDelayed <| fun _ -> sprintf "Compares the %s property against the supplied value using the '%s' operator" propertyName compValue
          providedMethod ]

let private buildCustomProp (propertyOperatorsType: ProvidedTypeDefinition) parentQueryType propertyName methodName documentation exectedResult = 
    let invoker = fun (args: Expr list) -> <@@ buildFilter(propertyName, QueryComparisons.Equal, exectedResult) :: ((%%args.[0]: obj) :?> string list) @@>
    let providedMethod = ProvidedMethod(methodName, [], parentQueryType, InvokeCode = invoker)
    providedMethod.AddXmlDocDelayed <| fun _ -> documentation
    providedMethod

/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType tableName propertyName propertyType parentQueryType = 
    let propertyOperatorsType = ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<obj>, HideObjectMethods = true)
    propertyOperatorsType.AddMembersDelayed(fun () -> 
        match propertyType with
        | EdmType.String -> buildGenericProp<string> propertyOperatorsType parentQueryType propertyName
        | EdmType.Boolean -> 
            let buildDescription = sprintf "Tests whether %s is %s." propertyName
            [ buildCustomProp propertyOperatorsType parentQueryType propertyName "True" (buildDescription "true") true
              buildCustomProp propertyOperatorsType parentQueryType propertyName "False" (buildDescription "false") false ]
        | EdmType.DateTime -> buildGenericProp<DateTime> propertyOperatorsType parentQueryType propertyName
        | EdmType.Double -> buildGenericProp<float> propertyOperatorsType parentQueryType propertyName
        | EdmType.Int32 -> buildGenericProp<int> propertyOperatorsType parentQueryType propertyName
        | EdmType.Int64 -> buildGenericProp<int64> propertyOperatorsType parentQueryType propertyName
        | EdmType.Guid -> buildGenericProp<Guid> propertyOperatorsType parentQueryType propertyName
        | _ -> [])
    propertyOperatorsType

/// Creates a query property (and child methods etc.) for a given entity
let createTableQueryType (tableEntityType: ProvidedTypeDefinition) connection tableName (properties: seq<string * EntityProperty>) =
    let tableQueryType = ProvidedTypeDefinition(tableName + "QueryBuilder", Some typeof<obj>, HideObjectMethods = true)
    let operatorTypes = [ "PartitionKey", buildPropertyOperatorsType tableName "PartitionKey" EdmType.String tableQueryType
                          "RowKey", buildPropertyOperatorsType tableName "RowKey" EdmType.String tableQueryType
                          "Timestamp", buildPropertyOperatorsType tableName "Timestamp" EdmType.DateTime tableQueryType ] @
                        [ for (name, value) in properties -> name, buildPropertyOperatorsType tableName name value.PropertyType tableQueryType ]
    tableQueryType.AddMembersDelayed(fun () ->        
        let executeQueryMethodAsync =
            let symbKind = (typeof<Async<_>> |> SymbolKind.Generic)
            let retType = ProvidedSymbolType( symbKind,[tableEntityType.MakeArrayType()]) :> Type
            ProvidedMethod
                ("ExecuteAsync", [ ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                                   ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 retType, 
                 InvokeCode = (fun args -> <@@ executeQueryAsync (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) @@>))
        executeQueryMethodAsync.AddXmlDocDelayed <| fun _ -> "Executes the current query asynchronously."

        let executeQueryMethod =
            ProvidedMethod
                ("Execute", [ ProvidedParameter("maxResults", typeof<int>, optionalValue = 0)
                              ProvidedParameter("connectionString", typeof<string>, optionalValue = connection) ],
                 tableEntityType.MakeArrayType(), 
                 InvokeCode = (fun args -> <@@ executeQuery (%%args.[2] : string) tableName %%args.[1] (composeAllFilters((%%args.[0]: obj) :?> string list)) @@>))
        executeQueryMethod.AddXmlDocDelayed <| fun _ -> "Executes the current query."
        
        let customQueryProperties = 
            [ for (name, operatorType) in operatorTypes -> 
                  let queryProperty = ProvidedProperty("Where" + name + "Is" |> splitOnCaps, operatorType, GetterCode = (fun args -> <@@ (%%args.[0]: obj) :?> string list @@>))
                  queryProperty.AddXmlDocDelayed <| fun _ -> sprintf "Creates a query part for the %s property." name
                  queryProperty :> MemberInfo ]

        (executeQueryMethodAsync :> MemberInfo) ::
        (executeQueryMethod :> MemberInfo) ::
        customQueryProperties)
        
    tableQueryType, operatorTypes |> List.unzip |> snd