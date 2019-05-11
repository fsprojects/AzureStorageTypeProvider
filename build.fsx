// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake.BuildServer
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
Target.create  "AssemblyInfo" (fun _ ->
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
Target.create "Clean" (fun _ -> try Shell.cleanDirs [ "bin"; "temp"; "tests/integrationtests/bin" ] with _ -> () )

// --------------------------------------------------------------------------------------
// Restore project
Target.create "RestoreProject" (fun _ ->
    runDotNet "restore" projectPath
)

// --------------------------------------------------------------------------------------
// Build library project
Target.create "Build" (fun _ ->
    runDotNet "publish" projectPath
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target.create "ResetTestData" (fun _ ->
    let script = Path.GetFullPath (testPath + @"\ResetTestData.fsx")
    Emulators.startStorageEmulator()
    Fsi.exec (fun p -> 
        { p with 
            TargetProfile = Fsi.Profile.NetStandard
            WorkingDirectory = testPath
            ToolPath = Fsi.FsiTool.Internal
        }) script [""]
    |> snd
    |> Seq.iter(fun x -> printfn "%s" x))  ///ToBeFixed


let build project framework =
    DotNet.build (fun p ->
        { p with Framework = Some framework } ) project

// Run integration tests
let root = __SOURCE_DIRECTORY__
let testNetCoreDir = root </> "tests"  </> "IntegrationTests" </> "bin" </> "Release" </> "netcoreapp2.0" </> "win10-x64" </> "publish"

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

// --------------------------------------------------------------------------------------
// Build a NuGet package
Target.create "NuGet" (fun _ -> 
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

Target.create "Release" (fun _ ->
    let user =
        match Environment.environVar "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserInput "Username: "
    let pw =
        match Environment.environVar "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "Password: "
    let remote =
        CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Staging.stageAll ""
    Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion)

Target.create "LocalDeploy" (fun _ ->
    DirectoryInfo @"bin"
    |> DirectoryInfo.getMatchingFiles "*.nupkg"
    |> Seq.map(fun x -> x.FullName)
    |> Shell.copyFiles @"..\..\LocalPackages")

Target.create "BuildServerDeploy" (fun _ -> publishOnAppveyor buildDir)

Target.createFinal "PublishTestsResultsToAppveyor" (fun _ ->
    Trace.publish ImportData.BuildArtifact "TestOutput"
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Nuget"
  ==> "ResetTestData"
  ==> "RunTests"
  =?> ("LocalDeploy", BuildServer.buildServer = LocalBuild)
  =?> ("BuildServerDeploy", BuildServer.buildServer = AppVeyor)

"RunTests"
  ==> "Release"

Target.activateFinal "PublishTestsResultsToAppveyor"
Target.runOrDefault "RunTests"