module internal FSharp.Azure.StorageTypeProvider.Blob.StaticSchema

open BlobRepository
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.Azure.Storage.Blob
open Newtonsoft.Json.Linq
open System.IO

let failInvalidJson() = failwith "Invalid JSON structure in static blob schema."

let private (|Folder|File|) (name:string) =
    if name.EndsWith "/" then Folder else File

let rec buildBlobItem prevPath (elementName, contents) =
    match elementName, contents with
    | File, Json.ObjectOrNull _ ->
        let blobType =
            match contents.TryGetProperty "Type" with
            | Some (Json.String "blockblob") -> BlobType.BlockBlob
            | Some (Json.String "pageblob") -> BlobType.PageBlob
            | Some content -> failwithf "Unknown value for blob type ('%A'). Must be either 'blockblob' or 'pageblob'." content
            | None -> BlobType.BlockBlob
        Blob (prevPath + elementName, elementName, blobType, None)
    | Folder, Json.ObjectOrNull children ->
        let path = prevPath + elementName
        Folder (path, elementName, async { return children |> Array.map (buildBlobItem path) })
    | _ -> failInvalidJson()

let buildBlobSchema (json:Json.Json) =
    json.AsObject
    |> Array.map (fun (containerName, containerElements) ->
        { Name = containerName
          Contents =
            async {
                return
                    match containerElements with
                    | Json.ObjectOrNull elements -> elements |> Array.map (buildBlobItem "")
                    | _ -> failInvalidJson() } } )
    |> Array.toList

let createSchema resolutionFolder path =
    path
    |> Option.map(fun path ->
        let paths = [ path; Path.Combine(resolutionFolder, path) ]
        match paths |> List.tryFind File.Exists with
        | None -> Error (exn (sprintf "Could not locate schema file. Searched: %A " paths))
        | Some file ->
            try
            file
            |> File.ReadAllText
            |> JToken.Parse
            |> Json.ofJToken
            |> buildBlobSchema
            |> Ok
            with ex -> Error ex)
    |> defaultArg <| Ok []