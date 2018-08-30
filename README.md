Azure Storage Type Provider
===========================

An F# Azure Type Provider which can be used to explore Azure Storage assets quickly and easily.

The goal is to create a provider which allows lightweight access to your Azure storage data within the context of e.g. F# scripts or applications to allow CRUD operations quickly and easily.

Support exists reading and writing from Blobs, Tables and Queues, as well as fallback to the standard .NET Azure SDK.

The provider currently does not support .NET Core for the reasons specified [here](https://github.com/fsprojects/AzureStorageTypeProvider/issues/111#issuecomment-417208922).

[![AppVeyor build status](https://ci.appveyor.com/api/projects/status/github/fsprojects/AzureStorageTypeProvider?svg=true)](https://ci.appveyor.com/project/fsprojectsgit/azurestoragetypeprovider)

### Maintainer(s)

- [@isaacabraham](https://github.com/isaacabraham)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) - F# Community Project Incubation Space (repo management)
