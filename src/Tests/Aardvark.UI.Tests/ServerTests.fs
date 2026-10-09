namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Expecto

module ``Server Tests`` =

    let private timeout = TimeSpan.FromSeconds 5.0
    let private gate() = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    type private Outcome = Return | Throw | Fault

    type private RecordingHost(startup: Outcome, shutdown: Outcome) =
        let startEntered, startRelease = gate(), gate()
        let stopEntered, stopRelease = gate(), gate()
        let started = new CancellationTokenSource()
        let stopping = new CancellationTokenSource()
        let stopped = new CancellationTokenSource()
        let startTokens, stopTokens = ConcurrentQueue<CancellationToken>(), ConcurrentQueue<CancellationToken>()
        let startError = InvalidOperationException("startup failed")
        let stopError = InvalidOperationException("shutdown failed")
        let mutable startupFailure : exn option = None
        let mutable disposeCount = 0
        let lifetime =
            { new IHostApplicationLifetime with
                member _.ApplicationStarted = started.Token
                member _.ApplicationStopping = stopping.Token
                member _.ApplicationStopped = stopped.Token
                member _.StopApplication() = stopping.Cancel() }
        let services =
            { new IServiceProvider with
                member _.GetService serviceType =
                    if serviceType = typeof<IHostApplicationLifetime> then lifetime :> obj
                    else null }

        member _.StartEntered = startEntered.Task :> Task
        member _.StopEntered = stopEntered.Task :> Task
        member _.StartTokens = startTokens.ToArray() |> Array.toList
        member _.StopTokens = stopTokens.ToArray() |> Array.toList
        member _.Started = started.IsCancellationRequested
        member _.StartupFailure = Option.get startupFailure
        member _.StopError = stopError :> exn
        member _.DisposeCount = Volatile.Read(&disposeCount)
        member _.ReleaseStartup() = startRelease.TrySetResult(()) |> ignore
        member _.ReleaseShutdown() = stopRelease.TrySetResult(()) |> ignore
        member _.StopApplication() = lifetime.StopApplication()
        member _.Cleanup() =
            started.Dispose()
            stopping.Dispose()
            stopped.Dispose()

        interface IHost with
            member _.Services = services
            member _.StartAsync token =
                startTokens.Enqueue token
                startEntered.TrySetResult(()) |> ignore
                if startup = Throw then
                    startupFailure <- Some startError
                    raise startError
                else
                    task {
                        try
                            do! startRelease.Task.WaitAsync(token)
                            if startup = Fault then raise startError
                            started.Cancel()
                        with error ->
                            startupFailure <- Some error
                            return raise error
                    }
            member _.StopAsync token =
                stopTokens.Enqueue token
                stopEntered.TrySetResult(()) |> ignore
                if shutdown = Throw then raise stopError
                else
                    task {
                        do! stopRelease.Task
                        if shutdown = Fault then raise stopError
                        stopped.Cancel()
                    }
            member _.Dispose() = Interlocked.Increment(&disposeCount) |> ignore

    // Keep invocation separate from the returned lifecycle task: startup must block
    // the caller, and startup failures must throw rather than return a faulted task.
    let private withHost startup shutdown preCanceled action =
        task {
            let host = new RecordingHost(startup, shutdown)
            use cancellation = new CancellationTokenSource()
            if preCanceled then cancellation.Cancel()
            let invocation =
                Task.Factory.StartNew(
                    Func<Choice<Task, exn>>(fun () ->
                        try Choice1Of2 (Aardvark.UI.Giraffe.Server.startHost cancellation.Token host)
                        with error -> Choice2Of2 error),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default
                )
            let mutable failure = None
            try do! action host cancellation invocation
            with error -> failure <- Some (ExceptionDispatchInfo.Capture error)

            // Release both stages and drain all work before test-owned fallback cleanup.
            host.ReleaseStartup()
            cancellation.Cancel()
            host.StopApplication()
            host.ReleaseShutdown()
            try
                try
                    let! outcome = invocation.WaitAsync timeout
                    match outcome with
                    | Choice1Of2 work ->
                        try do! work.WaitAsync timeout
                        with _ when work.IsCompleted -> () // Expected faults are asserted by the action.
                    | Choice2Of2 _ -> ()
                with error ->
                    if failure.IsNone then failure <- Some (ExceptionDispatchInfo.Capture error)
            finally
                if host.DisposeCount = 0 then (host :> IHost).Dispose()
                host.Cleanup()
            failure |> Option.iter (fun error -> error.Throw())
        }

    let private running (invocation: Task<Choice<Task, exn>>) =
        task {
            let! outcome = invocation.WaitAsync timeout
            match outcome with
            | Choice1Of2 work -> return work
            | Choice2Of2 error -> return raise error
        }

    let private expectSynchronousFailure (host: RecordingHost) token (invocation: Task<Choice<Task, exn>>) =
        task {
            let! outcome = invocation.WaitAsync timeout
            match outcome with
            | Choice1Of2 _ -> failtest "Startup failure must throw synchronously, not return a task"
            | Choice2Of2 error ->
                Expect.isTrue (Object.ReferenceEquals(error, host.StartupFailure)) "Cleanup must preserve the exact startup failure"
                match error with
                | :? OperationCanceledException as canceled -> Expect.equal canceled.CancellationToken token "Changed startup cancellation token"
                | _ -> ()
            Expect.equal host.StartTokens [token] "Startup must receive the original token exactly once"
            Expect.isEmpty host.StopTokens "Failed startup must not enter shutdown waiting"
            Expect.equal host.DisposeCount 1 "Startup failure must dispose the host exactly once before throwing"
        }

    let private lifetime cancel =
        withHost Return Return false (fun host cancellation invocation ->
            task {
                do! host.StartEntered.WaitAsync timeout
                Expect.isFalse invocation.IsCompleted "Server.start must wait synchronously for startup readiness"
                Expect.equal host.DisposeCount 0 "Pending startup must not dispose the host"
                Expect.isEmpty host.StopTokens "Shutdown must not begin during startup"
                host.ReleaseStartup()
                let! work = running invocation
                Expect.isTrue host.Started "Startup must complete before Server.start returns"
                Expect.isFalse work.IsCompleted "Returned task must cover the full running server lifetime"
                Expect.equal host.StartTokens [cancellation.Token] "Changed startup token or invocation count"
                Expect.equal host.DisposeCount 0 "Running server must keep its host alive"
                Expect.isEmpty host.StopTokens "Running server must not stop before shutdown is requested"

                if cancel then cancellation.Cancel() else host.StopApplication()
                do! host.StopEntered.WaitAsync timeout
                Expect.isFalse work.IsCompleted "Returned task must await shutdown completion"
                Expect.equal host.DisposeCount 0 "Pending shutdown must keep its host alive"
                Expect.equal host.StopTokens [CancellationToken.None] "Preserve WaitForShutdownAsync's graceful StopAsync token"
                host.ReleaseShutdown()
                do! work.WaitAsync timeout
                Expect.isTrue work.IsCompletedSuccessfully "Lifetime-token cancellation must remain graceful shutdown"
                Expect.equal host.DisposeCount 1 "Host must be disposed exactly once before lifecycle completion"
            })

    let private startupFailure outcome =
        withHost outcome Return false (fun host cancellation invocation ->
            task {
                do! host.StartEntered.WaitAsync timeout
                if outcome = Fault then
                    Expect.isFalse invocation.IsCompleted "Faulted asynchronous startup must still block its caller until completion"
                    Expect.equal host.DisposeCount 0 "Pending startup must not dispose the host"
                    host.ReleaseStartup()
                do! expectSynchronousFailure host cancellation.Token invocation
            })

    let private startupCancellation preCanceled =
        withHost Return Return preCanceled (fun host cancellation invocation ->
            task {
                do! host.StartEntered.WaitAsync timeout
                if not preCanceled then
                    Expect.isFalse invocation.IsCompleted "Pending startup must block its caller"
                    Expect.equal host.DisposeCount 0 "Pending startup must keep its host alive"
                    cancellation.Cancel()
                do! expectSynchronousFailure host cancellation.Token invocation
                Expect.isTrue (host.StartupFailure :? OperationCanceledException) "Expected startup cancellation, not another failure"
            })

    let private shutdownFailure outcome =
        withHost Return outcome false (fun host _ invocation ->
            task {
                do! host.StartEntered.WaitAsync timeout
                host.ReleaseStartup()
                let! work = running invocation
                Expect.equal host.DisposeCount 0 "Running server must keep its host alive"
                host.StopApplication()
                do! host.StopEntered.WaitAsync timeout
                if outcome = Fault then
                    Expect.isFalse work.IsCompleted "Lifetime task must wait for a pending shutdown failure"
                    Expect.equal host.DisposeCount 0 "Pending shutdown must not dispose the host"
                    host.ReleaseShutdown()
                let! error =
                    task {
                        try
                            do! work.WaitAsync timeout
                            return failtest "Expected shutdown failure"
                        with error -> return error
                    }
                Expect.isTrue (Object.ReferenceEquals(error, host.StopError)) "Cleanup must preserve the exact shutdown failure"
                Expect.isTrue work.IsFaulted "Shutdown failure must fault the lifecycle task"
                Expect.equal host.StopTokens [CancellationToken.None] "Changed shutdown invocation or token"
                Expect.equal host.DisposeCount 1 "Shutdown failure must dispose the host exactly once before lifecycle completion"
            })

    [<Tests>]
    let tests =
        testList "Server Tests.Giraffe" [
            testCaseAsync "Startup readiness and full shutdown precede host disposal" (async {
                for cancel in [false; true] do
                    do! lifetime cancel |> Async.AwaitTask
            })
            testCaseAsync "Startup failures dispose before throwing synchronously" (async {
                for outcome in [Throw; Fault] do
                    do! startupFailure outcome |> Async.AwaitTask
            })
            testCaseAsync "Startup cancellation disposes before throwing synchronously" (async {
                for preCanceled in [false; true] do
                    do! startupCancellation preCanceled |> Async.AwaitTask
            })
            testCaseAsync "Shutdown failures dispose before lifecycle completion" (async {
                for outcome in [Throw; Fault] do
                    do! shutdownFailure outcome |> Async.AwaitTask
            })
        ]
