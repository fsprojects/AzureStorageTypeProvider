namespace Elastacloud.FSharp.AzureTypeProvider

open Elastacloud.FSharp.AzureTypeProvider.Repositories.BlobRepository
open Samples.FSharp.ProvidedTypes

/// The different types of insertion mechanism to use.
type TableInsertMode = 
    /// Insert if the entity does not already exist.
    | Insert = 0
    /// Insert if the entity does not already exist; otherwise overwrite with this entity.
    | InsertOrReplace = 1

open System
open System.IO
open System.Xml.Linq

/// Represents a file in blob storage.
type BlobFile(connectionDetails) = 
    let connection, container, file = connectionDetails
    /// Generates a full-access shared-access signature for the supplied duration.
    member x.GenerateSharedAccessSignature(duration) = getSas (connection, container, file, duration)
    /// Downloads this file to the specified path.
    member x.Download(path) = downloadToFile (connection, container, file, path)
    /// Opens this file as a stream for reading.
    member x.OpenStream() = downloadStream (connectionDetails)
    /// Gets the blob size in bytes.
    member x.Size with get() = let _, properties = getFileDetails(connectionDetails)
                               properties.Length

/// Represents a file stored in blob storage that can be read as text file.
type TextFile(connectionDetails) = 
    inherit BlobFile(connectionDetails)
    /// Reads this file as a string.
    member x.ReadAsString() = downloadText (connectionDetails)
    /// Reads this file as a string asynchronously.
    member x.ReadAsStringAsync() = downloadTextAsync (connectionDetails)

/// Represents a file stored in blob storage that can be read as a XDocument.
type XmlFile(connectionDetails) = 
    inherit BlobFile(connectionDetails)
    /// Reads this file as an XDocument.
    member x.ReadAsXDocument() = downloadText (connectionDetails) |> XDocument.Parse
    /// Reads this file as an XDocument asynchronously.
    member x.ReadAsXDocumentAsync() = async { let! text = downloadTextAsync (connectionDetails)
                                              return XDocument.Parse text }

type BlobFolder(connectionDetails) =
    /// Downloads the entire folder contents to the local file system asynchronously.
    member x.Download(path) = downloadFolder(connectionDetails, path)

type BlobContainer(connectionString, container) =
    /// Downloads the entire container contents to the local file system asynchronously.
    member x.Download(path) =
        let connectionDetails = connectionString, container, String.Empty
        downloadFolder(connectionDetails, path)
    /// Uploads a file to this container.
    member x.Upload(path) = uploadFile(connectionString, container, path)

module ProvidedTypes =
    let BlobFileProvidedType = ProvidedTypeDefinition("BlobFile", Some typeof<BlobFile>, HideObjectMethods = true)
    let TextFileProvidedType = ProvidedTypeDefinition("TextFile", Some typeof<TextFile>, HideObjectMethods = true)
    let XmlFileProvidedType = ProvidedTypeDefinition("XmlFile", Some typeof<XmlFile>, HideObjectMethods = true)

// Builder methods to construct blobs etc..
module Builder =
    let internal (|Text|Binary|XML|)(name: string) = 
        let endsWith extension = name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)
        match name with
        | _ when [ ".txt"; ".csv" ] |> Seq.exists endsWith -> Text
        | _ when endsWith ".xml" -> XML
        | _ -> Binary

    let createBlobFile connectionString containerName path =
        let details = connectionString, containerName, path
        match path with
        | Text -> new TextFile(details) :> BlobFile
        | XML -> new XmlFile(details) :> BlobFile
        | Binary -> new BlobFile(details)

    let createContainer connectionString containerName = BlobContainer(connectionString,containerName)

    let createBlobFolder connectionString containerName path = BlobFolder(connectionString, containerName, path)