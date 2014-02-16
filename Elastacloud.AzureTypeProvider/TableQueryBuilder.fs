module internal Elastacloud.FSharp.AzureTypeProvider.TableQueryBuilder

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open System
open System.Reflection

let private queryComparisons = typeof<QueryComparisons>.GetFields() |> Seq.map (fun f -> f.Name, f.GetValue(null) :?> string) |> Seq.cache

/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType tableName propertyName propertyType tableQueryType = 
    let propertyOperatorsType = ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<string * string>)
    let argType = 
        match propertyType with
        | EdmType.String -> Some (typeof<string>, fun fieldValue (args:Expr list) -> <@@ composeFilter((%%args.[0]:string * string),fieldValue,(%%args.[1] : string),TableQuery.GenerateFilterCondition) @@>)
//        | EdmType.Boolean -> [ ProvidedMethod("IsTrue", [], tableQueryType, InvokeCode = (fun args -> <@@ (%%args.[0]) @@>))
//                               ProvidedMethod("IsFalse", [], tableQueryType, InvokeCode = (fun args -> <@@ (%%args.[0]) @@>)) ]
//        | EdmType.DateTime -> Some typeof<DateTime>
        | EdmType.Double -> Some (typeof<float>, fun fieldValue (args:Expr list) -> <@@ composeFilter((%%args.[0]:string * string),fieldValue,(%%args.[1] : float),TableQuery.GenerateFilterConditionForDouble) @@>)
        | EdmType.Int32 -> Some (typeof<int32>, fun fieldValue (args:Expr list) -> <@@ composeFilter((%%args.[0]:string * string),fieldValue,(%%args.[1] : int32),TableQuery.GenerateFilterConditionForInt) @@>)
//        | EdmType.Int64 -> Some typeof<int64>
//        | EdmType.Guid -> Some typeof<System.Guid>
        | _ -> None
    match argType with
    | None -> ()
    | Some (propertyType, expressionGenerator) ->
        for (compName, compValue) in queryComparisons do
            propertyOperatorsType.AddMember
            <| ProvidedMethod (compName, [ ProvidedParameter("arg", propertyType) ], tableQueryType, InvokeCode = expressionGenerator(compValue) )
    propertyOperatorsType.HideObjectMethods <- true
    propertyOperatorsType

let createTableQueryType (domainType : ProvidedTypeDefinition) (tableEntityType : ProvidedTypeDefinition) connection tableName 
    (properties : seq<string * EntityProperty>) = 
    let tableQueryType = ProvidedTypeDefinition(tableName + "QueryPropertyBuilder", Some typeof<string>)
    tableQueryType.HideObjectMethods <- true
    for (name, value) in properties do
        let operatorsType = buildPropertyOperatorsType tableName name value.PropertyType tableQueryType
        domainType.AddMember operatorsType
        tableQueryType.AddMember <| ProvidedProperty(name, operatorsType, GetterCode = (fun args -> <@@ (%%args.[0]:string), name @@>))
    tableQueryType.AddMember <| ProvidedMethod("Execute", [], tableEntityType.MakeArrayType(), InvokeCode = (fun args -> <@@ executeQuery (%%args.[0]:string) connection tableName @@>))
    tableQueryType
