module Elastacloud.FSharp.AzureTypeProvider.Utils

open System

/// Converts a Nullable<T> object into Option<T>
let asOption<'T when 'T:(new : unit -> 'T) and 'T:struct and 'T :> ValueType> (value:System.Nullable<'T>)  =
    if value.HasValue then Some value.Value else None

/// Converts a null object to an option type
let toOption data = if data = null then None else Some data