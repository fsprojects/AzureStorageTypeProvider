namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Azure.StorageTypeProvider")>]
[<assembly: AssemblyProductAttribute("FSharp.Azure.StorageTypeProvider")>]
[<assembly: AssemblyDescriptionAttribute("Allows easy access to Azure Storage assets through F# scripts.")>]
[<assembly: AssemblyVersionAttribute("1.6.0")>]
[<assembly: AssemblyFileVersionAttribute("1.6.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.6.0"
