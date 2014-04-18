namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Elastacloud.AzureTypeProvider")>]
[<assembly: AssemblyProductAttribute("Elastacloud.AzureTypeProvider")>]
[<assembly: AssemblyDescriptionAttribute("Allows easy access to Azure assets such as Blob Storage through F# scripts.")>]
[<assembly: AssemblyVersionAttribute("0.9.0")>]
[<assembly: AssemblyFileVersionAttribute("0.9.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.9.0"
