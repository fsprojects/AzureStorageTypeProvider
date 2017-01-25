module ConnectionTests

open FSharp.Azure.StorageTypeProvider
open Swensen.Unquote
open Expecto

type SecondBlank = AzureTypeProvider<"Foo">
type FirstBlank = AzureTypeProvider<"", "Foo">
//type TwoPart = AzureTypeProvider<"devstoreaccount1", "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==">
//type FullPath = AzureTypeProvider<"DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;", "test">

[<Tests>]
let connectionTests =
    testList "Connection Tests" [
        testCase "SecondBlank connection string works" (fun _ -> Expect.isTrue (SecondBlank.Containers.CloudBlobClient.GetContainerReference("samples").Exists()) "")
        testCase "FirstBlank connection string works" (fun _ -> Expect.isTrue (FirstBlank.Containers.CloudBlobClient.GetContainerReference("samples").Exists()) "")
    ]
//[<Fact>]
//let ``TwoPart connection string works``() =
//    TwoPart.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =! true
//
//[<Fact>]
//let ``FullPath connection string works``() =
//    FullPath.Containers.CloudBlobClient.GetContainerReference("samples").Exists() =! true
//
