module FSharp.Azure.StorageTypeProvider.Table.StaticSchema

open FSharp.Azure.StorageTypeProvider.Configuration
open Newtonsoft.Json.Linq
open System.IO

module internal Parsed =
    open Microsoft.WindowsAzure.Storage.Table
    let parseEdmType (value:string) =
        match value.ToLower() with
        | "binary" -> EdmType.Binary
        | "boolean" -> EdmType.Boolean
        | "datetime" -> EdmType.DateTime
        | "double" -> EdmType.Double
        | "guid" -> EdmType.Guid
        | "int32" -> EdmType.Int32
        | "int64" -> EdmType.Int64
        | "string" -> EdmType.String
        | value -> failwithf "Unknown column type '%s'" value

    type Table = { Table : string; Columns : ColumnDefinition list }
    type TableSchema = { Tables : Table array }

let internal buildTableSchema rawJson =
    let json = rawJson |> JToken.Parse |> Json.ofJToken

    { Parsed.TableSchema.Tables =
        json.AsObject
        |> Array.map (fun (tableName, cols) ->
            { Table = tableName
              Columns =
                cols.AsObject
                |> Array.map (fun (colName, props) ->
                    let colType = Parsed.parseEdmType (props.GetProperty "Type").AsString
                    let propNeed =
                        props.TryGetProperty "Optional"
                        |> Option.map (fun b -> b.AsBoolean)
                        |> function Some true -> Optional | _ -> Mandatory
                    { Name = colName
                      ColumnType = colType
                      PropertyNeed = propNeed })
                |> Array.toList }) }

let internal createSchema resolutionFolder path =
    path
    |> Option.map(fun path ->
        let paths = [ path; Path.Combine(resolutionFolder, path) ]
        match paths |> List.tryFind File.Exists with
        | None -> Failure (exn (sprintf "Could not locate schema file. Searched: %A " paths))
        | Some file ->
            try
            file
            |> File.ReadAllText
            |> buildTableSchema
            |> Some
            |> Success
            with ex -> Failure ex)
    |> defaultArg <| Success None

