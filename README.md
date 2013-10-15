AzureTypeProvider
=================

An F# Azure Type Provider which can be used to explore e.g. Blob Storage and easily apply operations on Azure assets.

The goal is to create a provider which allows lightweight access to your Azure assets within the context of e.g. F# scripts to allow CRUD operations quickly and easily.

* **Create a connection to your Azure account**

	type account = Elastacloud.FSharp.AzureTypeProvider.AzureAccount< "accountName","accountKey" >

* **Download a file from Azure**. Intellisense automatically prompts you for containers and files. There's a single Download() method on every file. It will return a different type depending on the extension of the file.

	// Downloads LeagueTable.csv as an Async<string>
	let textAsyncWorkflow = async {
		let! text = account.Containers.container1.``LeagueTable.csv``.Download()
		printfn "%s" (text.ToLower())
		return text
	}

	// Downloads binary.zip as an Async<Byte[]>
	let binaryAsyncWorkflow = async {
		let! binaryArray = account.Containers.container1.``binary.zip``.Download()
		printfn "%d bytes downloaded" binaryArray.Length
		return binaryArray
	}

	// Downloads document.xml as an Async<XDocument>
	let xmlAsyncWorkflow = async {
		let! xmlDoc = account.Containers.container1.``document.xml``.Download()
		printfn "First element is %A" xmlDoc.Elements() |> Seq.head
		return xmlDoc
	}

* **Download a file to the local file system**

	// Asynchronously downloads LeagueTable.csv to a file locally
	account.Containers.container1.``LeagueTable.csv``.DownloadToFile(@"D:\LeagueTable.csv")