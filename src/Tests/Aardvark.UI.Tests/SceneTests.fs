namespace Aardvark.UI.Tests

open System
open Aardvark.Rendering
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``Scene Tests`` =

    // Count actual Dispose calls, rather than hiding duplicates behind task idempotence.
    type private CountedTask() =
        inherit AdaptiveObject()
        let id = RenderTaskId.New()
        let mutable disposals, uses = 0, 0
        let mutable name = "No-op"
        member _.Disposals = disposals
        member _.Uses = uses
        interface IRenderTask with
            member _.Id = id
            member _.FramebufferSignature = None
            member _.Runtime = None
            member _.FrameId = 0UL
            member _.Name with get() = name and set value = name <- value
            member _.Update(_, _) = ()
            member _.Run(_, _, _) = ()
            member _.Use action = uses <- uses + 1; action()
            member _.Dispose() = disposals <- disposals + 1

    type private Fixture(failure: int -> exn option) =
        let contexts = ResizeArray<RenderClientValues>()
        let tasks = ResizeArray<CountedTask>()
        let handles = ResizeArray<IRenderTask>()
        let scene =
            Scene.custom (fun values ->
                contexts.Add values
                match failure contexts.Count with
                | Some error -> raise error
                | None ->
                    let task = new CountedTask()
                    tasks.Add task
                    task :> IRenderTask)
        // Managed no-op compilation never needs a framebuffer or runtime.
        let concrete = new ConcreteScene(scene, null)
        member _.Concrete = concrete
        member _.Attempts = contexts.Count
        member _.Tasks = tasks
        member _.Acquire() =
            let handle = concrete.CreateNewRenderTask()
            handles.Add handle
            handle
        member _.CheckContexts() =
            for values in contexts do
                Expect.isTrue (obj.ReferenceEquals(values, concrete.Values)) "Retries retain the same client values"
        interface IDisposable with
            member _.Dispose() =
                for handle in handles do handle.Dispose()
                concrete.Dispose()
                // Fallback cleanup must not supply the lifecycle evidence asserted by tests.
                for task in tasks do
                    if task.Disposals = 0 then (task :> IRenderTask).Dispose()

    let private withFixture failure action =
        let fixture = new Fixture(failure)
        // Serialize the real timer callback without changing global delays or sleeping.
        // Cleanup stays inside this monitor even when an assertion fails.
        lock fixture.Concrete (fun () ->
            try action fixture
            finally (fixture :> IDisposable).Dispose())

    let private expectFailure (expected: exn) (fixture: Fixture) =
        let actual =
            try fixture.Acquire() |> ignore; None
            with error -> Some error
        Expect.isTrue (actual |> Option.exists (fun error -> obj.ReferenceEquals(error, expected)))
            "Compilation preserves the original exception object"

    let private finish (fixture: Fixture) (handle: IRenderTask) (task: CountedTask) =
        handle.Dispose()
        Expect.equal task.Disposals 0 "Final release leaves the cached task for delayed destruction"
        fixture.Concrete.Dispose()
        Expect.equal task.Disposals 1 "Explicit destruction releases the compiled task exactly once"
        fixture.Concrete.Dispose()
        handle.Dispose()
        Expect.equal task.Disposals 1 "Repeated scene/handle disposal cannot release it again"

    [<Tests>]
    let tests =
        testList "Scene Tests" [
            test "A failed initial compilation can be retried and shared" {
                let failure = InvalidOperationException "initial compilation"
                withFixture (fun attempt -> if attempt = 1 then Some failure else None) (fun fixture ->
                    expectFailure failure fixture
                    Expect.equal fixture.Attempts 1 "First acquisition attempted compilation"
                    Expect.equal fixture.Tasks.Count 0 "Failed compilation owns no task"
                    let first = fixture.Acquire()
                    let second = fixture.Acquire()
                    Expect.equal fixture.Attempts 2 "Retry compiles once, then shares the recovered task"
                    Expect.equal fixture.Tasks.Count 1 "Only successful compilation owns a task"
                    let task = fixture.Tasks[0]
                    first.Use ignore
                    second.Use ignore
                    Expect.equal task.Uses 2 "Both wrappers forward to the recovered task"
                    first.Dispose()
                    fixture.Concrete.Dispose()
                    Expect.equal task.Disposals 0 "The second handle still protects the task"
                    fixture.CheckContexts()
                    finish fixture second task)
            }

            test "Repeated compilation failures preserve identity and remain retryable" {
                let failures : exn[] = [| InvalidOperationException "first"; ArgumentException "second"; OperationCanceledException "third" |]
                withFixture (fun attempt -> if attempt <= failures.Length then Some failures[attempt - 1] else None) (fun fixture ->
                    for index in 0 .. failures.Length - 1 do
                        expectFailure failures[index] fixture
                        Expect.equal fixture.Attempts (index + 1) "Each acquisition actually retries compilation"
                        Expect.equal fixture.Tasks.Count 0 "Failures own no compiled task"
                    let handle = fixture.Acquire()
                    Expect.equal fixture.Attempts 4 "Acquisition succeeds after consecutive failures"
                    fixture.CheckContexts()
                    finish fixture handle fixture.Tasks[0])
            }

            test "A failed recompile after normal destruction can recover" {
                let failure = InvalidOperationException "recompilation"
                withFixture (fun attempt -> if attempt = 2 then Some failure else None) (fun fixture ->
                    let first = fixture.Acquire()
                    let retired = fixture.Tasks[0]
                    finish fixture first retired
                    expectFailure failure fixture
                    Expect.equal fixture.Attempts 2 "Recompile was attempted after retirement"
                    let recovered = fixture.Acquire()
                    Expect.equal fixture.Attempts 3 "A failed recompile does not poison the next acquisition"
                    Expect.equal fixture.Tasks.Count 2 "Recovery creates a fresh task"
                    let task = fixture.Tasks[1]
                    recovered.Use ignore
                    Expect.equal task.Uses 1 "The recovered task is used"
                    Expect.equal retired.Disposals 1 "Retired task is never released twice"
                    fixture.CheckContexts()
                    finish fixture recovered task)
            }

            test "Active handles share compilation and protect exactly-once destruction" {
                for count in [1; 2; 4] do
                    for reverse in [false; true] do
                        withFixture (fun _ -> None) (fun fixture ->
                            let handles = List.init count (fun _ -> fixture.Acquire())
                            let ordered = if reverse then List.rev handles else handles
                            let task = fixture.Tasks[0]
                            Expect.equal fixture.Attempts 1 "All active handles share one compilation"
                            Expect.equal (handles |> List.map _.Id |> Set.ofList |> Set.count) count "Each handle is distinct"
                            for handle in ordered do
                                fixture.Concrete.Dispose()
                                Expect.equal task.Disposals 0 "Active references prevent destruction"
                                Expect.equal handle.FramebufferSignature None "Framebuffer signature is forwarded"
                                Expect.equal handle.Runtime None "Runtime is forwarded"
                                handle.Use ignore
                                handle.Dispose()
                                handle.Dispose()
                                Expect.equal task.Disposals 0 "Release does not synchronously destroy the shared task"
                            Expect.equal task.Uses count "Every handle uses the shared task"
                            fixture.Concrete.Dispose()
                            Expect.equal task.Disposals 1 "The final reference permits destruction"
                            fixture.Concrete.Dispose()
                            Expect.equal task.Disposals 1 "Destruction remains exactly once")
            }

            test "Zero-reference cached acquisition reuses the task before retirement" {
                withFixture (fun _ -> None) (fun fixture ->
                    let first = fixture.Acquire()
                    let cached = fixture.Tasks[0]
                    first.Dispose()
                    Expect.equal cached.Disposals 0 "Released task remains cached until destruction"
                    let second = fixture.Acquire()
                    Expect.equal fixture.Attempts 1 "Reacquisition reuses the cached task"
                    second.Use ignore
                    fixture.Concrete.Dispose()
                    Expect.equal cached.Disposals 0 "Reacquired reference prevents destruction"
                    finish fixture second cached
                    let next = fixture.Acquire()
                    Expect.equal fixture.Attempts 2 "Acquisition after destruction recompiles"
                    let fresh = fixture.Tasks[1]
                    next.Use ignore
                    Expect.equal fresh.Uses 1 "A fresh compiled task serves the next generation"
                    Expect.equal cached.Disposals 1 "Old generation stays retired"
                    fixture.CheckContexts()
                    finish fixture next fresh)
            }
        ]
