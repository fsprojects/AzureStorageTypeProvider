namespace Elastacloud.FSharp.AzureTypeProvider

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

[<TypeProvider>]
type azureAccountTypeProvider() as this = 
    inherit TypeProviderForNamespaces()
    let namespaceName = "Elastacloud.FSharp"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let azureAccountType = ProvidedTypeDefinition(thisAssembly, namespaceName, "AzureAccount", baseType = Some typeof<obj>)
    let containerListingType = ProvidedTypeDefinition(thisAssembly, namespaceName, "ContainerListing", baseType = Some typeof<obj>)

    do 
        let connectionString = "UseDevelopmentStorage=true"
        azureAccountType.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ "The object data" :> obj @@>)))
        let containerProps = AzureRepository.getBlobStorageAccountManifest(connectionString)
                             |> List.map(fun container -> let subNamespace = sprintf "%s.%s" namespaceName container.Name
                                                          let prop, containerType = ContainerTypeFactory.createContainerType(thisAssembly, subNamespace, container)
                                                          this.AddNamespace(subNamespace, [ containerType ])
                                                          prop)
    
        containerListingType.AddMembers containerProps
        azureAccountType.AddMember(ProvidedProperty(propertyName = "Containers", propertyType = containerListingType, GetterCode = (fun args -> <@@ null @@>)))
        
        this.AddNamespace(namespaceName, [ azureAccountType;containerListingType ])

[<TypeProviderAssembly>]
do ()
