///[omit]
module Option

open System

let internal ofNullable (value : Nullable<_>) = 
    if value.HasValue then Some value.Value
    else None

let internal toNullable = function
    | Some x -> Nullable x
    | None -> Nullable()