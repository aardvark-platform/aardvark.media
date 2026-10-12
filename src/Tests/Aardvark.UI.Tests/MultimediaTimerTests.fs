namespace Aardvark.UI.Tests

open System
open System.Threading
open System.Threading.Tasks
open Aardvark.UI.Primitives
open Expecto

module ``MultimediaTimer Tests`` =

    let private timeout = TimeSpan.FromSeconds 5.0

    let private nonWindows() =
        if OperatingSystem.IsWindows() then skiptest "Non-Windows worker lifecycle"

    let private startDispose (timer : IDisposable) =
        let result = TaskCompletionSource<Choice<unit, exn>>(TaskCreationOptions.RunContinuationsAsynchronously)
        let thread =
            Thread(ThreadStart(fun () ->
                let outcome = try timer.Dispose(); Choice1Of2 () with error -> Choice2Of2 error
                result.SetResult outcome
            ), IsBackground = true)
        thread.Start()
        thread, result.Task

    let private awaitDispose (result : Task<Choice<unit, exn>>) =
        Expect.isTrue (result.Wait timeout) "Disposal completes within the bound"
        match result.Result with
        | Choice1Of2 () -> ()
        | Choice2Of2 error -> failtestf "Disposal threw %s: %s" (error.GetType().Name) error.Message

    let private dispose timer = startDispose timer |> snd |> awaitDispose

    let private cleanup timer =
        let _, result = startDispose timer
        if result.Wait timeout then
            match result.Result with
            | Choice1Of2 () -> ()
            | Choice2Of2 (:? PlatformNotSupportedException) -> () // Original Abort fails; primary assertions still fail.
            | Choice2Of2 error -> raise error

    let private withTimer interval callback test =
        let timer = new MultimediaTimer.Timer(interval)
        let guard = obj()
        let mutable active = true
        use subscription = timer.Event.Subscribe(fun elapsed ->
            lock guard (fun () -> if active then callback timer elapsed)
        )
        try test timer
        finally
            // Detach even original workers before callback synchronization resources leave scope.
            lock guard (fun () -> active <- false)
            cleanup timer

    let private waitOnce wait signal manual =
        let result = TaskCompletionSource<Choice<unit, exn>>(TaskCreationOptions.RunContinuationsAsynchronously)
        let waiter =
            Thread(ThreadStart(fun () ->
                let outcome = try wait(); Choice1Of2 () with error -> Choice2Of2 error
                result.SetResult outcome
            ), IsBackground = true)
        waiter.Start()
        try
            Expect.isTrue (SpinWait.SpinUntil((fun () ->
                result.Task.IsCompleted || waiter.ThreadState.HasFlag ThreadState.WaitSleepJoin
            ), timeout)) "Waiter enters the wait"
            if manual then
                Expect.isFalse result.Task.IsCompleted "Wait has not consumed an earlier signal"
                signal()
            Expect.isTrue (result.Task.Wait timeout) "Wait is released"
            match result.Task.Result with
            | Choice1Of2 () -> ()
            | Choice2Of2 error -> raise error
        finally
            // Pulse until the waiter drains, including if the assertion ran before Monitor.Wait.
            SpinWait.SpinUntil((fun () -> signal(); not waiter.IsAlive), timeout) |> ignore
            waiter.Join timeout |> ignore

    [<Tests>]
    let tests =
        testList "MultimediaTimer Tests" [
            test "Timer delivers ordered callbacks and preserves Wait and Signal" {
                nonWindows()
                use delivered = new ManualResetEventSlim(false)
                let observations = ResizeArray<string * TimeSpan>()
                let guard = obj()
                let mutable active = true
                let mutable worker = Unchecked.defaultof<Thread>
                withTimer 5 (fun _ elapsed ->
                    lock guard (fun () ->
                        if active then
                            worker <- Thread.CurrentThread
                            observations.Add("first", elapsed)
                    )
                ) (fun timer ->
                    use subscription = timer.Event.Subscribe(fun elapsed ->
                        lock guard (fun () ->
                            if active then
                                observations.Add("second", elapsed)
                                if observations.Count >= 8 then delivered.Set()
                        )
                    )
                    try
                        Expect.isTrue (delivered.Wait timeout) "Ordinary callbacks arrive"
                        waitOnce timer.Wait timer.Signal false
                        dispose timer
                        Expect.isFalse worker.IsAlive "External disposal joins the callback worker"
                        let recorded = lock guard (fun () -> observations.ToArray())
                        // Subscription can occur between ticks; inspect complete subscriber pairs.
                        let pairs = recorded |> Array.skipWhile (fun (name, _) -> name <> "second") |> Array.skip 1
                        for pair in Array.chunkBySize 2 pairs do
                            Expect.equal (pair |> Array.map fst) [|"first"; "second"|] "Subscriber order"
                            Expect.equal (snd pair.[0]) (snd pair.[1]) "Same elapsed value reaches both subscribers"
                        let elapsed = recorded |> Array.map snd
                        Expect.isTrue (elapsed.[0] > TimeSpan.Zero) "Elapsed time is positive"
                        Expect.isTrue (elapsed |> Array.pairwise |> Array.forall (fun (a, b) -> b >= a)) "Elapsed time is monotonic"
                        waitOnce timer.Wait timer.Signal true
                        Expect.equal (lock guard (fun () -> observations.Count)) recorded.Length "Signal does not raise Event"
                    finally
                        lock guard (fun () -> active <- false)
                )
            }

            test "Repeated disposal stops one timer without affecting another" {
                nonWindows()
                let mutable stoppedCount = 0
                let mutable liveCount = 0
                let mutable worker = Unchecked.defaultof<Thread>
                withTimer 5 (fun _ _ ->
                    worker <- Thread.CurrentThread
                    Interlocked.Increment(&stoppedCount) |> ignore
                ) (fun stopped ->
                    withTimer 5 (fun _ _ -> Interlocked.Increment(&liveCount) |> ignore) (fun live ->
                        Expect.isTrue (SpinWait.SpinUntil((fun () -> Volatile.Read(&stoppedCount) >= 2 && Volatile.Read(&liveCount) >= 2), timeout)) "Both timers deliver"
                        let disposers = [for _ in 1 .. 4 -> startDispose stopped |> snd]
                        for result in disposers do awaitDispose result
                        for _ in 1 .. 3 do dispose stopped
                        Expect.isFalse worker.IsAlive "Retired worker has exited"
                        let stoppedAt = Volatile.Read(&stoppedCount)
                        let liveTarget = Volatile.Read(&liveCount) + 12
                        Expect.isTrue (SpinWait.SpinUntil((fun () -> Volatile.Read(&liveCount) >= liveTarget), timeout)) "Sibling continues delivering"
                        Expect.equal (Volatile.Read(&stoppedCount)) stoppedAt "Retired callbacks have ceased"
                        dispose live
                    )
                )
            }

            test "External disposal joins an in-flight callback" {
                nonWindows()
                use entered = new ManualResetEventSlim(false)
                use release = new ManualResetEventSlim(false)
                use exited = new ManualResetEventSlim(false)
                let mutable worker = Unchecked.defaultof<Thread>
                withTimer 5 (fun _ _ ->
                    worker <- Thread.CurrentThread
                    entered.Set()
                    release.Wait timeout |> ignore
                    exited.Set()
                ) (fun timer ->
                    try
                        Expect.isTrue (entered.Wait timeout) "Callback is held in flight"
                        let disposer, result = startDispose timer
                        try
                            Expect.isTrue (SpinWait.SpinUntil((fun () ->
                                result.IsCompleted || disposer.ThreadState.HasFlag ThreadState.WaitSleepJoin
                            ), timeout)) "Disposer reaches join or completes"
                            if result.IsCompleted then awaitDispose result
                            Expect.isFalse result.IsCompleted "Disposal cannot complete before the callback exits"
                        finally
                            release.Set()
                        awaitDispose result
                        Expect.isTrue (exited.Wait timeout) "Callback exits before disposal completes"
                        Expect.isFalse worker.IsAlive "Worker has stopped using synchronization resources"
                        dispose timer
                    finally
                        release.Set()
                )
            }

            test "A Timer Event callback can dispose its own worker" {
                nonWindows()
                let outcome = TaskCompletionSource<Choice<unit, exn>>(TaskCreationOptions.RunContinuationsAsynchronously)
                let mutable worker = Unchecked.defaultof<Thread>
                let timer = new MultimediaTimer.Timer(5)
                use subscription = timer.Event.Subscribe(fun _ ->
                    if not outcome.Task.IsCompleted then
                        worker <- Thread.CurrentThread
                        let result = try timer.Dispose(); Choice1Of2 () with error -> Choice2Of2 error
                        outcome.SetResult result
                )
                try
                    awaitDispose outcome.Task
                    Expect.isTrue (worker.Join timeout) "Callback-origin shutdown does not self-join or keep ticking"
                    Expect.isFalse worker.IsAlive "Self-disposed worker exits after its callback"
                    dispose timer
                    dispose timer
                finally
                    cleanup timer
            }

            test "Timer and Trigger disposal interrupt long waits and remain repeatable" {
                nonWindows()
                withTimer 60000 (fun _ _ -> ()) (fun timer ->
                    waitOnce timer.Wait timer.Signal true
                    dispose timer
                    dispose timer
                    waitOnce timer.Wait timer.Signal true
                )
                let trigger = new MultimediaTimer.Trigger(60000)
                try
                    waitOnce trigger.Wait trigger.Signal true
                    dispose trigger
                    dispose trigger
                    waitOnce trigger.Wait trigger.Signal true
                finally
                    cleanup trigger
            }

            test "Trigger Wait is released by ordinary timer pulses" {
                nonWindows()
                let trigger = new MultimediaTimer.Trigger(5)
                try
                    waitOnce trigger.Wait trigger.Signal false
                    dispose trigger
                    dispose trigger
                finally
                    cleanup trigger
            }
        ]
