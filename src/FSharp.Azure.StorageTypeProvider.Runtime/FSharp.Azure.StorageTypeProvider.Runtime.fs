namespace FSharp.Azure.StorageTypeProvider.Runtime

#if !IS_DESIGNTIME
// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("FSharp.Azure.StorageTypeProvider.DesignTime.dll")>]
do ()
#endif