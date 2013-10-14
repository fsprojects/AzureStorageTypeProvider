namespace Elastacloud.FSharp.AzureTypeProvider

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

[<TypeProvider>]
type AzureAccountTypeProvider() as this = 
    inherit TypeProviderForNamespaces()
    let namespaceName = "Elastacloud.FSharp.AzureTypeProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()
    
    let buildConnectionString (args : obj []) = 
        let accountName = args.[0] :?> string
        let accountKey = args.[1] :?> string
        if accountName = null || accountKey = null then "UseDevelopmentStorage=true"
        else sprintf "DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s;" accountName accountKey
    
    let buildTypes (typeName : string) (args : obj []) = 
        let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
        let typeProviderForAccount = 
            ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        let connectionString = buildConnectionString args
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ null @@>)))
        typeProviderForAccount.AddMember(containerListingType)
        containerListingType.AddMembersDelayed
            (fun _ -> 
            AzureRepository.getBlobStorageAccountManifest (connectionString) 
            |> List.map (fun container -> ContainerTypeFactory.createContainerType (connectionString, container)))
        typeProviderForAccount
    
    let parameters = 
        [ ProvidedStaticParameter("accountName", typeof<string>, String.Empty)
          ProvidedStaticParameter("accountKey", typeof<string>, String.Empty) ]
    
    let azureAccountType = 
        ProvidedTypeDefinition(thisAssembly, namespaceName, "AzureAccount", baseType = Some typeof<obj>)
    do azureAccountType.DefineStaticParameters(parameters, buildTypes)
    do this.AddNamespace(namespaceName, [ azureAccountType ])

[<TypeProviderAssembly>]
do ()
