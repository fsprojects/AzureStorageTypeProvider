// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"

open Fake.AppVeyor
open Fake.Git
open Fake.Core.TargetOperators
open Fake.AssemblyInfoFile
open Fake.Core
open Fake.DotNet
open Fake.IO.Globbing.Operators
open System
open System.IO
open Fake.IO

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharp.Azure.StorageTypeProvider"
// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Allows easy access to Azure Storage assets through F# scripts."
// Pattern specifying assemblies to be tested using XUnit
let testAssemblies = "tests/**/bin/Release/netstandard2.0/*Tests*.exe"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "AzureStorageTypeProvider"

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let buildDir = "bin"

// Read additional information from the release notes document

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
let install = lazy DotNet.install DotNet.Versions.FromGlobalJson

let inline withWorkDir wd =
    DotNet.Options.lift install.Value
    >> DotNet.Options.withWorkingDirectory wd
let runDotNet cmd workingDir =
    let result =
        DotNet.exec (withWorkDir workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

// --------------------------------------------------------------------------------------
// Clean build results
Target.create "Clean" (fun _ -> Shell.cleanDirs [ "bin"; "temp"; "tests/integrationtests/bin" ])
Target.create "CleanDocs" (fun _ -> Shell.cleanDirs ["docs/output"])

// --------------------------------------------------------------------------------------
// Build library project
Target.create "Build" (fun _ ->
    DotNet.publish (fun p -> { p with Project = "FSharp.Azure.StorageTypeProvider.sln" }))

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target.create "ResetTestData" (fun _ ->
    AzureHelper.StartStorageEmulator()
    FSIHelper.executeFSI (__SOURCE_DIRECTORY__ </> @"tests\IntegrationTests") "ResetTestData.fsx" []
    |> snd
    |> Seq.iter(fun x -> printfn "%s" x.Message))

// Run integration tests
Target.create "RunTests" (fun _ ->
    DotNetCli.Build (fun p -> { p with Project = "UnitTests.sln" })
    FileHelper.CreateDir "TestOutput"
    !!(testAssemblies) |> Fake.Testing.Expecto.Expecto id)

// --------------------------------------------------------------------------------------
// Build a NuGet package
Target.create "NuGet" 
    (fun _ -> 
        NuGet (fun p ->
            { p with
                Authors = [ "Isaac Abraham" ]
                Project = project
                Title = "F# Azure Storage Type Provider"
                Summary = summary
                Description = "The F# Azure Storage Type Provider allows simple access to Blob, Table and Queue assets, using Azure Storage metadata to intelligently infer schema where possible, whilst providing a simple API for common tasks."
                Version = release.NugetVersion
                ReleaseNotes = release.Notes |> String.concat Environment.NewLine
                Tags = "azure, f#, fsharp, type provider, blob, table, queue, script"
                OutputPath = "bin"
                Dependencies = [ "WindowsAzure.Storage", "9.1.1" ]
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
        ExecProcess
            (fun info ->
                info.FileName <- "appveyor"
                info.Arguments <- arguments)
            (TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwith (sprintf "Failed to execute appveyor command: %s" arguments)
    trace "Published packages"

  let publishOnAppveyor folder =
    !! (folder + "*.nupkg")
    |> Seq.iter (sprintf "PushArtifact %s" >> execOnAppveyor)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target.create "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed")

let generateHelp fail debug =
    let args =
        [ if not debug then yield "--define:RELEASE"
          yield "--define:HELP"]

    if executeFSIWithArgs "docs/tools" "generate.fsx" args [] then
        traceImportant "Help generated"
    else
        if fail then failwith "generating help documentation failed"
        else traceImportant "generating help documentation failed"

Target.create "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    CopyFile buildDir "packages/FSharp.Core/lib/net40/FSharp.Core.sigdata"
    CopyFile buildDir "packages/FSharp.Core/lib/net40/FSharp.Core.optdata"

    generateHelp false false)

Target.create "GenerateHelpDebug" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp true true)

Target.create "KeepRunning" (fun _ ->    
    use watcher = !! "docs/content/**/*.*" |> WatchChanges (fun changes ->
         generateHelp false false)

    traceImportant "Waiting for help edits. Press any key to stop."
    System.Console.ReadKey() |> ignore
    watcher.Dispose())

Target.create "GenerateDocs" DoNothing

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Git.CommandHelper.runSimpleGitCommand tempDocsDir "rm . -f -r" |> ignore
    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir)

Target.create "Release" (fun _ ->
    let user =
        match getBuildParam "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion)

Target.create "LocalDeploy" (fun _ ->
    directoryInfo @"bin"
    |> filesInDirMatching "*.nupkg"
    |> Seq.map(fun x -> x.FullName)
    |> CopyFiles @"..\..\LocalPackages")

Target.create "BuildServerDeploy" (fun _ -> publishOnAppveyor buildDir)

Target.createFinal "PublishTestsResultsToAppveyor" (fun _ ->
    UploadTestResultsXml TestResultsType.Xunit "TestOutput")

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Nuget"
  ==> "ResetTestData"
  ==> "RunTests"
  =?> ("LocalDeploy", buildServer = LocalBuild)
  =?> ("BuildServerDeploy", buildServer = AppVeyor)

"CleanDocs"
  ==> "GenerateHelp"

"CleanDocs"
  ==> "GenerateHelpDebug"

"RunTests"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "Release"

"GenerateHelp"
  ==> "KeepRunning"

Target.activateFinal "PublishTestsResultsToAppveyor"
Target.runOrDefault "RunTests"
