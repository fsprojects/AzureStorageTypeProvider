namespace global

///[omit]
module Option =
    open System
    let ofString text = if String.IsNullOrWhiteSpace text then None else Some text

///[omit]
module Async =
    let map mapper workflow =
        async {
            let! result = workflow
            return mapper result }

///[omit]
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Json =
    open Newtonsoft.Json.Linq
    type ObjectData = string * Json
    and Json =
        | String of string
        | Number of decimal
        | Boolean of bool
        | Object of ObjectData[]
        | Array of Json[]
        | Null
        member this.AsString = match this with Json.String s -> s | _ -> failwith "Expected a JSON string"
        member this.AsNumber = match this with Json.Number s -> s | _ -> failwith "Expected a JSON number"
        member this.AsBoolean = match this with Json.Boolean b -> b | _ -> failwith "Expected a JSON boolean"
        member this.AsObject = match this with Json.Object o -> o | _ -> failwith "Expected a JSON object"
        member this.AsArray = match this with Json.Array o -> o | _ -> failwith "Expected a JSON array"
        member this.TryGetProperty key =
            match this with
            | Json.Object data -> Some data
            | _ -> None
            |> Option.bind (Array.tryFind (fst >> (=) key))
            |> Option.map snd
        member this.GetProperty key =
            match this.TryGetProperty key with
            | Some property -> property
            | None -> failwithf "Property '%s' does not exist." key

    let rec ofJToken (jToken:JToken) =
        match jToken with
        | :? JValue as v ->
            match v.Value with
            | :? string as s -> Json.String s
            | :? decimal as d -> Json.Number (decimal d)
            | :? int64 as i -> Json.Number (decimal i)
            | :? int32 as i -> Json.Number (decimal i)
            | :? float as f -> Json.Number (decimal f)
            | :? bool as b -> Json.Boolean b
            | null -> Json.Null
            | v -> failwithf "Cannot convert JSON value type %s" (v.GetType().FullName)
        | :? JObject as o -> o.Properties() |> Seq.map (fun p -> p.Name, ofJToken p.Value) |> Seq.toArray |> Json.Object
        | :? JArray as a -> a.Values() |> Seq.map ofJToken |> Seq.toArray |> Json.Array
        | v -> failwithf "Cannot convert JSON type %s" (v.GetType().FullName)

    /// Match an Object and treat Null as an empty Object
    let (|ObjectOrNull|_|) = function
        | Json.Object o -> Some o
        | Json.Null -> Some [||]
        | _ -> None