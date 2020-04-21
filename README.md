# Azure Storage Type Provider

An F# [Type Provider](https://docs.microsoft.com/en/dotnet/fsharp/tutorials/type-providers/) that can be used to explore Azure Storage assets quickly and easily.

It allows lightweight access to your Azure storage data within the context of F# scripts or applications to allow CRUD operations quickly and easily.

Support exists reading and writing from Blobs, Tables and Queues, as well as fallback to the standard .NET Azure SDK.

## Building

Simply run `build.cmd` or `build.sh` if you're on Unix. You can then open this project in Visual Studio Code or Visual Studio.

### Developer build versus release build

This type provider specifies two "build paths" - developer and release. The developer build is the default.

The reason for this is due to how type providers with third-party dependencies work. To have the F# compiler recognize a Type Provider Design Time Component (TPDC), the `.dll` and _all of its dependencies_ must be copied into the `bin/{Configuration}/typeproviders/fsharp41/netstandard2.0/` folder, where `{Configuration}` is either `Debug` or `Release`.

F# editor tooling, including that for Visual Studio, issues a "Design-time build" that will build a project in the Debug configuration. This build does not perform a publish step, so all of the dependencies of a TPDTC will be missing unless they are explicitly copied from a publish step. This publish is done via the `build.cmd`/`build.sh` command by default.

**It is imperative that you run `build.cmd`/`build.sh` before editing this project in editor tooling**.

To reference your changes in the test project, you will need to run `build.cmd`/`build.sh` again.

### CI build

The CI build runs everything in `Release` mode. If you intend on developing the type provider instead of releasing it, do not build in release. If you do that, then you won't get good editor tooling support for the test project that references the type provider. This is an unfortunate consequence of how type providers are architected today, and it is indeed more difficult to develop type providers than normal F# code.

### Maintainer(s)

- [@isaacabraham](https://github.com/isaacabraham)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) - F# Community Project Incubation Space (repo management)
