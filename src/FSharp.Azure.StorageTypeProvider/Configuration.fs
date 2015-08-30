namespace FSharp.Azure.StorageTypeProvider.Configuration

open System.Configuration
open System.IO
open System.Collections.Generic

type Configuration() =    
    static member ReadConnectionStringFromConfigFileByName(name: string, resolutionFolder, fileName) =

        let configFilename = 
            if fileName <> "" 
            then
                let path = Path.Combine(resolutionFolder, fileName)
                if not <| File.Exists path 
                then raise <| FileNotFoundException( sprintf "Could not find config file '%s'." path)
                else path
            else
                let appConfig = Path.Combine(resolutionFolder, "app.config")
                let webConfig = Path.Combine(resolutionFolder, "web.config")

                if File.Exists appConfig then appConfig
                elif File.Exists webConfig then webConfig
                else failwithf "Cannot find either app.config or web.config."
        
        let map = ExeConfigurationFileMap()
        map.ExeConfigFilename <- configFilename
        let configSection = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None).ConnectionStrings.ConnectionStrings
        match configSection, lazy configSection.[name] with
        | null, _ | _, Lazy null -> raise <| KeyNotFoundException(message = sprintf "Cannot find name %s in <connectionStrings> section of %s file." name configFilename)
        | _, Lazy x -> x.ConnectionString
