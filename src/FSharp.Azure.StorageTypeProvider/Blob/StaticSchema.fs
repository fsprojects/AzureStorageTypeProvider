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
        let path = Path.Combine(resolutionFolder, path) |> File.ReadAllLines
        let schema =
            { Name = "samples"
              Contents =
                lazy
                    [ Folder("folder", "folder/", lazy [| Blob("folder/childFile.txt", "childFile.txt", BlobType.BlockBlob, None) |])
                      Blob("file1.txt", "file1.txt", BlobType.BlockBlob, None) ]
                    |> Seq.ofList }
        Success [ schema ]
        with ex -> Failure ex)
    |> defaultArg <| Success []
