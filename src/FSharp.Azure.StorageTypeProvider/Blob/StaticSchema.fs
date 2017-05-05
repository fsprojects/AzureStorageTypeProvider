module internal FSharp.Azure.StorageTypeProvider.Blob.StaticSchema

open BlobRepository
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.WindowsAzure.Storage.Blob
open Newtonsoft.Json.Linq
open System.IO

let rec buildBlobItem prevPath (name:string, item:Json.Json) =
    match name.EndsWith "/", item with
    | false, Json.ObjectOrNull _ -> Blob (prevPath + name, name, BlobType.BlockBlob, None)
    | true , Json.ObjectOrNull o ->
        let path = prevPath + name
        Folder (path, name, lazy (o |> Array.map (buildBlobItem path)))
    | _ -> failwith "Invalid JSON structure in static blob schema."

let buildBlobSchema (json:Json.Json) =
    json.AsObject |> Array.map (fun (containerName, container) ->
        { Name = containerName
          Contents = lazy (container.AsObject |> Seq.map (buildBlobItem "")) })
    |> Array.toList

let createSchema resolutionFolder path =
    path
    |> Option.map(fun path ->
        let paths = [ path; Path.Combine(resolutionFolder, path) ]
        match paths |> List.tryFind File.Exists with
        | None -> Failure (exn (sprintf "Could not locate schema file. Searched: %A " paths))
        | Some file ->
            try
            file
            |> File.ReadAllText
            |> JToken.Parse
            |> Json.ofJToken
            |> buildBlobSchema
            |> Success
            with ex -> Failure ex)
    |> defaultArg <| Success []