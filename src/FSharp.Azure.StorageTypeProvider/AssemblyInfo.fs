namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Azure.StorageTypeProvider")>]
[<assembly: AssemblyProductAttribute("FSharp.Azure.StorageTypeProvider")>]
[<assembly: AssemblyDescriptionAttribute("Allows easy access to Azure Storage assets through F# scripts.")>]
[<assembly: AssemblyVersionAttribute("0.9.2")>]
[<assembly: AssemblyFileVersionAttribute("0.9.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.9.2"
