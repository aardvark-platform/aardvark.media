namespace Aardvark.UI.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

module ``MutableApp Tests`` =

    type private Message =
        | Add of int
        | SetCommands of string list

    type private Model =
        { value: int; commands: string list }

    type private RecordingCommand(name: string, events: ConcurrentQueue<string>) =
        inherit Command<Message>()

        let gate = obj()
        let mutable starts = 0
        let mutable stops = 0
        let mutable emit = None

        member _.Starts = Volatile.Read(&starts)
        member _.Stops = Volatile.Read(&stops)
        member val OnStop = ignore with get, set

        member _.Emit(message) =
            lock gate (fun () -> (Option.get emit) message)

        override _.Start(callback) =
            lock gate (fun () ->
                Interlocked.Increment(&starts) |> ignore
                emit <- Some callback
                events.Enqueue $"start:{name}"
            )

        override this.Stop() =
            lock gate (fun () ->
                Interlocked.Increment(&stops) |> ignore
                emit <- None
                events.Enqueue $"stop:{name}"
                this.OnStop()
            )

    let private withApp initialCommands names action =
        let events = ConcurrentQueue<string>()
        let commands = names |> List.map (fun name -> name, RecordingCommand(name, events)) |> Map.ofList
        let app =
            {
                initial = { value = 0; commands = initialCommands }
                threads = fun model ->
                    model.commands
                    |> List.map (fun name -> name, commands.[name] :> Command<Message>)
                    |> HashMap.ofList
                    |> ThreadPool<Message>
                update = fun model message ->
                    match message with
                    | Add value -> { model with value = model.value + value }
                    | SetCommands names -> { model with commands = names }
                unpersist = { create = AVal.init; update = fun model value -> model.Value <- value }
                view = fun _ -> DomNode.Empty()
            }
            |> App.start
        let mutable disposed = false
        let dispose () =
            disposed <- true
            app.Dispose()
        try
            action app commands events dispose
        finally
            try
                if not disposed then dispose ()
            finally
                // Assert app-owned cleanup first; this fallback must not hide leaked commands.
                for KeyValue(_, command) in commands do
                    if command.Starts > 0 && command.Stops = 0 then command.Stop()

    let private registerResources (app: MutableApp<Model, cval<Model>, Message>) (events: ConcurrentQueue<string>) =
        for name in ["first"; "last"] do
            app.Register { new IDisposable with member _.Dispose() = events.Enqueue $"resource:{name}" }

    let private checkResourcesAfterStops (expected: string list) (events: ConcurrentQueue<string>) =
        let events = events.ToArray() |> Array.toList
        let cleanup = events |> List.filter (fun event -> not (event.StartsWith "start:"))
        Expect.equal (cleanup |> List.take expected.Length |> List.sort) (expected |> List.map (fun name -> $"stop:{name}") |> List.sort)
            "Every active command must be stopped before any resource is disposed"
        Expect.equal (cleanup |> List.skip expected.Length) ["resource:last"; "resource:first"]
            "Registered resources must retain their disposal order"

    [<Tests>]
    let tests =
        testList "MutableApp Tests" [
            test "Disposal stops initial commands before registered resources" {
                withApp ["a"; "b"] ["a"; "b"] (fun app commands events dispose ->
                    let token = app.CancellationToken
                    let stopStates = ConcurrentQueue<bool * bool>()
                    for KeyValue(_, command) in commands do
                        Expect.equal (command.Starts, command.Stops) (1, 0) "Initial commands must start once"
                        command.OnStop <- fun () -> stopStates.Enqueue(token.IsCancellationRequested, Monitor.IsEntered app.UpdateLock)
                    registerResources app events
                    dispose ()
                    Expect.equal (commands |> Map.toList |> List.map (fun (_, command) -> command.Stops)) [1; 1]
                        "Disposal must stop every active command exactly once"
                    Expect.equal (stopStates.ToArray() |> Array.toList) [true, true; true, true]
                        "Command cancellation must follow app cancellation and run under the update lock"
                    checkResourcesAfterStops ["a"; "b"] events
                )
            }

            test "Disposal stops the current pool without repeating removed commands" {
                withApp ["removed"; "retained"] ["removed"; "retained"; "new"] (fun app commands events dispose ->
                    app.UpdateSync(Guid.Empty, [SetCommands ["retained"; "new"]; Add 7; Add 5])
                    let expected = { value = 12; commands = ["retained"; "new"] }
                    Expect.equal (AVal.force app.Model) expected "Messages must update the immutable model in order"
                    Expect.equal (AVal.force app.MutableModel) expected "The adaptive model must receive the final state"
                    Expect.equal (commands.["removed"].Starts, commands.["removed"].Stops) (1, 1) "Removed commands must stop during the model transition"
                    Expect.equal (commands.["retained"].Starts, commands.["retained"].Stops) (1, 0) "Retained commands must not restart"
                    Expect.equal (commands.["new"].Starts, commands.["new"].Stops) (1, 0) "New commands must start during the model transition"
                    registerResources app events
                    dispose ()
                    for KeyValue(_, command) in commands do
                        Expect.equal (command.Starts, command.Stops) (1, 1) "Only currently active commands need another stop"
                    checkResourcesAfterStops ["removed"; "retained"; "new"] events
                )
            }

            test "Command and ordinary queued messages retain model and notification ordering" {
                withApp ["producer"] ["producer"] (fun app commands _ _ ->
                    let received = ConcurrentQueue<Message>()
                    let completed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                    use subscription =
                        app.Messages.Subscribe(fun message ->
                            received.Enqueue message
                            if received.Count = 3 then completed.SetResult()
                        )
                    commands.["producer"].Emit(Add 2)
                    app.Update(Guid.Empty, [Add 3; Add 4])
                    completed.Task.WaitAsync(TimeSpan.FromSeconds 5.0).GetAwaiter().GetResult()
                    Expect.equal (received.ToArray() |> Array.toList) [Add 2; Add 3; Add 4] "Messages must be forwarded once in processing order"
                    let expected = { value = 9; commands = ["producer"] }
                    Expect.equal (AVal.force app.Model) expected "Queued messages must update the immutable model"
                    Expect.equal (AVal.force app.MutableModel) expected "Queued messages must update the adaptive model"
                    Expect.equal (commands.["producer"].Starts, commands.["producer"].Stops) (1, 0) "Value-only updates must retain the running command"
                )
            }

            test "An empty command pool disposes resources normally" {
                withApp [] ["unused"] (fun app commands events dispose ->
                    let token = app.CancellationToken
                    app.UpdateSync(Guid.Empty, [Add 8])
                    Expect.equal (AVal.force app.Model).value 8 "Empty pools must still process messages"
                    registerResources app events
                    dispose ()
                    Expect.isTrue token.IsCancellationRequested "Disposal must retain app cancellation"
                    Expect.equal (commands.["unused"].Starts, commands.["unused"].Stops) (0, 0) "Commands outside the pool must not start or stop"
                    checkResourcesAfterStops [] events
                )
            }
        ]
