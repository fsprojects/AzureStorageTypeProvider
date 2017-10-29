(**
Hot Schema Loading
==================

As of 1.9.4, the type provider has support for "hot schema loading" of types. This is extremely useful when working
in an exploratory mode on top of a storage account, particularly when adding and removing data at the same time.

When Hot Schema Loading is activated, the type provider will on a regular basis "refresh" any types used in your scripts or
F# files based on the latest schema in real time - that is, either any schema files or the live data source used.

## Activating Hot Schema Loading
To turn on Hot Schema Loading, simply provide a value for the `autoRefresh` argument in the type provider. This argument
represents the number of *seconds* between refreshes.

Note that Hot Schema Loading is **not** turned on by default.
*)

/// A handle to local development storage whose schema refreshes every 5 seconds.
type Azure = AzureTypeProvider<"UseDevelopmentStorage=true", autoRefresh = 5>

(**
## Hot Schema Loading in practice
This section outlines some of the uses of Hot Schema Loading across the different types of services in Azure Storage.

### Working with Blobs
* As containers or blobs are renamed, added or deleted, the type system will automatically show compiler errors if your code references
a blob that no longer exists. There's no need to reload your IDE; it will happen automatically.
* From F# 4.1 onwards, the compiler will automatically suggest potential renames for closely matching blobs.

### Working with Tables
* As Tables are created or deleted, they will update within your code.
* As data changes within your tables, schema inference will automatically re-evaluate your code - new properties will show, removed properties
will cause compiler errors and optional parameters will appear / remove as needed.

### Working with Queues
* As Queues are created or deleted, they will update within your code.
* The `Peek` member will automatically refresh with the latest contents on the queue.

## Costs with Hot Schema Loading
Be aware that Hot Schema Loading makes repeated requests to your Azure Storage accounts to keep your schema up-to-date, and
you should factor in costs associated with this. Whilst Azure Storage costs for storage, ingress and egress are very competitive,
consider the following points to ensure that you do not end up with an unexpected bill.

### Use the local development emulator
If you do not need to use a live account for development purposes, consider installing the Azure Storage Development emulator.
This does not cost anything and allows you to seamlessly switch from local to live storage accounts.

### Set auto refresh at a high value
Consider setting a relatively high schema refresh timeout e.g. 5 minutes to avoid repeated requests on a regular basis.

## Working with scripts
When working with scripts, bear in mind that the **running FSI session** *does not auto-refresh*. It is *only* the IDE itself e.g. Visual Studio,
Code or Rider etc. that benefits from hot schema loading. This can leave the IDE and FSI out of sync, and you may wish to occasionally
reset / reload FSI if schema changes occur in order to re-load the latest schema into FSI.
*)

