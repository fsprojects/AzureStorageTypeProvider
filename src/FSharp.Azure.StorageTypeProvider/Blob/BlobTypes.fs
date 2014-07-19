namespace FSharp.Azure.StorageTypeProvider.Blob

open FSharp.Azure.StorageTypeProvider.Blob.BlobRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open ProviderImplementation.ProvidedTypes

open System
open System.IO
open System.Xml.Linq

/// Represents a file in blob storage.
type BlobFile internal (connectionDetails) = 
    let blobRef = getBlobRef (connectionDetails)
    let blobProperties = lazy
                            blobRef.FetchAttributes()
                            blobRef.Properties
    
    /// Generates a full-access shared-access signature for the supplied duration.
    member x.GenerateSharedAccessSignature(duration) = 
        let expiry = Nullable(DateTimeOffset.UtcNow.Add(duration))
        let policy = 
            SharedAccessBlobPolicy
                (SharedAccessExpiryTime = expiry,
                 Permissions = (SharedAccessBlobPermissions.Read ||| SharedAccessBlobPermissions.Write |||
                                SharedAccessBlobPermissions.Delete ||| SharedAccessBlobPermissions.List))
        let sas = blobRef.GetSharedAccessSignature policy
        Uri(sprintf "%s%s" (blobRef.Uri.ToString()) sas)
    
    /// Downloads this file to the specified path.
    member x.Download(path) = 
        let targetDirectory = Path.GetDirectoryName(path)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        blobRef.DownloadToFileAsync(path, FileMode.Create) |> awaitUnit
    
    /// Opens this file as a stream for reading.
    member x.OpenStream() = blobRef.OpenRead()
    
    /// Opens this file as a text stream for reading.
    member x.OpenStreamAsText() = new StreamReader(x.OpenStream())

    /// Reads this file as a string.
    member x.ReadAsString() = blobRef.DownloadText()
    
    /// Reads this file as a string asynchronously.
    member x.ReadAsStringAsync() = Async.AwaitTask(blobRef.DownloadTextAsync())

    /// Gets the blob size in bytes.
    member x.Size with get () = blobProperties.Value.Length

    /// Gets the name of the blob
    member x.Name with get () = blobRef.Name
    
/// Represents an XML file stored in blob storage.
type XmlFile internal (connectionDetails) = 
    inherit BlobFile(connectionDetails)
    let blobRef = getBlobRef (connectionDetails)
    
    /// Reads this file as an XDocument.
    member x.ReadAsXDocument() = blobRef.DownloadText() |> XDocument.Parse
    
    /// Reads this file as an XDocument asynchronously.
    member x.ReadAsXDocumentAsync() = async { let! text = blobRef.DownloadTextAsync() |> Async.AwaitTask
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
        let blobRef = getBlobRef (connectionString, container, fileName)
        awaitUnit (blobRef.UploadFromFileAsync(path, FileMode.Open))

module internal ProvidedTypeGenerator = 
    let generateTypes() = 
        [ ProvidedTypeDefinition("BlobFile", Some typeof<BlobFile>, HideObjectMethods = true)
          ProvidedTypeDefinition("XmlFile", Some typeof<XmlFile>, HideObjectMethods = true) ]

/// Builder methods to construct blobs etc..
module ContainerBuilder = 
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
        | Text | Binary -> new BlobFile(details)
    
    /// Creates a blob container object.
    let createContainer connectionString containerName = BlobContainer(connectionString, containerName)
    
    /// Creates a blob folder object.
    let createBlobFolder connectionString containerName path = BlobFolder(connectionString, containerName, path)
