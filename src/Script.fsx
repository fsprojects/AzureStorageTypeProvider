#I """..\build"""
#r "Microsoft.WindowsAzure.Storage.dll"
#r "Elastacloud.AzureTypeProvider.dll"

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Elastacloud.FSharp.AzureTypeProvider

type Azure = AzureAccount<"account", "secretkey">
