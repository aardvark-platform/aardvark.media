namespace Aardvark.UI.Animation

open Aardvark.Base

[<StructuredFormatDisplay("{AsString}")>]
type Duration =
    struct
        val MicroTime : MicroTime

        new (time: MicroTime) = { MicroTime = time }

        member inline this.IsZero             = this.MicroTime.IsZero
        member inline this.IsFinite           = this.MicroTime.IsFinite
        member inline this.IsInfinite         = this.MicroTime.IsInfinite
        member inline this.TotalMinutes       = this.MicroTime.TotalMinutes
        member inline this.TotalSeconds       = this.MicroTime.TotalSeconds
        member inline this.TotalMilliseconds  = this.MicroTime.TotalMilliseconds
        member inline this.TotalMicroseconds  = this.MicroTime.TotalMicroseconds

        static member inline (*) (x : Duration, y : float) = Duration (x.MicroTime * y)
        static member inline (*) (x : Duration, y : int)   = Duration (x.MicroTime * y)
        static member inline (*) (x : float, y : Duration) = Duration (x * y.MicroTime)
        static member inline (*) (x : int, y : Duration)   = Duration (x * y.MicroTime)
        static member inline (/) (x : Duration, y : Duration) = x.MicroTime / y.MicroTime

        member private this.AsString = this.ToString()
        override this.ToString() = this.MicroTime.ToString()
    end

[<StructuredFormatDisplay("{AsString}")>]
type GlobalTime =
    struct
        val MicroTime : MicroTime

        new (time: MicroTime) = { MicroTime = time }

        member inline this.IsZero             = this.MicroTime.IsZero
        member inline this.IsFinite           = this.MicroTime.IsFinite
        member inline this.IsInfinite         = this.MicroTime.IsInfinite
        member inline this.TotalMinutes       = this.MicroTime.TotalMinutes
        member inline this.TotalSeconds       = this.MicroTime.TotalSeconds
        member inline this.TotalMilliseconds  = this.MicroTime.TotalMilliseconds
        member inline this.TotalMicroseconds  = this.MicroTime.TotalMicroseconds

        static member inline (+) (x : GlobalTime, y : GlobalTime) = GlobalTime (x.MicroTime + y.MicroTime)
        static member inline (+) (x : GlobalTime, y : LocalTime) = GlobalTime (x.MicroTime + y.MicroTime)
        static member inline (+) (x : LocalTime, y : GlobalTime) = GlobalTime (x.MicroTime + y.MicroTime)
        static member inline (-) (x : GlobalTime, y : GlobalTime) = GlobalTime (x.MicroTime - y.MicroTime)
        static member inline (-) (x : GlobalTime, y : LocalTime) = GlobalTime (x.MicroTime - y.MicroTime)
        static member inline (-) (x : LocalTime, y : GlobalTime) = GlobalTime (x.MicroTime - y.MicroTime)
        static member inline (+) (x : GlobalTime, y : Duration) = GlobalTime (x.MicroTime + y.MicroTime)
        static member inline (+) (x : Duration, y : GlobalTime) = GlobalTime (x.MicroTime + y.MicroTime)

        member private this.AsString = this.ToString()
        override this.ToString() = this.MicroTime.ToString()
    end

and [<StructuredFormatDisplay("{AsString}")>] LocalTime =
    struct
        val MicroTime : MicroTime

        new (time: MicroTime) = { MicroTime = time }

        member inline this.IsZero             = this.MicroTime.IsZero
        member inline this.IsFinite           = this.MicroTime.IsFinite
        member inline this.IsInfinite         = this.MicroTime.IsInfinite
        member inline this.TotalMinutes       = this.MicroTime.TotalMinutes
        member inline this.TotalSeconds       = this.MicroTime.TotalSeconds
        member inline this.TotalMilliseconds  = this.MicroTime.TotalMilliseconds
        member inline this.TotalMicroseconds  = this.MicroTime.TotalMicroseconds

        static member inline (+) (x : LocalTime, y : LocalTime) = LocalTime (x.MicroTime + y.MicroTime)
        static member inline (-) (x : LocalTime, y : LocalTime) = LocalTime (x.MicroTime - y.MicroTime)
        static member inline (+) (x : LocalTime, y : Duration) = LocalTime (x.MicroTime + y.MicroTime)
        static member inline (+) (x : Duration, y : LocalTime) = LocalTime (x.MicroTime + y.MicroTime)
        static member inline (/) (x : LocalTime, y : Duration) = x.MicroTime / y.MicroTime

        member private this.AsString = this.ToString()
        override this.ToString() = this.MicroTime.ToString()
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Duration =
    let zero = Duration MicroTime.zero
    let infinite = Duration MicroTime.PositiveInfinity

    let inline ofNanoseconds (ns : ^Nanoseconds)   = Duration <| MicroTime(int64 ns)
    let inline ofMicroseconds (us : ^Microseconds) = Duration <| MicroTime.FromMicroseconds(float us)
    let inline ofMilliseconds (ms : ^Milliseconds) = Duration <| MicroTime.FromMilliseconds(float ms)
    let inline ofSeconds (s : ^Seconds)            = Duration <| MicroTime.FromSeconds(float s)
    let inline ofMinutes (m : ^Minutes)            = Duration <| MicroTime.FromMinutes(float m)
    let inline ofLocalTime (t : LocalTime)         = Duration t.MicroTime

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalTime =
    let zero = LocalTime MicroTime.zero
    let infinite = LocalTime MicroTime.PositiveInfinity

    let relative (relativeTo : GlobalTime) (time : GlobalTime) =
        if relativeTo.IsFinite && time.IsFinite then
            LocalTime (time.MicroTime - relativeTo.MicroTime)
        else
            Log.warn "[Animation] Trying to compute local time from infinity"
            infinite

    let inline max (duration : Duration)             = LocalTime duration.MicroTime
    let inline get (duration : Duration) (t : float) = max (t * duration)