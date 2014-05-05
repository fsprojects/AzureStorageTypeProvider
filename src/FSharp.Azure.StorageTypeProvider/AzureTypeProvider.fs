namespace FSharp.Azure.StorageTypeProvider

open FSharp.Azure.StorageTypeProvider.Types
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

[<TypeProvider>]
/// The type provider for connecting to Azure Storage.
type AzureTypeProvider() as this = 
    inherit TypeProviderForNamespaces()

    let namespaceName = "FSharp.Azure.StorageTypeProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let azureAccountType = ProvidedTypeDefinition(thisAssembly, namespaceName, "AzureTypeProvider", baseType = Some typeof<obj>)

    let buildConnectionString (args : obj []) = 
        let accountName = args.[0] :?> string
        let accountKey = args.[1] :?> string
        let blankArgs = [ accountName; accountKey ] |> Seq.exists (fun param -> String.IsNullOrEmpty(param.Trim()))
        if blankArgs then "UseDevelopmentStorage=true"
        else sprintf "DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s;" accountName accountKey
            
    let buildTypes (typeName : string) (args : obj []) = 
        // Create the top level property
        let typeProviderForAccount = 
            ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ null @@>)))
        let connectionString = buildConnectionString args
        let domainTypes = ProvidedTypeDefinition("Domain", Some typeof<obj>)
        domainTypes.AddMembers <| ProvidedTypeGenerator.generateTypes()
        typeProviderForAccount.AddMember(domainTypes)

        // Now create child members e.g. containers, tables etc.
        typeProviderForAccount.AddMembers
            ([ ContainerTypeFactory.getBlobStorageMembers 
               ContainerTypeFactory.getTableStorageMembers ]
            |> List.map (fun builder -> builder(connectionString,domainTypes)))
        typeProviderForAccount
    
    // Parameterising the provider
    let parameters = 
        [ ProvidedStaticParameter("accountName", typeof<string>, String.Empty)
          ProvidedStaticParameter("accountKey", typeof<string>, String.Empty) ]
    
    do azureAccountType.DefineStaticParameters(parameters, buildTypes)
    do this.AddNamespace(namespaceName, [ azureAccountType ])

[<TypeProviderAssembly>]
do ()