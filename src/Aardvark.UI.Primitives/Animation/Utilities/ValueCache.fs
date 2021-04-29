namespace Aardvark.UI.Anewmation

type private ValueCache<'Value>(creator : unit -> 'Value) =
    let mutable cache = ValueNone

    member x.Value =
        match cache with
        | ValueSome v -> v
        | _ ->
            let v = creator()
            cache <- ValueSome v
            v