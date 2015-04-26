module Option

open System

let ofNullable (value : Nullable<_>) = 
    if value.HasValue then Some value.Value
    else None

let toNullable = function
    | Some x -> Nullable x
    | None -> Nullable()