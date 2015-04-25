module FSharp.Azure.StorageTypeProvider.``Connection Tests``

open FSharp.Azure.StorageTypeProvider
open Swensen.Unquote
open Xunit

type SecondBlank = AzureTypeProvider<"Foo">
type FirstBlank = AzureTypeProvider<"", "Foo">
//type TwoPart = AzureTypeProvider<"devstoreaccount1", "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==">
//type FullPath = AzureTypeProvider<"DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;", "test">

[<Fact>]
let ``SecondBlank connection string works``() =
    SecondBlank.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =? true

[<Fact>]
let ``FirstBlank connection string works``() =
    FirstBlank.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =? true

//[<Fact>]
//let ``TwoPart connection string works``() =
//    TwoPart.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =? true
//
//[<Fact>]
//let ``FullPath connection string works``() =
//    FullPath.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =? true
//
