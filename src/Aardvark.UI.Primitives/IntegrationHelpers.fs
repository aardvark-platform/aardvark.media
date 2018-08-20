namespace Aardvark.UI.Primitives

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Service

open Aardvark.UI.Primitives

open System
open System.Runtime.InteropServices
open System.Security
open Aardvark.Base

module MultimediaTimer =

    open System.Threading

    module private Windows = 
        type MultimediaTimerCallbackDel  = delegate of uint32 * uint32 * nativeint * uint32 * uint32 -> unit

        [<DllImport("winmm.dll", SetLastError = true); SuppressUnmanagedCodeSecurity>]
        extern uint32 timeSetEvent(uint32 msDelay, uint32 msResolution, nativeint callback, uint32& userCtx, uint32 eventType)
            
        [<DllImport("winmm.dll", SetLastError = true); SuppressUnmanagedCodeSecurity>]
        extern void timeKillEvent(uint32 timer)

        let start (interval : int) (callback : unit -> unit) =
            let del = MultimediaTimerCallbackDel(fun _ _ _ _ _ -> callback())
            let ptr = Marshal.PinDelegate del
            let mutable user = 0u
            let id = timeSetEvent(uint32 interval, 1u, ptr.Pointer, &user, 1u)
            if id = 0u then
                let err = Marshal.GetLastWin32Error()
                ptr.Dispose()
                failwithf "[Timer] could not start timer: %A" err
            else
                { new IDisposable with
                    member x.Dispose() =
                        timeKillEvent(id)
                        ptr.Dispose() 
                }
            
    module private Linux =
        
        [<DllImport("libc"); SuppressUnmanagedCodeSecurity>]
        extern void usleep(int usec)

        let start (interval : int) (callback : unit -> unit) =
            let run () =
                while true do
                    usleep(interval * 1000)
                    callback()
            let thread = new Thread(ThreadStart(run), IsBackground = true)
            thread.Start()

            { new IDisposable with
                member x.Dispose() = thread.Abort()
            }

    type Trigger(ms : int) =
        let ticksPerMillisecond = int64 TimeSpan.TicksPerMillisecond 
        let pulse = obj()

        let callback() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )
                
        let timer = 
            match Environment.OSVersion  with
                | Windows -> Windows.start ms callback
                | _ -> Linux.start ms callback
        
        member x.Wait() =
            lock pulse (fun () ->
                Monitor.Wait pulse |> ignore
            )  

        member x.Signal() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )  
            
        member x.Dispose() =
            timer.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type Timer(ms : int) =
        let s = Event<TimeSpan>()
        let ticksPerMillisecond = int64 TimeSpan.TicksPerMillisecond 
        let pulse = obj()
        let sw = System.Diagnostics.Stopwatch()
        do sw.Start()

        let callback() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
                s.Trigger(sw.Elapsed)
            )
                

        let timer = 
            match Environment.OSVersion  with
                | Windows -> Windows.start ms callback
                | _ -> Linux.start ms callback
        
        member x.Event = s.Publish

        member x.Wait() =
            lock pulse (fun () ->
                Monitor.Wait pulse |> ignore
            )  

        member x.Signal() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )  
            
        member x.Dispose() =
            timer.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


module Integrator = 

    let inline private dbl (one) = one + one

    let inline rungeKutta (f : ^t -> ^a -> ^da) (y0 : ^a) (h : ^t) : ^a =
        let twa : ^t = dbl LanguagePrimitives.GenericOne
        let half : ^t = LanguagePrimitives.GenericOne / twa
        let hHalf = h * half

        let k1 = h * f LanguagePrimitives.GenericZero y0
        let k2 = h * f hHalf (y0 + k1 * half)
        let k3 = h * f hHalf (y0 + k2 * half)
        let k4 = h * f h (y0 + k3)
        let sixth = LanguagePrimitives.GenericOne / (dbl twa + twa)
        y0 + (k1 + twa*k2 + twa*k3 + k4) * sixth

    let inline euler (f : ^t -> ^a -> ^da) (y0 : ^a) (h : ^t) : ^a=
        y0 + h * f LanguagePrimitives.GenericZero y0

    let rec integrate (maxDt : float) (f : 'm -> float -> 'm) (m0 : 'm) (dt : float) =
        if dt <= maxDt then
            f m0 dt
        else
            integrate maxDt f (f m0 maxDt) (dt - maxDt) 