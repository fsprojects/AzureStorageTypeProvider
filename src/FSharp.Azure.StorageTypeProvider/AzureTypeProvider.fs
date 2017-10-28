namespace ProviderImplementation

open FSharp.Azure.StorageTypeProvider
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

    let startLiveRefresh : obj -> _ =
        function
        | :? int as seconds when seconds >= 1 ->
            let seconds = seconds * 1000
            async {
                while true do
                    do! Async.Sleep seconds
                    this.Invalidate()
            } |> Async.Start
        | _ -> ()
        
            
    let buildTypes (typeName : string) (args : obj []) = 
        // Create the top level property
        let typeProviderForAccount = ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun _ -> <@@ null @@>)))
        
        startLiveRefresh args.[8]
        
        let connectionString = buildConnectionString args
        let staticBlobSchema = args.[6] :?> string |> Option.ofString
        let staticTableSchema = args.[7] :?> string |> Option.ofString
        let connectionStringValidation =
            match staticBlobSchema, staticTableSchema with
            | Some _, _ | _, Some _ -> None
            | _ -> Some (validateConnectionString connectionString)
            
        let parsedBlobSchema = Blob.StaticSchema.createSchema config.ResolutionFolder staticBlobSchema
        let parsedTableSchema = Table.StaticSchema.createSchema config.ResolutionFolder staticTableSchema

        match connectionStringValidation, parsedBlobSchema, parsedTableSchema with
        | Some (Success _), Success blobSchema, Success tableSchema
        | None, Success blobSchema, Success tableSchema ->
            let domainTypes = ProvidedTypeDefinition("Domain", Some typeof<obj>)            
            typeProviderForAccount.AddMember(domainTypes)

            let schemaInferenceRowCount = args.[4] :?> int
            let humanizeColumns = args.[5] :?> bool

            // Now create child members e.g. containers, tables etc.
            typeProviderForAccount.AddMembers
                ([ (BlobMemberFactory.getBlobStorageMembers blobSchema, "blobs")
                   (TableMemberFactory.getTableStorageMembers tableSchema schemaInferenceRowCount humanizeColumns, "tables")
                   (QueueMemberFactory.getQueueStorageMembers, "queues") ]
                |> List.map (fun (builder, name) ->
                    try builder(connectionString, domainTypes)
                    with ex -> failwithf "An error occurred during initial type generation for %s: %O" name ex))
            typeProviderForAccount
        | Some (Failure ex), _, _ -> failwithf "Unable to validate connection string (%s)" ex.Message
        | _, Failure ex, _ -> failwithf "Unable to parse blob schema file (%s)" ex.Message
        | _, _, Failure ex -> failwithf "Unable to parse table schema file (%s)" ex.Message
    
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
          createParam("blobSchema", String.Empty, "Provide a path to a local file containing a fixed schema to eagerly use, instead of lazily generating the blob schema from a live storage account.")
          createParam("tableSchema", String.Empty, "Provide a path to a local file containing a fixed schema to eagerly use, instead of lazily generating the table schema from a live storage account.")
          createParam("autoRefresh", 0, "Optionally provide the number of seconds to wait before refreshing the schema. Defaults to 0 (never).") ]
    
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