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
    let azureAccountType = 
        ProvidedTypeDefinition
            (thisAssembly, namespaceName, "AzureAccount", baseType = Some typeof<obj>)
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
    do 
        let connectionString = "UseDevelopmentStorage=true"
        azureAccountType.AddMember
            (ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ null @@>)))
        azureAccountType.AddMember(containerListingType)
        AzureRepository.getBlobStorageAccountManifest (connectionString) 
        |> List.iter 
               (fun container -> 
               let individualContainerType = 
                   ProvidedTypeDefinition(container.Name, Some typeof<obj>)
               containerListingType.AddMember(individualContainerType)
               container.Files 
               |> Seq.iter 
                      (fun file -> 
                      individualContainerType.AddMember
                          (ProvidedProperty
                               (propertyName = file, propertyType = typeof<obj>, IsStatic = true, 
                                GetterCode = (fun args -> <@@ null @@>)))))
        //    
        //        containerListingType.AddMembers containerProps
        this.AddNamespace(namespaceName, [ azureAccountType ])

[<TypeProviderAssembly>]
do ()
