namespace MyNamespace

open System

// Put any utilities here
[<AutoOpen>]
module internal Utilities = 

    let x = 1

// Put any runtime constructs here
type DataSource(filename:string) = 
    member this.FileName = filename


// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("FSharp.Azure.StorageTypeProvider.DesignTime.dll")>]
do ()
