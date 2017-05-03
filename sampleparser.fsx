#load @".paket\load\windowsazure.storage.fsx"

module Raw =
    type Column = { Column : string; Type : string; Optional : bool }
    type Table = { Table : string; Columns : Column array }
    type Schema = { Tables : Table array }

module Parsed =
    open System
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

    type Column = { Column : string; Type : EdmType; Optional : bool }
    type Table = { Table : string; Columns : Column array }
    type Schema = { Tables : Table array }

open Newtonsoft.Json

let schema =
    let schema = JsonConvert.DeserializeObject<Raw.Schema>(System.IO.File.ReadAllText "table-schema.json")

    { Parsed.Schema.Tables =
        schema.Tables
        |> Array.map(fun t ->
            { Table = t.Table
              Columns =
                t.Columns
                |> Array.map(fun c ->
                    { Column = c.Column
                      Type = Parsed.parseEdmType c.Type
                      Optional = c.Optional }) }) }