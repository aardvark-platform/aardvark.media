namespace Aardvark.UI.Anewmation

open System

type private ValueCache<'Value> =
    struct
        val mutable Creator : Func<'Value>
        val mutable Cache : ValueOption<'Value>

        new (creator : unit -> 'Value) =
            { Creator = Func<_> creator; Cache = ValueNone }

        member x.Value =
            match x.Cache with
            | ValueSome v -> v
            | _ ->
                let v = x.Creator.Invoke()
                x.Creator <- null
                x.Cache <- ValueSome v
                v
    end