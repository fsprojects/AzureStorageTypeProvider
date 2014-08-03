namespace FSharp.Azure.StorageTypeProvider 
    module internal Utils =
        open System

        let toOption (value : Nullable<_>) = 
            if value.HasValue then Some value.Value
            else None
        
        let toNullable = function
                         | Some x -> Nullable x
                         | None -> Nullable()