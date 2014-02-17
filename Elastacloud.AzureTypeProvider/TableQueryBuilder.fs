module internal Elastacloud.FSharp.AzureTypeProvider.MemberFactories.TableQueryBuilder

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open System
open System.Reflection

let private queryComparisons = typeof<QueryComparisons>.GetFields() |> Seq.map (fun f -> f.Name, f.GetValue(null) :?> string) |> Seq.cache

let private buildGenericProp<'a> (propertyOperatorsType:ProvidedTypeDefinition) parentQueryType propertyName =
    [ for compName, compValue in queryComparisons ->
        let invokeCode = fun (args:Expr list) -> <@@ buildFilter (propertyName, compValue, (%%args.[1]:'a)) :: ((%%args.[0]:obj) :?> string list) @@>
        let providedMethod = ProvidedMethod (compName, [ ProvidedParameter(propertyName.ToLower(), typeof<'a>) ], parentQueryType, InvokeCode = invokeCode)
        providedMethod.AddXmlDocDelayed <| fun _ -> (sprintf "Compares the %s property against the supplied value using the '%s' operator" propertyName compValue)
        providedMethod ]

let private buildCustomProp (propertyOperatorsType:ProvidedTypeDefinition) parentQueryType propertyName methodName exectedResult =
    let invoker = fun (args:Expr list) -> <@@ buildFilter(propertyName, QueryComparisons.Equal, exectedResult) :: ((%%args.[0]:obj) :?> string list) @@>
    ProvidedMethod(methodName, [], parentQueryType, InvokeCode = invoker )
    
/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType tableName propertyName propertyType parentQueryType = 
    let propertyOperatorsType = ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<obj>, HideObjectMethods = true)
    propertyOperatorsType.AddMembersDelayed(fun () -> match propertyType with
                                                      | EdmType.String -> buildGenericProp<string> propertyOperatorsType parentQueryType propertyName
                                                      | EdmType.Boolean -> [ buildCustomProp propertyOperatorsType parentQueryType propertyName "IsTrue" true
                                                                             buildCustomProp propertyOperatorsType parentQueryType propertyName "IsFalse" false ]
                                                      | EdmType.DateTime -> buildGenericProp<DateTime> propertyOperatorsType parentQueryType propertyName
                                                      | EdmType.Double -> buildGenericProp<float> propertyOperatorsType parentQueryType propertyName
                                                      | EdmType.Int32 -> buildGenericProp<int> propertyOperatorsType parentQueryType propertyName
                                                      | EdmType.Int64 -> buildGenericProp<int64> propertyOperatorsType parentQueryType propertyName
                                                      | EdmType.Guid -> buildGenericProp<Guid> propertyOperatorsType parentQueryType propertyName
                                                      | _ -> [])
    propertyOperatorsType

/// Creates a query property (and child methods etc.) for a given entity
let createTableQueryType (tableEntityType : ProvidedTypeDefinition) connection tableName (properties : seq<string * EntityProperty>) = 
    let tableQueryType = ProvidedTypeDefinition(tableName + "QueryPropertyBuilder", Some typeof<obj>, HideObjectMethods = true)
    let operatorTypes = [ for (name, value) in properties -> name, buildPropertyOperatorsType tableName name value.PropertyType tableQueryType ]
    let partitionKeyType = "PartitionKey", buildPropertyOperatorsType tableName "PartitionKey" EdmType.String tableQueryType
    let operatorTypes = partitionKeyType :: operatorTypes
    tableQueryType.AddMembersDelayed(fun () ->
        ProvidedMethod("Execute", [], tableEntityType.MakeArrayType(), InvokeCode = (fun args -> <@@ executeQuery connection tableName (composeAllFilters ((%%args.[0]:obj) :?> string list)) @@>)) :> MemberInfo ::
        [ for (name, operatorType) in operatorTypes -> ProvidedProperty(name, operatorType, GetterCode = (fun args -> <@@ (%%args.[0]:obj) :?> (string list) @@>)) :> MemberInfo ] )
    tableQueryType, operatorTypes |> List.unzip |> snd
