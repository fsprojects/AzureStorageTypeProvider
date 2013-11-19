#if BOOT
open Fake
module FB = Fake.Boot
FB.Prepare {
    FB.Config.Default __SOURCE_DIRECTORY__ with
        NuGetDependencies =
            let (!!) x = FB.NuGetDependency.Create x
            [
                !!"FAKE"
                !!"NuGet.Build"
                !!"NuGet.Core"
                !!"NUnit.Runners"
            ]
}
#endif

#load ".build/boot.fsx"

open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.MSBuild

// properties
let projectName = "AzureTypeProvider"
let version = if isLocalBuild then "0.1." + System.DateTime.UtcNow.ToString("yMMdd") else buildVersion
let projectSummary = "An F# Azure Type Provider which can be used to explore e.g. Blob Storage and easily apply operations on Azure assets."
let projectDescription = projectSummary
let authors = ["Isaac Abraham";"Ryan Riley"]
let mail = "ryan.riley@panesofglass.org"
let homepage = "http://github.com/isaacabraham/AzureTypeProvider"
let license = "http://github.com/isaacabraham/AzureTypeProvider"

// directories
let buildDir = __SOURCE_DIRECTORY__ @@ "build"
let deployDir = __SOURCE_DIRECTORY__ @@ "deploy"
let packagesDir = __SOURCE_DIRECTORY__ @@ "packages"
let nugetDir = __SOURCE_DIRECTORY__ @@ "nuget"
let nugetLib = nugetDir @@ "lib/net40"
let template = __SOURCE_DIRECTORY__ @@ "template.html"
let sources = __SOURCE_DIRECTORY__ @@ "src"
let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docRoot = getBuildParamOrDefault "docroot" homepage

// tools
let nugetPath = "./.nuget/nuget.exe"
let nunitPath = "./packages/NUnit.Runners.2.6.2/tools"

// files
let appReferences =
    !! "src/Elastacloud.AzureTypeProvider.fsproj"

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir
               docsDir
               deployDir
               nugetDir
               nugetLib]
)

Target "BuildApp" (fun _ ->
    if not isLocalBuild then
        [ Attribute.Version(buildVersion)
          Attribute.Title(projectName)
          Attribute.Description(projectDescription)
          Attribute.Guid("fb7ca8f3-c158-49e9-b816-501741e2921f")
        ]
        |> CreateFSharpAssemblyInfo "src/AssemblyInfo.fs"

    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "CreateNuGet" (fun _ ->
    [ buildDir @@ "Elastacloud.AzureTypeProvider.dll"
      buildDir @@ "Elastacloud.AzureTypeProvider.pdb" ]
        |> CopyTo nugetLib

    let dependencies =
        [ "Microsoft.Data.Edm"
          "Microsoft.Data.OData"
          "Microsoft.WindowsAzure.ConfigurationManager"
          "System.Spatial"
          "WindowsAzure.Storage"
        ] |> List.map (fun name -> name, GetPackageVersion packagesDir name)

    NuGet (fun p -> 
            {p with               
                Authors = authors
                Project = projectName
                Description = projectDescription
                Version = version
                OutputPath = nugetDir
                ToolPath = nugetPath
                Dependencies = dependencies
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                Publish = hasBuildParam "nugetKey" })
        "Elastacloud.AzureTypeProvider.nuspec"

    !! (nugetDir @@ sprintf "Elastacloud.AzureTypeProvider.%s.nupkg" version)
        |> CopyTo deployDir
)

Target "Deploy" DoNothing
Target "Default" DoNothing

// Build order
"Clean"
  ==> "BuildApp"
  ==> "CreateNuGet"
  ==> "Deploy"

"Default" <== ["Deploy"]

// Start build
RunTargetOrDefault "Default"

