namespace Aardvark.UI

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Text
open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive

[<AutoOpen>]
module internal ``Internal Utilities`` =
    let typename<'T> = ReflectionHelpers.getPrettyName typeof<'T>

    type ArraySegment<'T> with
        member inline x.Item(index : int) = x.Array.[x.Offset + index]

    type private EmptyAdaptiveObject() =
        inherit AdaptiveObject()

    type AdaptiveObject with
        static member Create() : AdaptiveObject = EmptyAdaptiveObject()

    [<Struct>]
    type CountdownEventDisposable(event: CountdownEvent, count: int) =
        member _.Dispose() = event.Signal count |> ignore
        interface IDisposable with member this.Dispose() = this.Dispose()

    type CountdownEvent with
        member inline this.Acquire([<Optional; DefaultParameterValue(1)>] count: int) =
            this.AddCount count
            new CountdownEventDisposable(this, count)

    type Encoding with
        member inline this.GetString(data: ArraySegment<byte>) = this.GetString(data.Array, data.Offset, data.Count)
        member inline this.TryGetString(data: ArraySegment<byte>) = try this.GetString(data) with _ -> null

    [<AutoOpen>]
    module Patterns =

        [<return: Struct>]
        let (|Int|_|) (str: string) =
            match Int32.TryParse str with
            | true, v -> ValueSome v
            | _ -> ValueNone

        [<return: Struct>]
        let (|C4f|_|) (str: string) =
            match C4f.TryParse str with
            | true, v -> ValueSome v
            | _ -> ValueNone

        [<return: Struct>]
        let (|Guid|_|) (str: string) =
            match Guid.TryParse str with
            | true, guid -> ValueSome guid
            | _ -> ValueNone

    [<AutoOpen>]
    module ``Time Extensions`` =
        let private sw = Stopwatch()
        let private start = MicroTime(TimeSpan.FromTicks(DateTime.Now.Ticks))
        do sw.Start()

        type MicroTime with
            static member Now = start + sw.MicroTime