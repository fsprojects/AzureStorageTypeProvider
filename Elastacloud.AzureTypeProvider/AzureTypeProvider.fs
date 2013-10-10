namespace Elastacloud.FSharp.AzureTypeProvider

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

[<TypeProvider>]
type blobStorageTypeProvider() as this = 
    inherit TypeProviderForNamespaces()
    let namespaceName = "Elastacloud.FSharp.AzureTypeProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let blobStorageType = ProvidedTypeDefinition(thisAssembly, namespaceName, "blobStorageType", baseType = Some typeof<obj>)
    let filesPropType = ProvidedTypeDefinition(thisAssembly, namespaceName, "ContainerListing", baseType = Some typeof<obj>)
    do 
        let connectionString = "UseDevelopmentStorage=true"
        blobStorageType.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun args -> <@@ "The object data" :> obj @@>)))
        let fileProperties = 
            AzureRepository.getBlobStorageAccountManifest(connectionString) |> List.map(fun container -> 
                                                                                   let subNamespace = namespaceName + "." + container.Name
                                                                                   let prop, containerType = ContainerTypeFactory.createContainerType(thisAssembly, subNamespace, container)
                                                                                   this.AddNamespace(subNamespace, [ containerType ])
                                                                                   prop)
        filesPropType.AddMembers fileProperties
        let filesProp = ProvidedProperty(propertyName = "Files", propertyType = filesPropType, GetterCode = (fun args -> <@@ null @@>))
        blobStorageType.AddMember filesProp
        this.AddNamespace(namespaceName, [ blobStorageType;filesPropType ])

[<TypeProviderAssembly>]
do ()
