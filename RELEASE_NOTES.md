### 0.9.0 - 8th May 2014
* Fixed nuget package deployment
* Sorted namespacing
* Refactored lots of code
* Added better error handling
* Introduced F# scaffold project structure

### 0.9.1 - 16th May 2014
* Fixed namespace clash between Type Provider and parent provider type class
* Added ability to retrieve the name of a blob and table programmatically
* Removed EntityId from the LightweightTableEntity and replaced with separate PartitionKey and RowKey properties
* LightweightTableEntity made public and changed to a class rather than a record

### 0.9.2 - 12th June 2014
* Fixed a bug whereby provided type constructors sometimes mixed up field names and their values.

### 1.0.0 - 14th June 2014
* Forced Get to provide a Partition Key as well as Row Key to semantically reflect that it can never return more than 1 result (this removes the exception path).

### 1.1.0 - 13th August 2014
* Perf improvement for batch inserts when building lightweight table entities.
* Azure Storage Queue support.

### 1.2.0 - 20th April 2015
* Upgrade to Azure Storage 4.3.0 SDK.
* Added hooks into native Azure Storage SDK directly in the type provider.
* Simplified some naming of methods on blobs.

### 1.3.0 - 27th August 2015
* Lots of work on documentation.
* Blobs now support connection string overrides.
* Support for Page Blobs.
* Go back to .NET 4.0 dependency - no need to push up to 4.5.1.
* No longer add Azure SDK references to client projects.
* Breaking change: downloadFolder() now requires the caller to explicitly start the Async workflow to being downloading.

### 1.4.0 - 28th October 2015
* Configuration file support.

### 1.4.1 - 8th January 2016
* Remove obsolete reference from StorageTypeProvider.fsx

### 1.5.0
* Better connection string management
* Eager table type loading
* Delete entire partitions
* Better handling with large batches
* Blob listing on folders