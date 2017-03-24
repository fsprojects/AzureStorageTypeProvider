module internal FSharp.Azure.StorageTypeProvider.Blob.StaticSchema

open BlobRepository
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

module private Option =
    let ofString text = if String.IsNullOrWhiteSpace text then None else Some text

let private pathsToContainerItems paths =
    let segmentedPaths =
        paths
        |> Seq.map (fun (path:string) -> path.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
    
    let rec toFileTrees prevPath childPaths = 
        childPaths
        |> Seq.groupBy (function
            | [] -> None
            | [ fileName ] -> Some (fileName, true)
            | dirName :: _ -> Some (dirName, false))
        |> Seq.choose (function (Some k, v) -> Some (k, v) | _ -> None)
        |> Seq.map (fun ((name, isFile), childPaths) ->
            if isFile then Blob (prevPath + name, name, BlobType.BlockBlob, None)
            else
                let folderName = name + "/"
                let subPaths = childPaths |> Seq.map List.tail
                let path = prevPath + folderName
                Folder (path, folderName, lazy (toFileTrees path subPaths |> Seq.toArray)))

    toFileTrees "" segmentedPaths

let private schemaLinesToContainers lines =
    lines
    |> Seq.map (fun line ->
        match (line:string).Split '@' with
        | [| container; path |] -> (container, path)
        | _ -> failwith (sprintf "Invalid blob path in static schema: %s" line))
    |> Seq.groupBy fst
    |> Seq.map (fun (containerName, paths) ->
        { Name = containerName
          Contents = lazy (paths |> Seq.map snd |> pathsToContainerItems) })
    |> Seq.toList

let createSchema resolutionFolder path =
    path
    |> Option.ofString
    |> Option.map(fun path ->
        try
        Path.Combine(resolutionFolder, path)
        |> File.ReadAllLines
        |> schemaLinesToContainers
        |> Success
        with ex -> Failure ex)
    |> defaultArg <| Success []
