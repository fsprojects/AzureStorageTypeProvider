module internal FSharp.Azure.StorageTypeProvider.Blob.StaticSchema

open BlobRepository
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.WindowsAzure.Storage.Blob
open System
open System.IO

module private Option =
    let ofString text = if String.IsNullOrWhiteSpace text then None else Some text

let createSchema resolutionFolder path =
    path
    |> Option.ofString
    |> Option.map(fun path ->
        try
        printfn "FOUND A SCHEMA!"   
        let paths = Path.Combine(resolutionFolder, path) |> File.ReadAllLines
        let schema = pathsToFileTrees paths
        Success schema
        with ex -> Failure ex)
    |> defaultArg <| Success []
