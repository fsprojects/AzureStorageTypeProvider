// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------
#r "packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open Fake.Git
open Fake.ReleaseNotesHelper
open System
open System.IO

// --------------------------------------------------------------------------------------
// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package 
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"
// The name of the project 
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharp.Azure.StorageTypeProvider"
// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Allows easy access to Azure Storage assets through F# scripts."
// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = """The F# Azure Storage Type Provider allows simple access to Blob, Table and Queue assets, using Azure Storage metadata to intelligently infer schema where possible, whilst providing a simple API for common tasks."""
// List of author names (for NuGet package)
let authors = [ "Isaac Abraham" ]
// Tags for your project (for NuGet package)
let tags = "azure, f#, fsharp, type provider, blob, table, queue, script"
// File system information 
// (<solutionFile>.sln is built during the building process)
let solutionFile = "UnitTests"
// Pattern specifying assemblies to be tested using XUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"
// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/fsprojects"
// The name of the project on GitHub
let gitName = "AzureStorageTypeProvider"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------
// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let release = parseReleaseNotes (File.ReadAllLines "RELEASE_NOTES.md")

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ -> 
    let fileName = "src/" + project + "/AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName [ Attribute.Title project
                                        Attribute.Product project
                                        Attribute.Description summary
                                        Attribute.Version release.AssemblyVersion
                                        Attribute.FileVersion release.AssemblyVersion ])
// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages
Target "Clean" (fun _ -> CleanDirs [ "bin"; "temp"; "tests/integrationtests/bin" ])

// Build library & test project
Target "Build" (fun _ -> 
    !!("*.sln")
    |> MSBuildRelease "" "Rebuild"
    |> ignore)
// --------------------------------------------------------------------------------------
// Testing
Target "ResetTestData" (fun _ ->
    let emulatorPath =
        [ "AzureStorageEmulator"; "WAStorageEmulator" ]
        |> List.map (combinePaths ProgramFilesX86 << sprintf @"Microsoft SDKs\Azure\Storage Emulator\%s.exe")
        |> List.tryFind fileExists
        |> function
        | None -> failwith "Unable to locate Azure Storage Emulator!"
        | Some file -> file

    { defaultParams with
        CommandLine = "start"
        Program = combinePaths ProgramFilesX86 emulatorPath } |> shellExec |> ignore
    FSIHelper.executeFSI (Path.Combine(__SOURCE_DIRECTORY__, @"tests\IntegrationTests")) "ResetTestData.fsx" []
    |> snd
    |> Seq.iter(fun x -> printfn "%s" x.Message)
)
// Run integration tests
Target "IntegrationTests" (fun _ ->
    !!(testAssemblies)
    |> xUnit (fun p -> { p with Verbose = true }))
// --------------------------------------------------------------------------------------
// Generate the documentation
Target "CleanDocs" (fun _ -> CleanDirs [ "docs/output" ])
Target "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed"
)

Target "GenerateHelp" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:HELP"] [] then
      failwith "generating help documentation failed"
)

Target "GenerateDocs" DoNothing

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)
// --------------------------------------------------------------------------------------
// Build a NuGet package
Target "Package" 
    (fun _ -> 
        NuGet (fun p ->
            { p with Authors = authors
                     Project = project
                     Title = "F# Azure Storage Type Provider"
                     Summary = summary
                     Description = description
                     Version = release.NugetVersion
                     ReleaseNotes = release.Notes |> String.concat Environment.NewLine
                     Tags = tags
                     OutputPath = "bin"
                     Dependencies = [ "WindowsAzure.Storage", "4.3.0" ]
                     References = [ "FSharp.Azure.StorageTypeProvider.dll" ]
                     Files = 
                         ([ "FSharp.Azure.StorageTypeProvider.xml"; "FSharp.Azure.StorageTypeProvider.dll"; "Microsoft.Data.Edm.dll"; 
                            "Microsoft.Data.OData.dll"; "Microsoft.Data.Services.Client.dll"; 
                            "Microsoft.WindowsAzure.Configuration.dll"; "Microsoft.WindowsAzure.Storage.dll"; 
                            "Newtonsoft.Json.dll"; "System.Spatial.dll" ] 
                          |> List.map (fun file -> @"..\bin\" + file, Some "lib/net40", None))
                          @ [ "StorageTypeProvider.fsx", None, None ] }) 
              ("nuget/" + project + ".nuspec"))
// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override
Target "All" DoNothing
Target "Release" DoNothing

"Build"
  ==> "CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "Release"

"Clean"
  ==> "AssemblyInfo"
  ==> "ResetTestData"
  ==> "Build"
  ==> "IntegrationTests"
  ==> "GenerateDocs"
  ==> "Package"
  ==> "All"

RunTargetOrDefault "All"

