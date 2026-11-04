namespace Aardvark.UI.Tests

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``MutableApp Disposal Tests`` =

    let private timeout = TimeSpan.FromSeconds 10.0

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private throwFromDisposer (error: exn) = raise error

    type private Resource(id: int, fail: bool, record: string -> unit, observe: unit -> bool) =
        let error = InvalidOperationException $"resource-{id}"
        let mutable calls = 0
        let mutable enabled = fail
        let mutable originalStack = ""
        let mutable ordered = true
        member _.Calls = Volatile.Read(&calls)
        member _.Error = error
        member _.OriginalStack = originalStack
        member _.Ordered = ordered
        member _.DisableFailure() = enabled <- false
        interface IDisposable with
            member _.Dispose() =
                Interlocked.Increment(&calls) |> ignore
                ordered <- ordered && observe()
                record $"resource:{id}"
                if enabled then
                    try throwFromDisposer error
                    with _ ->
                        originalStack <- error.StackTrace
                        reraise()

    type private Fixture(mask: int, registrations: int list, blockUpdate: bool) =
        let trace = ConcurrentQueue<string>()
        let entered = new ManualResetEventSlim(false)
        let proceed = new ManualResetEventSlim(false)
        let canceled = new ManualResetEventSlim(false)
        let mutable worker: Thread = null
        let mutable stops = 0
        let mutable stopOrdered = true
        let command =
            { new Command<int>() with
                member _.Start _ = trace.Enqueue "start"
                member _.Stop() =
                    stopOrdered <- stopOrdered && (isNull worker || not worker.IsAlive)
                    Interlocked.Increment(&stops) |> ignore
                    trace.Enqueue "stop" }
        let definition = {
            initial = 0
            unpersist = { create = AVal.init; update = fun state value -> state.Value <- value; trace.Enqueue "model" }
            threads = fun _ -> ThreadPool(HashMap.ofList ["owned", command])
            update = fun model message ->
                worker <- Thread.CurrentThread
                trace.Enqueue "update-enter"
                entered.Set()
                if blockUpdate && not (proceed.Wait timeout) then failwith "Update gate timed out"
                trace.Enqueue "update-exit"
                model + message
            view = fun _ -> div [] []
        }
        let app = App.start definition
        let token = app.CancellationToken
        let cancelSubscription = token.Register(fun () -> trace.Enqueue "cancel"; canceled.Set())
        let messageSubscription = app.Messages.Subscribe(fun _ -> trace.Enqueue "message")
        let resources = Array.init 3 (fun id ->
            new Resource(id, mask &&& (1 <<< id) <> 0, trace.Enqueue,
                fun () -> token.IsCancellationRequested && Volatile.Read(&stops) = 1 && (isNull worker || not worker.IsAlive)))
        let mutable disposal: Task<exn option> option = None
        do for id in registrations do app.Register resources.[id]

        member _.Resources = resources
        member _.Trace = trace.ToArray() |> Array.toList
        member _.StopOrdered = stopOrdered
        member _.Token = token
        member _.SourceDisposed =
            try app.CancellationToken |> ignore; false
            with :? ObjectDisposedException -> true
        member _.RegistrationsRemaining =
            // Observe queue consumption without adding a production-only inspection API.
            let field = app.GetType().GetFields(BindingFlags.Instance ||| BindingFlags.NonPublic)
                        |> Array.find (fun f -> f.FieldType = typeof<Stack<IDisposable>>)
            (field.GetValue(app) :?> Stack<IDisposable>).Count
        member _.BeginDispose() =
            match disposal with
            | Some work -> work
            | None ->
                let work = Task.Run(fun () -> try app.Dispose(); None with error -> Some error)
                disposal <- Some work
                work
        member this.FinishDispose() = this.BeginDispose().WaitAsync(timeout).GetAwaiter().GetResult()
        member _.StartUpdate() =
            app.Update(Guid.Empty, [1])
            Expect.isTrue (entered.Wait timeout) "The actual update thread entered the gated update"
        member _.WaitForCancellation() =
            Expect.isTrue (canceled.Wait timeout) "Dispose requested lifetime cancellation"
        member _.ReleaseUpdate() = proceed.Set()
        interface IDisposable with
            member this.Dispose() =
                // Drain owned work before releasing fixture synchronization objects, even on a red assertion.
                proceed.Set()
                this.FinishDispose() |> ignore
                try
                    if not this.SourceDisposed then
                        // Original failed cleanup retains registrations and CTS; finish it only after observations.
                        for resource in resources do resource.DisableFailure()
                        app.Dispose()
                finally
                    messageSubscription.Dispose()
                    cancelSubscription.Dispose()
                    entered.Dispose()
                    proceed.Dispose()
                    canceled.Dispose()

    let private checkFailure mask (fixture: Fixture) (error: exn option) =
        if mask = 0 then Expect.isNone error "Successful cleanup does not introduce a failure"
        else
            let first = [2; 1; 0] |> List.find (fun id -> mask &&& (1 <<< id) <> 0)
            let resource = fixture.Resources.[first]
            Expect.isTrue (error |> Option.exists (fun error -> obj.ReferenceEquals(error, resource.Error))) "Rethrow the first LIFO failure itself, not an aggregate or replacement"
            let actual = error.Value
            Expect.stringContains actual.StackTrace "throwFromDisposer" "Keep the originating disposer frame"
            Expect.stringStarts actual.StackTrace resource.OriginalStack "Preserve the original exception stack prefix"

    [<Tests>]
    let tests =
        testList "MutableApp.Disposal" [
            test "Every registered resource is attempted once in LIFO order despite failures" {
                for mask in [4; 2; 1; 3; 5; 6; 7; 0] do
                    use fixture = new Fixture(mask, [0; 1; 2], false)
                    let error = fixture.FinishDispose()
                    Expect.equal fixture.Trace ["start"; "cancel"; "stop"; "resource:2"; "resource:1"; "resource:0"] "Release every older registration after a throwing disposer"
                    Expect.equal (fixture.Resources |> Array.map _.Calls) [|1; 1; 1|] "Exactly one call per registration"
                    for resource in fixture.Resources do Expect.isTrue resource.Ordered "Cancellation, update-thread exit and command Stop precede resources"
                    checkFailure mask fixture error
            }

            test "Consumed registrations and cancellation source are released before failure escapes" {
                for mask in 0 .. 7 do
                    use fixture = new Fixture(mask, [0; 1; 2], false)
                    let error = fixture.FinishDispose()
                    let remaining, disposed = fixture.RegistrationsRemaining, fixture.SourceDisposed
                    Expect.isTrue disposed "Dispose the cancellation source before reporting a resource failure"
                    Expect.equal remaining 0 "Clear consumed registrations even when a disposer throws"
                    Expect.isTrue fixture.Token.IsCancellationRequested "Cached token still records lifetime cancellation"
                    checkFailure mask fixture error
            }

            test "Empty successful and repeated registrations keep their existing ownership" {
                for registrations in [[]; [0]; [0; 1; 2]; [0; 1; 0]] do
                    use fixture = new Fixture(0, registrations, false)
                    Expect.isNone (fixture.FinishDispose()) "Successful and empty cleanup complete normally"
                    let releases = registrations |> List.rev |> List.map (fun id -> $"resource:{id}")
                    Expect.equal fixture.Trace (["start"; "cancel"; "stop"] @ releases) "Each registration retains its LIFO position, including duplicate objects"
                    for id in 0 .. 2 do
                        Expect.equal fixture.Resources.[id].Calls (registrations |> List.filter ((=) id) |> List.length) "Do not deduplicate or repeat registrations"
                    Expect.equal fixture.RegistrationsRemaining 0 "Successful cleanup clears registrations"
                    Expect.isTrue fixture.SourceDisposed "Empty and successful cleanup dispose the cancellation source"
            }

            test "Cancellation update completion and command stopping retain shutdown order" {
                for mask in [0; 7] do
                    use fixture = new Fixture(mask, [0; 1; 2], true)
                    fixture.StartUpdate()
                    let work = fixture.BeginDispose()
                    fixture.WaitForCancellation()
                    Expect.isFalse work.IsCompleted "Dispose cannot complete while the update is gated"
                    fixture.ReleaseUpdate()
                    let error = fixture.FinishDispose()
                    Expect.equal fixture.Trace ["start"; "update-enter"; "cancel"; "update-exit"; "model"; "message"; "stop"; "resource:2"; "resource:1"; "resource:0"] "Join the real update thread before stopping commands and releasing registrations"
                    Expect.isTrue fixture.StopOrdered "The update worker is no longer alive at command Stop"
                    for resource in fixture.Resources do Expect.isTrue resource.Ordered "Resources observe canceled lifetime, stopped commands and a joined update thread"
                    Expect.isTrue fixture.SourceDisposed "Ordered cleanup still closes the lifetime source"
                    checkFailure mask fixture error
            }
        ]
