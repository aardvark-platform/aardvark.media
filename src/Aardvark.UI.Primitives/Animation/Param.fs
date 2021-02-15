namespace Aardvark.UI.Anewmation

type Param<'Value> =
    {
        Value : 'Value
        Flag : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Param =

    let inline create (flag : bool) (value : 'Value) =
        { Value = value; Flag = flag }

    let inline signaled (value : 'Value) =
        value |> create true

    let inline unsignaled (value : 'Value) =
        value |> create false

    let inline set (value : 'U) (x : Param<'T>) =
        { Value = value; Flag = x.Flag }

    let inline map (mapping : 'T -> 'U) (x : Param<'T>) =
        { Value = mapping x.Value; Flag = x.Flag }

    let inline map2 (mapping : 'T1 -> 'T2 -> 'U) (x : Param<'T1>) (y : Param<'T2>) =
        { Value = mapping x.Value y.Value; Flag = x.Flag || y.Flag }

    let inline bind (mapping : 'T -> Param<'U>) (x : Param<'T>) =
        let output = mapping x.Value
        { Value = output.Value; Flag = x.Flag || output.Flag }

    let inline value (x : Param<'Value>) =
        x.Value

    let inline flag (x : Param<'Value>) =
        x.Flag

    module Operators =

        let inline ( ~~ ) (x : 'a) =
            unsignaled x

        let inline ( ! ) (x : Param<'a>) =
            x.Value

        let inline ( +. ) (x : Param<'a>) (y : Param<'b>) =
            (x, y) ||> map2 (+)

        let inline ( -. ) (x : Param<'a>) (y : Param<'b>) =
            (x, y) ||> map2 (-)

        let inline ( *. ) (x : Param<'a>) (y : Param<'b>) =
            (x, y) ||> map2 (*)

        let inline ( /. ) (x : Param<'a>) (y : Param<'b>) =
            (x, y) ||> map2 (/)

        let inline ( ~-. ) (x : Param<'a>) =
            x |> map (~-)

        module private ``Compiler Tests`` =
            let working() =
                let a : Param<int> = ~~4 *. ~~3
                let a : Param<int> = ~~4 *. (signaled 4)
                let a : Param<int> = (signaled 4) *. (signaled 4)
                let b = !a
                ()