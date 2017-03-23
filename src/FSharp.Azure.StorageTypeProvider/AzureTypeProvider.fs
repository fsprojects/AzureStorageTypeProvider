namespace ProviderImplementation

open FSharp.Azure.StorageTypeProvider.Blob
open FSharp.Azure.StorageTypeProvider.Queue
open FSharp.Azure.StorageTypeProvider.Table
open FSharp.Azure.StorageTypeProvider.Configuration
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open System
open System.Collections.Generic
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
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun _ -> <@@ null @@>)))
        let connectionString = buildConnectionString args
        match validateConnectionString connectionString, StaticSchema.createSchema config.ResolutionFolder (args.[6] :?> string)  with
        | Success(), Success blobSchema ->
            let domainTypes = ProvidedTypeDefinition("Domain", Some typeof<obj>)
            domainTypes.AddMembers <| ProvidedTypeGenerator.generateTypes()
            typeProviderForAccount.AddMember(domainTypes)

            let schemaInferenceRowCount = args.[4] :?> int
            let humanizeColumns = args.[5] :?> bool

            // Now create child members e.g. containers, tables etc.
            typeProviderForAccount.AddMembers
                ([ BlobMemberFactory.getBlobStorageMembers blobSchema
                   TableMemberFactory.getTableStorageMembers schemaInferenceRowCount humanizeColumns
                   QueueMemberFactory.getQueueStorageMembers ]
                |> List.map (fun builder -> builder(connectionString, domainTypes)))
            typeProviderForAccount
        | Failure ex, _ -> failwith (sprintf "Unable to validate connection string (%s)" ex.Message)
        | _, Failure ex -> failwith (sprintf "Unable to parse blob schema file (%s)" ex.Message)
    
    let createParam (name, defaultValue:'a, help) =
        let providedParameter = ProvidedStaticParameter(name, typeof<'a>, defaultValue)
        providedParameter.AddXmlDoc help
        providedParameter
    
    // Parameterising the provider
    let parameters =
        [ createParam("accountName", String.Empty, "The Storage Account name, or full connection string in the format 'DefaultEndpointsProtocol=protocol;AccountName=account;AccountKey=key;'.")
          createParam("accountKey", String.Empty, "The Storage Account key. Ignored if the accountName argument is the full connection string.")
          createParam("connectionStringName", String.Empty, "The Connection String key from the configuration file to use to retrieve the connection string. If set, accountName and accountKey are ignored.")
          createParam("configFileName", "app.config", "The name of the configuration file to look for. Defaults to 'app.config'")
          createParam("schemaSize", 10, "The maximum number of rows to read per table, from which to infer schema. Defaults to 10.")
          createParam("humanize", false, "Whether to humanize table column names. Defaults to false.")
          createParam("blobSchema", String.Empty, "Provide a path to a local file containing a fixed schema to eagerly use, instead of lazily generating the schema from a live storage account.") ]
    
    let memoize func =
        let cache = Dictionary()
        fun argsAsString args ->
            if not (cache.ContainsKey argsAsString) then
                cache.Add(argsAsString, func argsAsString args)
            cache.[argsAsString]

    do
        azureAccountType.DefineStaticParameters(parameters, memoize buildTypes)
        this.AddNamespace(namespaceName, [ azureAccountType ])
        azureAccountType.AddXmlDoc("The entry type to connect to Azure Storage assets.")

[<TypeProviderAssembly>]
do ()