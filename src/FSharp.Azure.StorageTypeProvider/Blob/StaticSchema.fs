module internal FSharp.Azure.StorageTypeProvider.Blob.StaticSchema

open BlobRepository
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

module Option =
    let ofString text = if String.IsNullOrWhiteSpace text then None else Some text

let private splitOn (c:char) (value:string) = value.Split c

let private pathsToContainerItems paths =   
    let rec toFileTrees prevPath childPaths = 
        childPaths
        |> Array.groupBy (function
            | [] | [ "" ] -> None
            | [ fileName ] -> Some (fileName, true)
            | dirName :: _ -> Some (dirName, false))
        |> Array.choose (function (Some k, v) -> Some (k, v) | _ -> None)
        |> Array.map (fun ((name, isFile), childPaths) ->
            if isFile then Blob (prevPath + name, name, BlobType.BlockBlob, None)
            else
                let folderName = name + "/"
                let subPaths = childPaths |> Array.map List.tail
                let path = prevPath + folderName
                Folder (path, folderName, lazy (toFileTrees path subPaths |> Seq.toArray)))

    toFileTrees "" paths

let private schemaLinesToContainers lines =
    lines
    |> Seq.toArray
    |> Array.map (fun line ->
        match line |> splitOn '@' with
        | [| container; path |] -> (container, path)
        | _ -> failwith (sprintf "Invalid blob path in static schema: %s" line))
    |> Array.groupBy fst
    |> Array.map (fun (containerName, paths) ->
        { Name = containerName
          Contents = lazy (paths |> Array.map (snd >> splitOn '/' >> Array.toList) |> pathsToContainerItems |> Array.toSeq) })
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
            |> File.ReadAllLines
            |> schemaLinesToContainers
            |> Success
            with ex -> Failure ex)
    |> defaultArg <| Success []