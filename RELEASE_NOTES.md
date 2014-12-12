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

### 1.2.0
* Upgrade to Azure Storage 4.3.0 SDK.