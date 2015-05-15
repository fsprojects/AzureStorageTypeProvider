namespace FSharp.Azure.StorageTypeProvider.Blob

open FSharp.Azure.StorageTypeProvider.Blob.BlobRepository
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open ProviderImplementation.ProvidedTypes
open System
open System.IO
open System.Xml.Linq

/// Represents a file in blob storage.
type BlobFile internal (defaultConnectionString, container, file) = 
    let getBlobRef connectionString = getBlobRef (defaultArg connectionString defaultConnectionString, container, file)
    let blobProperties =
        lazy
            let blobRef = getBlobRef None
            blobRef.FetchAttributes()
            blobRef.Properties
    
    /// Gets a handle to the Azure SDK client for this blob.
    member __.AsCloudBlockBlob(?connectionString) = getBlobRef(connectionString)

    /// Generates a full-access shared-access signature for the supplied duration.
    member __.GenerateSharedAccessSignature(duration, ?connectionString) = 
        let expiry = Nullable(DateTimeOffset.UtcNow.Add(duration))
        let policy = 
            SharedAccessBlobPolicy
                (SharedAccessExpiryTime = expiry,
                 Permissions = (SharedAccessBlobPermissions.Read ||| SharedAccessBlobPermissions.Write |||
                                SharedAccessBlobPermissions.Delete ||| SharedAccessBlobPermissions.List))
        let blobRef = getBlobRef connectionString
        let sas = blobRef.GetSharedAccessSignature policy
        Uri(sprintf "%s%s" (blobRef.Uri.ToString()) sas)
    
    /// Downloads this file to the specified path.
    member __.Download(path, ?connectionString) = 
        let targetDirectory = Path.GetDirectoryName(path)
        if not (Directory.Exists targetDirectory) then Directory.CreateDirectory targetDirectory |> ignore
        getBlobRef(connectionString).DownloadToFileAsync(path, FileMode.Create) |> awaitUnit
    
    /// Opens this file as a stream for reading.
    member __.OpenStream(?connectionString) = getBlobRef(connectionString).OpenRead()
    
    /// Opens this file as a text stream for reading.
    member this.OpenStreamAsText() = new StreamReader(this.OpenStream())

    /// Reads this file as a string.
    member __.Read(?connectionString) = getBlobRef(connectionString).DownloadText()
    
    /// Reads this file as a string asynchronously.
    member __.ReadAsync(?connectionString) = getBlobRef(connectionString).DownloadTextAsync() |> Async.AwaitTask

    /// Gets the blob size in bytes.
    member __.Size with get () = blobProperties.Value.Length

    /// Gets the name of the blob
    member __.Name with get () = (getBlobRef None).Name
    
/// Represents an XML file stored in blob storage.
type XmlFile internal (defaultConnectionString, container, file) = 
    inherit BlobFile(defaultConnectionString, container, file)
    let getBlobRef connectionString = getBlobRef (defaultArg connectionString defaultConnectionString, container, file)
    
    /// Reads this file as an XDocument.
    member __.ReadAsXDocument(?connectionString) = getBlobRef(connectionString).DownloadText() |> XDocument.Parse
    
    /// Reads this file as an XDocument asynchronously.
    member __.ReadAsXDocumentAsync(?connectionString) =
        async {
            let! text = getBlobRef(connectionString).DownloadTextAsync() |> Async.AwaitTask
            return XDocument.Parse text
        }

/// Represents a pseudo-folder in blob storage.
type BlobFolder internal (defaultConnectionString, container, file) = 
    /// Downloads the entire folder contents to the local file system asynchronously.
    member __.Download(path, ?connectionString) =
        let connectionDetails = defaultArg connectionString defaultConnectionString, container, file
        downloadFolder (connectionDetails, path)

/// Represents a container in blob storage.
type BlobContainer internal (defaultConnectionString, container) =  
    /// Gets a handle to the Azure SDK client for this container.
    member __.AsCloudBlobContainer(?connectionString) = getContainerRef(defaultArg connectionString defaultConnectionString, container)

    /// Downloads the entire container contents to the local file system asynchronously.
    member __.Download(path, ?connectionString) = 
        let connectionDetails = (defaultArg connectionString defaultConnectionString), container, String.Empty
        downloadFolder (connectionDetails, path)
    
    /// Uploads a file to this container.
    member __.Upload(path, ?connectionString) = 
        let fileName = path |> Path.GetFileName
        let blobRef = getBlobRef ((defaultArg connectionString defaultConnectionString), container, fileName)
        awaitUnit (blobRef.UploadFromFileAsync(path, FileMode.Open))

module internal ProvidedTypeGenerator = 
    let generateTypes() = 
        [ ProvidedTypeDefinition("BlobFile", Some typeof<BlobFile>, HideObjectMethods = true)
          ProvidedTypeDefinition("XmlFile", Some typeof<XmlFile>, HideObjectMethods = true) ]

/// Builder methods to construct blobs etc..
/// [omit]
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

    /// Creates a blob client.
    let createBlobClient connectionString = BlobRepository.getBlobClient connectionString