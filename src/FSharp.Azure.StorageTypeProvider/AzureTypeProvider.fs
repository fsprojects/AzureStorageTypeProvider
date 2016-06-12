﻿namespace ProviderImplementation

open FSharp.Azure.StorageTypeProvider.Blob
open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Table
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open System
open System.Reflection


[<TypeProvider>]
/// [omit]
type public AzureTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let namespaceName = "FSharp.Azure.StorageTypeProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let azureAccountType = ProvidedTypeDefinition(thisAssembly, namespaceName, "AzureTypeProvider", baseType = Some typeof<obj>)

    let buildConnectionString (args : obj []) =
        let (|ConnectionString|TwoPart|DevelopmentStorage|) (args:obj []) =
            let getArg i = args.[i] :?> string
            let accountNameOrConnectionString, accountKey = getArg 0, getArg 1
            let configFileKey, configFileName = getArg 2, getArg 3

            match accountNameOrConnectionString, accountKey, configFileKey with
            | _ when (not << String.IsNullOrWhiteSpace) configFileKey -> 
                let connectionFromConfig = getConnectionString(configFileKey, config.ResolutionFolder, configFileName)
                ConnectionString connectionFromConfig
            | _ when accountNameOrConnectionString.StartsWith "DefaultEndpointsProtocol" -> ConnectionString accountNameOrConnectionString
            | _ when [ accountNameOrConnectionString; accountKey ] |> List.exists String.IsNullOrWhiteSpace -> DevelopmentStorage
            | _ -> TwoPart (accountNameOrConnectionString, accountKey)

        match args with
        | DevelopmentStorage -> "UseDevelopmentStorage=true"
        | ConnectionString conn -> conn
        | TwoPart (name, key) -> sprintf "DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s;" name key
            
    let buildTypes (typeName : string) (args : obj []) = 
        // Create the top level property
        let typeProviderForAccount = ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ null @@>)))
        let connectionString = buildConnectionString args
        match validateConnectionString connectionString with
        | Success ->
            let domainTypes = ProvidedTypeDefinition("Domain", Some typeof<obj>)
            domainTypes.AddMembers <| ProvidedTypeGenerator.generateTypes()
            typeProviderForAccount.AddMember(domainTypes)

            // Now create child members e.g. containers, tables etc.
            typeProviderForAccount.AddMembers
                ([ BlobMemberFactory.getBlobStorageMembers 
                   TableMemberFactory.getTableStorageMembers
                   QueueMemberFactory.getQueueStorageMembers ]
                |> List.map (fun builder -> builder(connectionString, domainTypes)))
            typeProviderForAccount
        | Failure ex -> failwith (sprintf "Unable to validate connection string (%s)" ex.Message)
    
    // Parameterising the provider
    let parameters = 
        [ ProvidedStaticParameter("accountName", typeof<string>, String.Empty)
          ProvidedStaticParameter("accountKey", typeof<string>, String.Empty)
          ProvidedStaticParameter("connectionStringName", typeof<string>, String.Empty)
          ProvidedStaticParameter("configFileName", typeof<string>, "app.config") ]
    
    do
        azureAccountType.DefineStaticParameters(parameters, buildTypes)
        this.AddNamespace(namespaceName, [ azureAccountType ])
        azureAccountType.AddXmlDoc("The entry type to connect to Azure Storage assets.")

[<TypeProviderAssembly>]
do ()