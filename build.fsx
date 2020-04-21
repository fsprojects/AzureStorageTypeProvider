// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "netstandard"
#endif

open System
open System.IO

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO.Globbing.Operators
open Fake.IO
open Fake.Azure
open Fake.DotNet.NuGet
open Fake.Tools.Git
open Fake.IO.FileSystemOperators

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharp.Azure.StorageTypeProvider"
// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Allows easy access to Azure Storage assets through F# scripts."

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "AzureStorageTypeProvider"

let designTimeProject = __SOURCE_DIRECTORY__ </> "src" </> "FSharp.Azure.StorageTypeProvider.DesignTime" </> "FSharp.Azure.StorageTypeProvider.DesignTime.fsproj"

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

// Test path
let testPath = Path.getFullName "./tests/IntegrationTests"

let buildConfiguration = DotNet.Custom <| Environment.environVarOrDefault "configuration" "Debug"

let release =
    File.ReadAllLines "RELEASE_NOTES.md"
    |> ReleaseNotes.parseAll
    |> List.head

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion ]

    let getProjectDetails projectPath =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        (Path.GetDirectoryName(projectPath), getAssemblyInfoAttributes projectName)

    !! "src/**/*.fsproj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes )
)

let inline withWorkDir wd = 
    DotNet.Options.withWorkingDirectory wd

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    !! "**/**/bin/" |> Shell.cleanDirs
    !! "**/**/obj/" |> Shell.cleanDirs
    ["bin"; "temp"] |> Shell.cleanDirs
)

// --------------------------------------------------------------------------------------
// Build

Target.create "BuildForDev" (fun _ ->
    DotNet.publish (fun p -> { p with Configuration = DotNet.BuildConfiguration.Debug }) designTimeProject |> ignore
    DotNet.exec id "build" "FSharp.Azure.StorageTypeProvider.sln -c Debug" |> ignore
)

Target.create "BuildForRelease" (fun _ ->
    DotNet.publish (fun p -> { p with Configuration = DotNet.BuildConfiguration.Release }) designTimeProject |> ignore
    DotNet.exec id "build" "FSharp.Azure.StorageTypeProvider.sln -c Release" |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target.create "ResetTestData" (fun _ ->
    let script = Path.Combine(testPath, "ResetTestData.fsx")
    Emulators.startStorageEmulator()
    Fsi.exec (fun p -> 
        { p with 
            TargetProfile = Fsi.Profile.Netcore
            WorkingDirectory = testPath
        }) script [""]
    |> snd
    |> Seq.iter(fun x -> printfn "%s" x))  ///ToBeFixed

// Run integration tests
let root = __SOURCE_DIRECTORY__
let testNetCoreDir = root </> "tests"  </> "IntegrationTests" </> "bin" </> "Release" </> "netcoreapp3.1" </> "win10-x64" </> "publish"

Target.create "RunTests" (fun _ ->
    let result = DotNet.exec (withWorkDir testPath) "publish --self-contained -c Release -r win10-x64" ""
    if not result.OK then failwith "Publish failed"
    printfn "Dkr: %s" testNetCoreDir
    let result = DotNet.exec (withWorkDir testNetCoreDir) "" "IntegrationTests.dll"
    if result.OK then
        printfn "Expecto for netcore finished without Errors"
    else 
        printfn "Expecto for netcore finished with Errors"
    )

// Update package with this stuff?
// Microsoft.Azure.Storage.Blob
// Microsoft.Azure.Storage.File
// Microsoft.Azure.Storage.Queue
// Microsoft.Azure.Storage.Common
// Microsoft.Azure.Cosmos.Table

// --------------------------------------------------------------------------------------
// Build a NuGet package
Target.create "NuGet" (fun _ -> 
    Fake.IO.Directory.create @"pkg"
    NuGet.NuGet (fun p ->
        { p with
            Authors = [ "Isaac Abraham" ]
            Project = project
            Title = "F# Azure Storage Type Provider"
            Summary = summary
            Description = "The F# Azure Storage Type Provider allows simple access to Blob, Table and Queue assets, using Azure Storage metadata to intelligently infer schema where possible, whilst providing a simple API for common tasks."
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> String.concat Environment.NewLine
            Tags = "azure, f#, fsharp, type provider, blob, table, queue, script"
            OutputPath = @"pkg"
            Dependencies = [ "Microsoft.Azure.Storage.Blob", "11.1.3"
                             "Microsoft.Azure.Storage.File", "11.1.3"
                             "Microsoft.Azure.Storage.Queue", "11.1.3"
                             "Microsoft.Azure.Storage.Common", "11.1.3"
                             "Microsoft.Azure.Cosmos.Table", "1.0.7" ]
            References = [ "FSharp.Azure.StorageTypeProvider.dll" ]
            Files = 
                ([ "FSharp.Azure.StorageTypeProvider.xml"; "FSharp.Azure.StorageTypeProvider.dll"
                   "Microsoft.WindowsAzure.Storage.dll"; "Newtonsoft.Json.dll" ] 
                 |> List.map (fun file -> "../bin/netstandard2.0/publish/" + file, Some "lib/netstandard2.0", None))
                 @ [ "StorageTypeProvider.fsx", None, None ] }) 
          ("nuget/" + project + ".nuspec"))

// [<AutoOpen>]
// module AppVeyorHelpers =
//   let execOnAppveyor arguments =
//     let result =
//         Process.execSimple(fun info ->
//             { info with
//                 FileName = "appveyor"
//                 Arguments = arguments})
//             (TimeSpan.FromMinutes 2.)
//     if result <> 0 then failwith (sprintf "Failed to execute appveyor command: %s" arguments)
//     Trace.trace "Published packages"

//   let publishOnAppveyor folder =
//     !! (folder + "*.nupkg")
//     |> Seq.iter (sprintf "PushArtifact %s" >> execOnAppveyor)

// --------------------------------------------------------------------------------------
// Release Scripts
// Target.create "LocalDeploy" (fun _ ->
//     DirectoryInfo @"bin"
//     |> DirectoryInfo.getMatchingFiles "*.nupkg"
//     |> Seq.map(fun x -> x.FullName)
//     |> Shell.copyFiles @"..\..\LocalPackages")

// Target.create "BuildServerDeploy" (fun _ -> publishOnAppveyor buildDir)

"Clean"
  ==> "AssemblyInfo"
  ==> "BuildForDev"

"Clean"
==> "AssemblyInfo"
==> "BuildForRelease"
//   ==> "Nuget"
//   ==> "ResetTestData"
//   ==> "RunTests"
//   =?> ("LocalDeploy", BuildServer.isLocalBuild)
//   =?> ("BuildServerDeploy", BuildServer.buildServer = Fake.Core.BuildServer.AppVeyor)

Target.runOrDefault "BuildForDev"