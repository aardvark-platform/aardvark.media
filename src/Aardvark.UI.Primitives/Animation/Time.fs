namespace Aardvark.UI.Anewmation

open Aardvark.Base

[<Struct>]
[<RequireQualifiedAccess>]
[<DefaultAugmentation(false)>]
type Duration =
    | Finite of MicroTime
    | Infinite

    member x.IsZero =
        match x with | Duration.Finite t -> t.IsZero | _ -> false

    member x.IsFinite =
        match x with | Duration.Finite _ -> true | _ -> false

    member x.IsInfinite =
        not x.IsFinite

    static member inline (*) (x : Duration, y : ^Value) =
        match x with
        | Finite d -> Finite (d * y)
        | Infinite -> Infinite

    static member inline (*) (y : ^Value, x : Duration) =
        match x with
        | Finite d -> Finite (d * y)
        | Infinite -> Infinite

    static member inline (/) (x : Duration, y : Duration) =
        match x, y with
        | Duration.Finite a, Duration.Finite b -> a / b
        | Duration.Finite _, Duration.Infinite -> 0.0
        | Duration.Infinite, Duration.Infinite -> nan
        | Duration.Infinite, Duration.Finite _ -> infinity


[<Struct>]
[<RequireQualifiedAccess>]
type GlobalTime =
    | Timestamp of MicroTime
    | Infinity

    static member inline (+) (x : GlobalTime, y : LocalTime) =
        match x, y with
        | Timestamp t, LocalTime.Offset o -> Timestamp (t + o)
        | _ -> Infinity

    static member inline (+) (x : LocalTime, y : GlobalTime) =
        y + x

    static member inline (+) (x : GlobalTime, y : Duration) =
        match x, y with
        | Timestamp t, Duration.Finite d -> Timestamp (t + d)
        | _ -> Infinity

    static member inline (+) (x : Duration, y : GlobalTime) =
        y + x

    static member inline (-) (x : GlobalTime, y : GlobalTime) =
        match x, y with
        | Timestamp a, Timestamp b -> Timestamp (a - b)
        | _ -> Infinity

    static member inline (-) (x : GlobalTime, y : LocalTime) =
        match x, y with
        | Timestamp t, LocalTime.Offset o -> Timestamp (t - o)
        | _ -> Infinity


and [<Struct; RequireQualifiedAccess>] LocalTime =
    | Offset of MicroTime
    | Infinity

    static member inline (+) (x : LocalTime, y : LocalTime) =
        match x, y with
        | Offset t, Offset o -> Offset (t + o)
        | _ -> Infinity

    static member inline (+) (x : LocalTime, y : Duration) =
        match x, y with
        | Offset t, Duration.Finite d -> Offset (t + d)
        | _ -> Infinity

    static member inline (+) (x : Duration, y : LocalTime) =
        y + x

    static member inline (-) (x : LocalTime, y : LocalTime) =
        match x, y with
        | Offset a, Offset b -> Offset (a - b)
        | _ -> Infinity

    static member inline (/) (x : LocalTime, y : Duration) =
        match x, y with
        | Offset a, Duration.Finite b -> a / b
        | Offset _, Duration.Infinite -> 0.0
        | Infinity, Duration.Infinite -> nan
        | Infinity, Duration.Finite _ -> infinity


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Duration =

    let zero = Duration.Finite MicroTime.zero

    let inline ofNanoseconds (ns : ^Nanoseconds)   = Duration.Finite <| MicroTime(int64 ns)
    let inline ofMicroseconds (us : ^Microseconds) = Duration.Finite <| MicroTime.FromMicroseconds(float us)
    let inline ofMilliseconds (us : ^Milliseconds) = Duration.Finite <| MicroTime.FromMilliseconds(float us)
    let inline ofSeconds (us : ^Seconds)           = Duration.Finite <| MicroTime.FromSeconds(float us)
    let inline ofMinutes (us : ^Minutes)           = Duration.Finite <| MicroTime.FromMinutes(float us)

    let isZero (d : Duration) = d.IsZero
    let isFinite (d : Duration) = d.IsFinite
    let isInfinite (d : Duration) = d.IsInfinite

    let ofLocalTime = function
        | LocalTime.Offset o -> Duration.Finite o
        | LocalTime.Infinity -> Duration.Infinite

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalTime =

    let zero =
        LocalTime.Offset MicroTime.zero

    let relative (relativeTo : GlobalTime) (time : GlobalTime) =
        match relativeTo, time with
        | GlobalTime.Timestamp a, GlobalTime.Timestamp t -> LocalTime.Offset (t - a)
        | _ -> Log.warn "[Animation] Trying to compute local time from infinity"; LocalTime.Infinity

    let max = function
        | Duration.Finite d -> LocalTime.Offset d
        | Duration.Infinite -> LocalTime.Infinity

    let get (duration : Duration) (t : float) =
        max (t * duration)