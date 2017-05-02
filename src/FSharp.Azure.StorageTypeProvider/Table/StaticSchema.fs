module FSharp.Azure.StorageTypeProvider.Table.StaticSchema

open FSharp.Azure.StorageTypeProvider.Configuration
open Newtonsoft.Json
open System.IO

module Raw =
    type Column = { Column : string; Type : string; Optional : bool }
    type Table = { Table : string; Columns : Column array }
    type Schema = { Tables : Table array }

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
    let schema = JsonConvert.DeserializeObject<Raw.Schema>(rawJson)

    { Parsed.TableSchema.Tables =
        schema.Tables
        |> Array.map(fun t ->
            { Table = t.Table
              Columns =
                t.Columns
                |> Array.map(fun c ->
                    { Name = c.Column
                      ColumnType = Parsed.parseEdmType c.Type
                      PropertyNeed = if c.Optional then Optional else Mandatory })
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

