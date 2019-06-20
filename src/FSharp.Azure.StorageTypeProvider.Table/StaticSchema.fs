module FSharp.Azure.StorageTypeProvider.Table.StaticSchema

open FSharp.Azure.StorageTypeProvider.Configuration
open Newtonsoft.Json.Linq
open System.IO

module internal Parsed =
    open Microsoft.Azure.Cosmos.Table
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
        |> Array.map (fun (tableName, columns) ->
            { Table = tableName
              Columns =
                columns.AsObject
                |> Array.map (fun (columnName, properties) ->
                    let colType = Parsed.parseEdmType (properties.GetProperty "Type").AsString
                    let propNeed =
                        match properties.TryGetProperty "Optional" with
                        | Some (Json.Boolean true) -> Optional
                        | Some (Json.Boolean false) | None -> Mandatory
                        | Some _ -> failwith "Optional must be a boolean value."
                    { Name = columnName
                      ColumnType = colType
                      PropertyNeed = propNeed })
                |> Array.toList }) }

let internal createSchema resolutionFolder path =
    path
    |> Option.map(fun path ->
        let paths = [ path; Path.Combine(resolutionFolder, path) ]
        match paths |> List.tryFind File.Exists with
        | None -> Error (exn (sprintf "Could not locate schema file. Searched: %A " paths))
        | Some file ->
            try
            file
            |> File.ReadAllText
            |> buildTableSchema
            |> Some
            |> Ok
            with ex -> Error ex)
    |> defaultArg <| Ok None

