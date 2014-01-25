/// Generates top-level blob containers folders.
module internal Elastacloud.FSharp.AzureTypeProvider.ContainerTypeFactory

open Elastacloud.FSharp.AzureTypeProvider.Repositories
open Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository
open Elastacloud.FSharp.AzureTypeProvider.MemberFactories
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open System
open System.Reflection

let rec private createFileItem connectionString containerName fileItem = 
    match fileItem with
    | Folder(path, name, getContents) -> 
        let folderProp = ProvidedTypeDefinition("/" + name, Some typeof<obj>)
        folderProp.AddMembersDelayed
            (fun _ -> 
            [ BlobMemberFactory.createDownloadFolderFunction (connectionString, containerName, path) :> MemberInfo ] 
            @ (getContents()
               |> Array.map (createFileItem connectionString containerName)
               |> Array.toList))
        folderProp :> MemberInfo
    | Blob(path, name, properties) -> 
        let fileDetails = connectionString, containerName, path
        let fileProp = ProvidedTypeDefinition(name, Some typeof<obj>)
        fileProp.AddMembersDelayed(fun _ -> 
            [ BlobMemberFactory.createFileDetailsProperty path properties :> MemberInfo
              BlobMemberFactory.createDownloadFunction fileDetails :> MemberInfo
              BlobMemberFactory.createDownloadFileFunction fileDetails :> MemberInfo
              BlobMemberFactory.createGenerateSasFunction fileDetails :> MemberInfo ])
        fileProp :> MemberInfo

let private createContainerType connectionString (container:LightweightContainer) = 
    let individualContainerType = ProvidedTypeDefinition(container.Name, Some typeof<obj>)
    individualContainerType.AddMembersDelayed(fun _ -> 
        (container.GetFiles()
         |> Seq.map (createFileItem connectionString container.Name)
         |> Seq.toList) @ [ BlobMemberFactory.createUploadFileFunction (connectionString, container.Name)
                            BlobMemberFactory.createDownloadContainerFunction (connectionString, container.Name) ])
    individualContainerType

/// Builds up the Blob Storage member
let getBlobStorageMembers connectionString = 
    let containerListingType = ProvidedTypeDefinition("Containers", Some typeof<obj>)
    containerListingType.AddMembersDelayed
        (fun _ -> BlobRepository.getBlobStorageAccountManifest (connectionString) 
                  |> List.map (createContainerType connectionString))
    containerListingType

/// Builds up the Table Storage member
let getTableStorageMembers (connectionString : string) = 
    let tableListingType = ProvidedTypeDefinition("Tables", Some typeof<obj>)
    tableListingType.AddMembersDelayed
        (fun _ -> TableRepository.getTables connectionString
                  |> Seq.map(fun table -> ProvidedTypeDefinition(table, Some typeof<obj>))
                  |> Seq.toList)
    tableListingType