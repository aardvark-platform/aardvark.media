namespace Aardvark.UI.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``Scene Tests`` =

    // Count actual Dispose calls, rather than hiding duplicates behind task idempotence.
    type private CountedTask(?onDispose: unit -> unit, ?getRuntime: unit -> IRuntime option) =
        inherit AdaptiveObject()
        let id = RenderTaskId.New()
        let mutable disposals, uses = 0, 0
        let mutable name = "No-op"
        member _.Disposals = disposals
        member _.Uses = uses
        interface IRenderTask with
            member _.Id = id
            member _.FramebufferSignature = None
            member _.Runtime = match getRuntime with Some get -> get() | None -> None
            member _.FrameId = 0UL
            member _.Name with get() = name and set value = name <- value
            member _.Update(_, _) = ()
            member _.Run(_, _, _) = ()
            member _.Use action = uses <- uses + 1; action()
            member _.Dispose() =
                disposals <- disposals + 1
                onDispose |> Option.iter (fun action -> action())

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

    let private constructors : (Scene[] -> Scene) list =
        [ Scene.ofArray
          Array.toList >> Scene.ofList
          fun scenes -> Scene.ofSeq (Seq.ofArray scenes) ]

    let private withChildren action =
        let tasks = ResizeArray<CountedTask>()
        try action tasks
        finally
            // Do not let fallback cleanup establish the assertions or hide the primary error.
            for task in tasks do
                if task.Disposals = 0 then
                    try (task :> IRenderTask).Dispose()
                    with _ -> ()

    let private expectCompileFailure (expected: exn) (scene: Scene) values =
        let actual =
            try
                use task = scene.Compile values
                None
            with error -> Some error
        Expect.isTrue (actual |> Option.exists (fun error -> obj.ReferenceEquals(error, expected)))
            "The exact compilation exception survives cleanup"

    let private withReports failReporting action =
        let messages = ResizeArray<string>()
        let reporter =
            { new IReporter with
                member _.Line(_, _, _, _, left, _, right) =
                    messages.Add(left + right)
                    if failReporting then failwith "reporting unavailable"
                member _.Text(_, _, _, _) = ()
                member _.Wrap(_, _, _, _) = ()
                member _.Begin(_, _, _, _, _) = ()
                member _.End(_, _, _, _, _) = ()
                member _.Progress(_, _, _, _, _, _) = ()
                member _.Values(_, _, _, _, _, _) = () }
        Report.AddReporter reporter
        try action messages
        finally Report.RemoveReporter reporter

    [<Tests>]
    let tests =
        testList "Scene Tests" [
            test "Composite failures release only the acquired prefix on every attempt" {
                for compose in constructors do
                    for failingIndex in [0; 1; 3; 4] do
                        withChildren (fun tasks ->
                            let values = RenderClientValues(null)
                            let compiled, disposed = ResizeArray<int>(), ResizeArray<int>()
                            let failure = InvalidOperationException "child compilation"
                            let mutable failCompilation = true
                            let scene =
                                Array.init 5 (fun index ->
                                    Scene.custom (fun received ->
                                        Expect.isTrue (obj.ReferenceEquals(received, values)) "Every child receives the caller's values"
                                        compiled.Add index
                                        if failCompilation && index = failingIndex then raise failure
                                        let task = new CountedTask(onDispose = fun () -> disposed.Add index)
                                        tasks.Add task
                                        task :> IRenderTask))
                                |> compose
                            for attempt in 1 .. 3 do
                                compiled.Clear()
                                disposed.Clear()
                                expectCompileFailure failure scene values
                                Expect.sequenceEqual compiled [0 .. failingIndex] "Compilation stops at the failing child"
                                Expect.equal tasks.Count (attempt * failingIndex) "Every retry acquires fresh prefix tasks"
                                Expect.sequenceEqual disposed [failingIndex - 1 .. -1 .. 0] "Rollback releases the completed prefix in reverse"
                                Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Each acquired task is disposed exactly once"
                            failCompilation <- false
                            compiled.Clear()
                            let firstLive = tasks.Count
                            use recovered = scene.Compile values
                            Expect.sequenceEqual compiled [0 .. 4] "The same composite can compile successfully after repeated failures"
                            Expect.equal tasks.Count (firstLive + 5) "Recovery acquires a fresh complete set of tasks"
                            Expect.isTrue (tasks |> Seq.skip firstLive |> Seq.forall (fun task -> task.Disposals = 0)) "Recovered children remain alive"
                            recovered.Dispose()
                            recovered.Dispose()
                            Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Rollback and recovered ownership never overlap")
            }

            test "Nested failures release inner and outer ownership without duplication" {
                for compose in constructors do
                    // Partially compiled inner, fully compiled inner, and deeper mixed ownership.
                    for shape in [0; 1; 2] do
                        withChildren (fun tasks ->
                            let values = RenderClientValues(null)
                            let compiled, disposed = ResizeArray<int>(), ResizeArray<int>()
                            let failure = InvalidOperationException "nested child compilation"
                            let child index =
                                Scene.custom (fun received ->
                                    Expect.isTrue (obj.ReferenceEquals(received, values)) "Nested children retain the same client values"
                                    compiled.Add index
                                    if index = 3 then raise failure
                                    let task = new CountedTask(onDispose = fun () -> disposed.Add index)
                                    tasks.Add task
                                    task :> IRenderTask)
                            let scene, cleanupOrder =
                                match shape with
                                | 0 -> compose [| child 0; compose [| child 1; child 2; child 3; child 4 |]; child 5 |], [2; 1; 0]
                                | 1 -> compose [| child 0; compose [| child 1; child 2 |]; child 3; child 4 |], [1; 2; 0]
                                | _ -> compose [| child 0; compose [| compose [| child 1; child 2 |]; compose [| child 3; child 4 |] |]; child 5 |], [1; 2; 0]
                            for attempt in 1 .. 2 do
                                compiled.Clear()
                                disposed.Clear()
                                expectCompileFailure failure scene values
                                Expect.sequenceEqual compiled [0; 1; 2; 3] "No later inner or outer child compiles"
                                Expect.sequenceEqual disposed cleanupOrder "Completed composites retain their normal disposal order"
                                Expect.equal tasks.Count (attempt * 3) "Retries do not retain partially compiled tasks"
                                Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Each leaf has exactly one owner during rollback")
            }

            test "Cleanup errors are reported without masking failure or skipping children" {
                for compose in constructors do
                    for failReporting in [false; true] do
                        withChildren (fun tasks ->
                            let disposed = ResizeArray<int>()
                            let failure = InvalidOperationException "primary compilation failure"
                            let scene =
                                Array.init 5 (fun index ->
                                    Scene.custom (fun _ ->
                                        if index = 4 then raise failure
                                        let task =
                                            new CountedTask(onDispose = fun () ->
                                                disposed.Add index
                                                if index = 1 || index = 3 then
                                                    raise <| InvalidOperationException($"cleanup-{index}"))
                                        tasks.Add task
                                        task :> IRenderTask))
                                |> compose
                            withReports failReporting (fun messages ->
                                expectCompileFailure failure scene (RenderClientValues(null))
                                Expect.sequenceEqual disposed [3; 2; 1; 0] "Both throwing disposals still permit all cleanup"
                                Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Throwing cleanup is not retried"
                                if failReporting then
                                    Expect.equal messages.Count 2 "Reporting is attempted for both errors even if the reporter throws"
                                else
                                    for index in [1; 3] do
                                        Expect.isTrue (messages |> Seq.exists (fun message -> message.Contains($"cleanup-{index}")))
                                            "Each secondary cleanup error is reported"))
            }

            test "Composite construction failure releases every compiled child" {
                for compose in constructors do
                    withChildren (fun tasks ->
                        let compiled, disposed = ResizeArray<int>(), ResizeArray<int>()
                        let failure = InvalidOperationException "runtime discovery"
                        let scene =
                            Array.init 3 (fun index ->
                                Scene.custom (fun _ ->
                                    compiled.Add index
                                    let task =
                                        new CountedTask(
                                            onDispose = (fun () -> disposed.Add index),
                                            getRuntime = (fun () -> if index = 1 then raise failure else None))
                                    tasks.Add task
                                    task :> IRenderTask))
                            |> compose
                        expectCompileFailure failure scene (RenderClientValues(null))
                        Expect.sequenceEqual compiled [0; 1; 2] "All children compile before native composite runtime discovery fails"
                        Expect.sequenceEqual disposed [2; 1; 0] "Composite creation failure rolls back the entire array"
                        Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Every completed child is disposed exactly once")
            }

            test "Successful composites retain ordered compilation and task ownership" {
                for compose in constructors do
                    for count in [0; 1; 4] do
                        for nested in [false; true] do
                            withChildren (fun tasks ->
                                let values = RenderClientValues(null)
                                let compiled = ResizeArray<int>()
                                let children =
                                    Array.init count (fun index ->
                                        Scene.custom (fun received ->
                                            Expect.isTrue (obj.ReferenceEquals(received, values)) "Successful children receive the original values"
                                            compiled.Add index
                                            let task = new CountedTask()
                                            tasks.Add task
                                            task :> IRenderTask))
                                let scene =
                                    if nested then compose [| compose children; compose [||] |]
                                    else compose children
                                use composite = scene.Compile values
                                Expect.sequenceEqual compiled [0 .. count - 1] "Successful children compile in input order"
                                let live = tasks.ToArray()
                                Expect.equal live.Length count "Success owns precisely one task per child"
                                Expect.isTrue (live |> Array.forall (fun task -> task.Disposals = 0)) "Children remain alive after successful composition"
                                composite.Use ignore
                                Expect.isTrue (live |> Array.forall (fun task -> task.Uses = 1)) "The returned composite forwards to every live child"
                                Expect.isTrue (live |> Array.forall (fun task -> task.Disposals = 0)) "Use does not release children"
                                composite.Dispose()
                                composite.Dispose()
                                Expect.isTrue (tasks |> Seq.forall (fun task -> task.Disposals = 1)) "Composite disposal releases each child exactly once")
            }

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
