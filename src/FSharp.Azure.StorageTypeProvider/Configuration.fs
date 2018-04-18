module internal FSharp.Azure.StorageTypeProvider.Configuration

open System.Collections.Generic
open System.Configuration
open System.IO
open Microsoft.WindowsAzure.Storage

type Result<'T> = Success of result:'T | Failure of exn

let private doesFileExist folder file =
    let fullPath = Path.Combine(folder, file) 
    if fullPath |> File.Exists then Some fullPath else None

let private (|NoConfigRequested|RequestedConfigExists|RequestedConfigDoesNotExist|) (configFile, resolutionFolder) =
    let toOption s = if s = "" then None else Some s
    let configFile = configFile |> toOption |> Option.map (doesFileExist resolutionFolder)
    match configFile with
    | None -> NoConfigRequested
    | Some (Some configFile) -> RequestedConfigExists configFile
    | Some None -> RequestedConfigDoesNotExist

let private (|DefaultConfigExists|NoDefaultConfigFound|) resolutionFolder =
    [ "app.config"; "web.config" ]
    |> List.tryPick (doesFileExist resolutionFolder)
    |> Option.map(fun config -> DefaultConfigExists config)
    |> defaultArg <| NoDefaultConfigFound

let getConnectionString(connectionName: string, resolutionFolder, requestedConfig) =
    let configPath =
        match (requestedConfig, resolutionFolder), resolutionFolder with
        | RequestedConfigExists configPath, _
        | NoConfigRequested, DefaultConfigExists configPath -> configPath
        | RequestedConfigDoesNotExist, _ -> raise <| FileNotFoundException(sprintf "Could not find config file '%s' in path '%s'." requestedConfig resolutionFolder)
        | NoConfigRequested, NoDefaultConfigFound -> failwithf "Cannot find either app.config or web.config in path '%s'." resolutionFolder

    let map = ExeConfigurationFileMap(ExeConfigFilename = configPath)
    let configSection = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None).ConnectionStrings.ConnectionStrings

    match configSection, lazy configSection.[connectionName] with
    | null, _ -> raise <| KeyNotFoundException(sprintf "Cannot find the <connectionStrings> section of %s file." configPath)
    | _, Lazy null -> raise <| KeyNotFoundException(sprintf "Cannot find name %s in <connectionStrings> section of %s file." connectionName configPath)
    | _, Lazy x -> x.ConnectionString


[<AutoOpen>]
module ConnectionValidation =
    let private memoize code =
        let cache = Dictionary()
        fun arg ->
            if not(cache.ContainsKey arg)
            then cache.[arg] <- code arg
            cache.[arg]
    let private checkConnectionString connectionString =
        try
            CloudStorageAccount
                .Parse(connectionString)
                .CreateCloudBlobClient()
                .GetContainerReference("abc")
                .Exists() //throws an exception if attempted with an invalid connection string
                |> ignore
            Success()
        with | ex -> Failure ex
    let validateConnectionString = memoize checkConnectionString