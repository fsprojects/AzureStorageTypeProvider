namespace FSharp.Azure.StorageTypeProvider.Types

open FSharp.Azure.StorageTypeProvider.Repositories.BlobRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Samples.FSharp.ProvidedTypes

/// The different types of insertion mechanism to use.
type TableInsertMode = 
    /// Insert if the entity does not already exist.
    | Insert = 0
    /// Insert if the entity does not already exist; otherwise overwrite the entity.
    | Upsert = 1

open System
open System.IO
open System.Xml.Linq

/// Represents a file in blob storage.
type BlobFile internal (connectionDetails) = 
    let connection, container, file = connectionDetails
    let blobRef = getBlobRef(connectionDetails)
    let details = blobRef.FetchAttributes()
    
    /// Generates a full-access shared-access signature for the supplied duration.
    member x.GenerateSharedAccessSignature(duration) = 
        let expiry = Nullable(DateTimeOffset.UtcNow.Add(duration))
        let policy = 
            SharedAccessBlobPolicy
                (SharedAccessExpiryTime = expiry, 
                 Permissions = (SharedAccessBlobPermissions.Read
                                ||| SharedAccessBlobPermissions.Write
                                ||| SharedAccessBlobPermissions.Delete 
                                ||| SharedAccessBlobPermissions.List))
        let sas = blobRef.GetSharedAccessSignature policy
        Uri(sprintf "%s%s" (blobRef.Uri.ToString()) sas)
    
    /// Downloads this file to the specified path.
    member x.Download(path) =
        let targetDirectory = Path.GetDirectoryName(path)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(path, FileMode.Create) |> awaitUnit
    
    /// Opens this file as a stream for reading.
    member x.OpenStream() = blobRef.OpenRead()
    
    /// Gets the blob size in bytes.
    member x.Size with get () = blobRef.Properties.Length

    /// Reads this file as a string.
    member x.ReadAsString() = blobRef.DownloadText()
    
    /// Reads this file as a string asynchronously.
    member x.ReadAsStringAsync() = Async.AwaitTask(blobRef.DownloadTextAsync())

/// Represents a text file stored in blob storage.
type TextFile internal (connectionDetails) =
    inherit BlobFile(connectionDetails)
    /// Opens this file as a text stream for reading.
    member x.OpenStreamAsText() = new StreamReader(base.OpenStream())

/// Represents an XML file stored in blob storage.
type XmlFile internal (connectionDetails) = 
    inherit BlobFile(connectionDetails)
    let blobRef = getBlobRef(connectionDetails)
    
    /// Reads this file as an XDocument.
    member x.ReadAsXDocument() = blobRef.DownloadText() |> XDocument.Parse
    
    /// Reads this file as an XDocument asynchronously.
    member x.ReadAsXDocumentAsync() =
        async { let! text = blobRef.DownloadTextAsync() |> Async.AwaitTask
                return XDocument.Parse text }

/// Represents a pseudo-folder in blob storage.
type BlobFolder internal (connectionDetails) = 
    /// Downloads the entire folder contents to the local file system asynchronously.
    member x.Download(path) = downloadFolder (connectionDetails, path)

/// Represents a container in blob storage.
type BlobContainer internal (connectionString, container) = 
    /// Downloads the entire container contents to the local file system asynchronously.
    member x.Download(path) = 
        let connectionDetails = connectionString, container, String.Empty
        downloadFolder (connectionDetails, path)
    
    /// Uploads a file to this container.
    member x.Upload(path) =
        let fileName = path |> Path.GetFileName
        let blobRef = getBlobRef(connectionString, container, fileName)
        awaitUnit (blobRef.UploadFromFileAsync(path, FileMode.Open))

module internal ProvidedTypeGenerator =
    let generateTypes() = 
        [ ProvidedTypeDefinition("BlobFile", Some typeof<BlobFile>, HideObjectMethods = true)
          ProvidedTypeDefinition("TextFile", Some typeof<TextFile>, HideObjectMethods = true)
          ProvidedTypeDefinition("XmlFile", Some typeof<XmlFile>, HideObjectMethods = true) ]

/// Builder methods to construct blobs etc..
module Builder = 
    let internal (|Text|Binary|XML|) (name : string) = 
        let endsWith extension = name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)
        match name with
        | _ when [ ".txt"; ".csv" ] |> Seq.exists endsWith -> Text
        | _ when endsWith ".xml" -> XML
        | _ -> Binary

    /// Creates a blob file object.
    let createBlobFile connectionString containerName path = 
        let details = connectionString, containerName, path
        match path with
        | XML -> new XmlFile(details) :> BlobFile
        | Text -> new TextFile(details) :> BlobFile
        | Binary -> new BlobFile(details)
    /// Creates a blob container object.
    let createContainer connectionString containerName = BlobContainer(connectionString, containerName)
    /// Creates a blob folder object.
    let createBlobFolder connectionString containerName path = BlobFolder(connectionString, containerName, path)