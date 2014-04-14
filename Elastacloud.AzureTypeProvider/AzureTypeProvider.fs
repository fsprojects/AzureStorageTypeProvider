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
    let azureAccountType = ProvidedTypeDefinition(thisAssembly, namespaceName, "AzureAccount", baseType = Some typeof<obj>)

    do
        azureAccountType.AddMembers [ ProvidedTypes.BlobFileProvidedType; ProvidedTypes.TextFileProvidedType; ProvidedTypes.XmlFileProvidedType ]
    
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
        let domainTypes = ProvidedTypeDefinition("DomainTypes", Some typeof<obj>)
        typeProviderForAccount.AddMember(domainTypes)

        // Now create child members e.g. containers, tables etc.
        typeProviderForAccount.AddMembers
            ([ ContainerTypeFactory.getBlobStorageMembers 
               (* ContainerTypeFactory.getTableStorageMembers *) ]
            |> List.map (fun builder -> builder connectionString domainTypes))
        typeProviderForAccount
    
    // Parameterising the provider
    let parameters = 
        [ ProvidedStaticParameter("accountName", typeof<string>, String.Empty)
          ProvidedStaticParameter("accountKey", typeof<string>, String.Empty) ]
    
    do azureAccountType.DefineStaticParameters(parameters, buildTypes)
    do this.AddNamespace(namespaceName, [ azureAccountType ])

[<TypeProviderAssembly>]
do ()