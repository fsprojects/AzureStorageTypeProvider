#load @".paket\load\newtonsoft.json.fsx"

module Raw =
    type Column = { Column : string; Type : string }
    type Table = { Table : string; Columns : Column array }
    type Schema = { Tables : Table array }

module Parsed =
    type EdmType =
        | EdmBinary
        | EdmBoolean
        | EdmDateTime
        | EdmDouble
        | EdmGuid
        | EdmInt32
        | EdmInt64
        | EdmString
        static member Parse (value:string) =
            match value.ToLower() with
            | "binary" -> EdmBinary
            | "boolean" -> EdmBoolean
            | "datetime" -> EdmDateTime
            | "double" -> EdmDouble
            | "guid" -> EdmGuid
            | "int32" -> EdmInt32
            | "int64" -> EdmInt64
            | "string" -> EdmString
            | bad -> failwithf "Unknown column type '%s'" bad

    type Column = { Column : string; Type : EdmType }
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
                      Type = Parsed.EdmType.Parse c.Type }) }) }

    

