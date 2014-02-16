module internal Elastacloud.FSharp.AzureTypeProvider.TableQueryBuilder

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.WindowsAzure.Storage.Table
open Samples.FSharp.ProvidedTypes
open Elastacloud.FSharp.AzureTypeProvider.Repositories.TableRepository
open System
open System.Reflection

let private queryComparisons = typeof<QueryComparisons>.GetFields() |> Seq.map (fun f -> f.Name, f.GetValue(null) :?> string) |> Seq.cache

type QueryProperty =
| GenericProp of System.Type * (string -> Expr list -> Expr)
| CustomProp of (string * (Expr list -> Expr)) list
| None

/// Generates strongly-type query provided properties for an entity property e.g. Equal, GreaterThan etc. etc.
let private buildPropertyOperatorsType tableName propertyName propertyType parentQueryType = 
    let propertyOperatorsType = ProvidedTypeDefinition(sprintf "%s.%sQueryOperators" tableName propertyName, Some typeof<obj>)
    let argType = 
        match propertyType with
        | EdmType.String -> GenericProp (typeof<string>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:string)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | EdmType.Boolean -> CustomProp [ "IsTrue", (fun args -> <@@ buildFilter(propertyName, QueryComparisons.Equal, true) :: ((%%args.[0]:obj) :?> string list) @@>)
                                          "IsFalse", (fun args -> <@@ buildFilter(propertyName, QueryComparisons.Equal, false) :: ((%%args.[0]:obj) :?> string list) @@>) ]
        | EdmType.DateTime -> GenericProp (typeof<DateTimeOffset>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:DateTimeOffset)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | EdmType.Double -> GenericProp (typeof<float>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:float)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | EdmType.Int32 -> GenericProp (typeof<int32>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:int)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | EdmType.Int64 -> GenericProp (typeof<int64>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:int64)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | EdmType.Guid -> GenericProp (typeof<Guid>, fun fieldValue (args:Expr list) -> <@@ buildFilter (propertyName, fieldValue, (%%args.[1]:Guid)) :: ((%%args.[0]:obj) :?> string list) @@>)
        | _ -> None
    match argType with
    | None -> ()
    | GenericProp (propertyType, expressionGenerator) ->
        for (compName, compValue) in queryComparisons do
            propertyOperatorsType.AddMember
            <| ProvidedMethod (compName, [ ProvidedParameter("arg", propertyType) ], parentQueryType, InvokeCode = expressionGenerator(compValue) )
    | CustomProp props ->
        for (compName, invoker) in props do
            propertyOperatorsType.AddMember <| ProvidedMethod(compName, [], parentQueryType, InvokeCode = invoker )
    propertyOperatorsType.HideObjectMethods <- true
    propertyOperatorsType

let createTableQueryType (domainType : ProvidedTypeDefinition) (tableEntityType : ProvidedTypeDefinition) connection tableName 
    (properties : seq<string * EntityProperty>) = 
    let tableQueryType = ProvidedTypeDefinition(tableName + "QueryPropertyBuilder", Some typeof<obj>)
    tableQueryType.HideObjectMethods <- true
    for (name, value) in properties do
        let operatorsType = buildPropertyOperatorsType tableName name value.PropertyType tableQueryType
        domainType.AddMember operatorsType
        tableQueryType.AddMember <| ProvidedProperty(name, operatorsType, GetterCode = (fun args -> <@@ (%%args.[0]:obj) :?> (string list) @@>))
    tableQueryType.AddMember <| ProvidedMethod("Execute", [], tableEntityType.MakeArrayType(), InvokeCode = (fun args -> <@@ executeQuery (composeAllFilters ((%%args.[0]:obj) :?> string list)) connection tableName @@>))
    tableQueryType
