Azure Storage Type Provider
=================

An F# Azure Type Provider which can be used to explore Azure Storage assets i.e. Blob and Table Storage, and then easily apply operations on those asset.

The goal is to create a provider which allows lightweight access to your Azure storage data within the context of e.g. F# scripts to allow CRUD operations quickly and easily.

# Blob Storage
The blob storage provider is geared for ad-hoc querying of your data, rather than programmatic access.

##Create a connection to your Azure account
	type account = Elastacloud.FSharp.AzureTypeProvider.AzureAccount< "accountName","accountKey" >
##Download a file from Azure into memory
Intellisense automatically prompts you for containers and files. There's a single Download() method on every file. It will return a different type depending on the extension of the file.

	// Downloads LeagueTable.csv as an Async<string>
	let textAsyncWorkflow = async {
		let! text = account.Containers.container1.``LeagueTable.csv``.DownloadAsync()
		printfn "%s" (text.ToLower())
		return text
	}

	// Can also do this
	let text = account.Containers.container1.``LeagueTable.csv``.DownloadAsync() |> Async.RunSynchronously
	
	// Or this - don't use on large files though, will lock up FSI whilst downloading...
	let text = account.Container.container1.``LeagueTable.csv``.Download()

	// Downloads document.xml as an XDocument
	let xmlDoc = account.Containers.container1.``document.xml``.Download()
	// xmlDoc is an XDocument, NOT a plain string
	printfn "First element is %A" xmlDoc.Elements() |> Seq.head
	xmlDoc
	
	// Open a file as a stream - ideal for binary data or large files that you want to process sequentially
	// Works on any file
	let stream = account.Containers.container1.``binary.zip``.OpenStream()
	let textStream = new StreamReader(stream)
	let firstLine = textStream.ReadLine()
	// etc. etc.

For non-text and xml files, you will get a ```DownloadString()``` function that can be used, although there is no guarantee that the contents of the file will be text :)

##Downloading files to the local file system
	// Downloads LeagueTable.csv to a file locally
	account.Containers.container1.``LeagueTable.csv``.DownloadToFile(@"D:\LeagueTable.csv")
	
	// Downloads an entire folder locally
	account.Containers.container1.``myfolder``.DownloadFolder(@"D:\MyFolder")
	
	// Downloads an entire container locally
	account.Containers.container1.DownloadContainer(@"D:\MyContainer")
	
#Table Storage
##Get a list of tables
	account.Tables. // list of tables are presented
##Download all rows for a partition
	let londonCustomers = account.Tables.Customers.GetPartition("London").AllRows()
	londonCustomers
	|> Seq.map(fun customer -> row.RowKey, customer.Name, customer.Address)
	
	// customer shape is inferred from EDM metadata 
