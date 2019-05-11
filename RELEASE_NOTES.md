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

### 1.5.0 - 24th June 2016
* Better connection string management
* Eager table type loading
* Delete entire partitions
* Better handling with large batches
* Blob listing on folders

### 1.6.0 - 27th July 2016
* Async support for Tables
* BREAKING CHANGE: Lazy deserialization of AsString and AsBytes members on Queue messages
* BREAKING CHANGE: Automatic inference of option types for fields that are missing for some rows
* Name property on containers
* Visibility Timeout control on queue messages
* Minor bug fixes

### 1.6.1 - 3rd August 2016
* Improve synchronous bulk table operation performance
* Fix asynchronous bulk table operation error handling

### 1.7.0 - 6th October 2016
* Support for Azure Metrics tables.
* Support for simple humanization of table properties.

### 1.8.0 - 28th March 2017
* Upgrade to F# 4.0
* Upgrade to .NET452
* Support for static blob schemas

### 1.9.0 - 6th May 2017
* Better support for programmatic blob access
* Static Table schema support
* BREAKING CHANGE: Size property on Blobs changed to a method.
* BREAKING CHANGE: Reworked static Blob schema

### 1.9.1 - 8th May 2017
* Support for specifying blob type in blob schema definition.

### 1.9.2 - 31st May 2017
* Support for MIME type assignment on blob upload.
* Support for blob file and container properties
* Upgrade to Azure Storage 7.2.1

### 1.9.3 - 25th August 2017
* Blob and Queue SAS tokens are now configurable.
* Ability to supply a prefix when listing blobs.

### 1.9.4 - 29th October 2017
* Hot Schema Loading.

### 1.9.5 - 9th November 2017
* Re-enable storage metrics support.

### 2.0.0b - 12th November 2018
* Upgrade to NET Standard 2.0
* Upgrade Azure Storage to v9
* Upgrade many other dependencies
* Rationalise many API implementations
* Minor changes to API to expose Async methods on provided types

### 2.0.0 - 28th December 2018
* Support for blob-only accounts

### 2.0.1 - 05th February 2019
* Fix #119 (Even when JSON tableSchema parameter is used Emulator required to be running)

### 2.0.2 - 7th April 2019
* Remove dependency on FSharp.Compiler.Tools.