Azure Storage Type Provider
=================

An F# Azure Type Provider which can be used to explore Azure Storage assets i.e. Blob and Table Storage, and then easily apply operations on those asset.

The goal is to create a provider which allows lightweight access to your Azure storage data within the context of e.g. F# scripts to allow CRUD operations quickly and easily.

# Install

Use the Nuget Package Manager to install the Type Provider (https://www.nuget.org/packages/FSharp.Azure.StorageTypeProvider) or through the console

``` 
PM> Install-Package FSharp.Azure.StorageTypeProvider
#load "StorageTypeProvider.fsx" // Load all azure references for use in .fsx files etc.
```
# Blob Storage
The blob storage provider is geared for ad-hoc querying of your data, rather than programmatic access.

##Create a connection to your Azure account
	open FSharp.Azure.StorageTypeProvider
	
	// Connect to a live Azure account
	type account = AzureTypeProvider<"accountName","accountKey">
	
	// Connect to the local Storage emulator
	type localAccount = AzureTypeProvider<"DevStorageAccount", "">

##Reading a file from Azure into memory
Intellisense automatically prompts you for containers and files.

	// Downloads LeagueTable.csv as an Async<string>
	let textAsyncWorkflow = async {
		let! text = account.Containers.container1.``LeagueTable.csv``.ReadAsStringAsync()
		printfn "%s" (text.ToLower())
		return text
	}

	// Can also do this
	let text = account.Containers.container1.``LeagueTable.csv``.ReadAsStringAsync() |> Async.RunSynchronously
	
	// Or this - don't use on large files though, will lock up FSI whilst downloading...
	let text = account.Container.container1.``LeagueTable.csv``.ReadAsString()

	// Downloads document.xml as an XDocument
	let xmlDoc = account.Containers.container1.``document.xml``.ReadAsXDocument()
	// xmlDoc is an XDocument, NOT a plain string
	printfn "First element is %A" xmlDoc.Elements() |> Seq.head
	xmlDoc
	
	// Open a file as a stream - ideal for binary data or large files that you want to process sequentially
	// Works on any file
	let stream = account.Containers.container1.``binary.zip``.OpenStream()
	// etc. etc.
	
	// Open a file as a text-based StreamReader.
	let streamReader = account.Containers.container1.``largefile.txt``.OpenStreamAsText()
	let firstLine = streamReader.ReadLine()


##Downloading files to the local file system
	// Downloads LeagueTable.csv to a file locally
	account.Containers.container1.``LeagueTable.csv``.Download(@"D:\LeagueTable.csv")
	
	// Downloads an entire folder locally
	account.Containers.container1.``myfolder``.Download(@"D:\MyFolder")
	
	// Downloads an entire container locally
	account.Containers.container1.Download(@"D:\MyContainer")
	
#Table Storage
	open FSharp.Azure.StorageTypeProvider.Table

##Get a list of tables
	account.Tables. // list of tables are presented
##Download all rows for a partition
	let londonCustomers = account.Tables.Customers.GetPartition("London")
	londonCustomers
	|> Seq.map(fun customer -> row.RowKey, customer.Name, customer.Address)
	
	// customer shape is inferred from EDM metadata 
##Get a single entity by RowKey and PartitionKey
	let joeBloggs = account.Tables.Customers.Get(Row "joe.bloggs@hotmail.com", Partition "London")
##Search for entities
	let ukCustomers = account.Tables.Customers.Query("Country eq 'UK'")
	let ukCustomers = account.Tables.Customers.Query().``Where Country Is``.``Equal To``("UK").Execute()
##Insert entity
	account.Tables.Customers.Insert(new Customer("fred.smith@live.co.uk", "UK", "London"))
