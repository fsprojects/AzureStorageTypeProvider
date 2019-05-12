// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "netstandard"
#endif

open Fake.Core.TargetOperators
open Fake.Core
open Fake.DotNet
open Fake.IO.Globbing.Operators
open System
open System.IO
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

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let buildDir = "bin"


// TypeProvider path
let projectPath = Path.getFullName "./src/FSharp.Azure.StorageTypeProvider"

// Test path
let testPath = Path.getFullName "./tests/IntegrationTests"
// Read additional information from the release notes document

// Test Output Dir
let testOutPutDir = "TestOutput"


let release =
    File.ReadAllLines "RELEASE_NOTES.md"
    |> ReleaseNotes.parseAll
    |> List.head

// Generate assembly info files with the right version & up-to-date information
Target.Create  "AssemblyInfo" (fun _ ->
    let fileName = "src/" + project + "/AssemblyInfo.fs"
    AssemblyInfoFile.createFSharp fileName [    AssemblyInfo.Title project
                                                AssemblyInfo.Product project
                                                AssemblyInfo.Description summary
                                                AssemblyInfo.Version release.AssemblyVersion
                                                AssemblyInfo.FileVersion release.AssemblyVersion ])

let inline withWorkDir wd = 
    DotNet.Options.withWorkingDirectory wd
let runDotNet cmd workingDir =
    let result =
        DotNet.exec (withWorkDir workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

// --------------------------------------------------------------------------------------
// Clean build results
Target.Create "Clean" (fun _ ->
    try Shell.cleanDirs [ "bin"; "temp"; "tests/integrationtests/bin" ] with _ -> ()
)

// --------------------------------------------------------------------------------------
// Restore project
Target.Create "RestoreProject" (fun _ ->
    DotNet.restore id projectPath
)

// --------------------------------------------------------------------------------------
// Build library project
Target.Create "Build" (fun _ ->
    DotNet.publish id projectPath
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target.Create "ResetTestData" (fun _ ->
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
let testNetCoreDir = root </> "tests"  </> "IntegrationTests" </> "bin" </> "Release" </> "netcoreapp2.0" </> "win10-x64" </> "publish"

Target.Create "RunTests" (fun _ ->
    let result = DotNet.exec (withWorkDir testPath) "publish --self-contained -c Release -r win10-x64" ""
    if not result.OK then failwith "Publish failed"
    printfn "Dkr: %s" testNetCoreDir
    let result = DotNet.exec (withWorkDir testNetCoreDir) "" "IntegrationTests.dll"
    if result.OK then
        printfn "Expecto for netcore finished without Errors"
    else 
        printfn "Expecto for netcore finished with Errors"
    )

// --------------------------------------------------------------------------------------
// Build a NuGet package
Target.Create "NuGet" (fun _ -> 
    Fake.IO.Directory.create @"bin\package"
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
            OutputPath = @"bin\package"
            Dependencies = [ "WindowsAzure.Storage", "9.3.2"
                             "FSharp.Compiler.Tools", "10.2.1" ]
            References = [ "FSharp.Azure.StorageTypeProvider.dll" ]
            Files = 
                ([ "FSharp.Azure.StorageTypeProvider.xml"; "FSharp.Azure.StorageTypeProvider.dll"
                   "Microsoft.WindowsAzure.Storage.dll"; "Newtonsoft.Json.dll" ] 
                 |> List.map (fun file -> "../bin/netstandard2.0/publish/" + file, Some "lib/netstandard2.0", None))
                 @ [ "StorageTypeProvider.fsx", None, None ] }) 
          ("nuget/" + project + ".nuspec"))

[<AutoOpen>]
module AppVeyorHelpers =
  let execOnAppveyor arguments =
    let result =
        Process.execSimple(fun info ->
            { info with
                FileName = "appveyor"
                Arguments = arguments})
            (TimeSpan.FromMinutes 2.)
    if result <> 0 then failwith (sprintf "Failed to execute appveyor command: %s" arguments)
    Trace.trace "Published packages"

  let publishOnAppveyor folder =
    !! (folder + "*.nupkg")
    |> Seq.iter (sprintf "PushArtifact %s" >> execOnAppveyor)

// --------------------------------------------------------------------------------------
// Release Scripts
Target.Create "LocalDeploy" (fun _ ->
    DirectoryInfo @"bin"
    |> DirectoryInfo.getMatchingFiles "*.nupkg"
    |> Seq.map(fun x -> x.FullName)
    |> Shell.copyFiles @"..\..\LocalPackages")

Target.Create "BuildServerDeploy" (fun _ -> publishOnAppveyor buildDir)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.Create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Nuget"
  ==> "ResetTestData"
  ==> "RunTests"
  =?> ("LocalDeploy", BuildServer.isLocalBuild)
  =?> ("BuildServerDeploy", BuildServer.buildServer = Fake.Core.BuildServer.AppVeyor)

Target.RunOrDefault "RunTests"