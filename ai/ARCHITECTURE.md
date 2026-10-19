# Architecture

Aardvark.Media uses an ELM-style message loop:

- **Model:** immutable application state.
- **Update:** `Model -> Message -> Model`.
- **View:** adaptive model to `DomNode<Message>`.
- **Threads:** model-dependent background processes that emit messages.

Adaptive values (`aval`), lists (`alist`), sets (`aset`), and maps (`amap`) come from
`FSharp.Data.Adaptive`. Build adaptive expressions instead of forcing values in the view;
only dependencies read by an expression can invalidate it.

## Adaptive Models

Apply Adaptify's `[<ModelType>]` to the immutable model, not to a handwritten record of
adaptive values. The build generates `AdaptiveModel`, its `Create` / `Update` members,
and associated `.g.fs` files. Do not edit generated files.

`Model.fs`:

```fsharp
namespace Counter

open Adaptify

[<ModelType>]
type Model = { count : int }

type Message = Increment | Decrement
```

For a source type named `CameraControllerState`, the counterpart is
`AdaptiveCameraControllerState`. Nested model types and collections may have generated
adaptive wrappers; consult the generated signature rather than assuming every field is
an `aval`. `[<NonAdaptive>]` excludes a field from adaptive wrapping; use it only when
changes to that field should not drive the view.

Sources: [counter model](../src/Examples/01%20-%20Inc/Model.fs),
[camera model](../src/Aardvark.UI.Primitives/Controllers/CameraModel.fs).

## Application Structure

[App.fs](../src/Aardvark.UI/App.fs) defines `App<'model, 'mmodel, 'msg>`:

| Field | Type | Purpose |
|-------|------|---------|
| `unpersist` | `Unpersist<'model, 'mmodel>` | Create and synchronize the adaptive model |
| `initial` | `'model` | Initial immutable state |
| `threads` | `'model -> ThreadPool<'msg>` | Background message producers |
| `update` | `'model -> 'msg -> 'model` | State transition |
| `view` | `'mmodel -> DomNode<'msg>` | Adaptive DOM |

`App.fs`, compiled after `Model.fs` and its Adaptify-generated counterpart:

```fsharp
module Counter.App

open Aardvark.UI
open FSharp.Data.Adaptive
open Counter

let update (model : Model) (message : Message) =
    match message with
    | Increment -> { model with count = model.count + 1 }
    | Decrement -> { model with count = model.count - 1 }

let view (model : AdaptiveModel) =
    div [] [
        button [onClick (fun () -> Decrement)] [text "-"]
        Incremental.text (model.count |> AVal.map string)
        button [onClick (fun () -> Increment)] [text "+"]
    ]

let app : App<Model, AdaptiveModel, Message> =
    { unpersist = Unpersist.instance
      initial = { count = 0 }
      threads = fun _ -> ThreadPool.empty
      update = update
      view = view }
```

[Unpersist.fs](../src/Aardvark.UI/Core/Unpersist.fs) defines a record with
`create : 'model -> 'mmodel` and `update : 'mmodel -> 'model -> unit`.
`Unpersist.instance` binds the adaptive type's generated members. It does not itself
open a transaction: the application loop invokes it inside `transact`.

## Application Lifetime

`App.start definition` returns the disposable class
`MutableApp<'model, 'mmodel, 'msg>`, not a two-parameter record.

| Member | Purpose |
|--------|---------|
| `Model : aval<'model>` | Current immutable state as an adaptive value |
| `MutableModel : 'mmodel` | Adaptive counterpart used by the view |
| `Dom : DomNode<'msg>` | Root DOM |
| `Update(session, messages)` | Queue messages for the update thread |
| `UpdateSync(session, messages)` | Process messages synchronously |
| `Messages : IEvent<'msg>` | Observe processed messages |
| `UpdateLock : obj` | Lock used by message processing |
| `CancellationToken` | Server and connection lifetime token |
| `DocumentTitle` | Title used by the server's HTML template |
| `ApplicationError` | Application update, event-handler, or channel errors |
| `InternalError` | Infrastructure and framework errors |
| `Dispose()` | Cancel lifetime, join the update thread, request command cancellation, and release registered resources |

Use `use` to own an app's lifetime. Access its adaptive state through `MutableModel`.
Send external messages through `Update` or `UpdateSync` instead of directly editing
`Model` or the adaptive counterpart. `Dispose()` requests background command cancellation
but does not join command workers; coordinate their completion separately if they depend
on the rendering runtime.

Create the rendering application before the mutable app so it outlives it. Cancel/dispose
apps and await server shutdown before releasing the rendering runtime. See
[server setup](RENDERING.md#server-setup) for startup and ownership requirements.

Sources: [MutableApp.fs](../src/Aardvark.UI/MutableApp.fs),
[interfaces](../src/Aardvark.UI/Core/Interface.fs).

## Background Messages

`ThreadPool<'msg>` is defined in the `FSharp.Data.Adaptive` namespace by the
`Aardvark.Base.Incremental` dependency. The application compares thread pools after
updates, starts newly added commands, and stops removed commands. An ID present in both pools keeps the existing command; assigning
a new process to the same ID does not restart it. Remove and re-add it across updates,
or change its ID, when a restart is required.

A standalone model-dependent timer:

```fsharp
module Timer

open Aardvark.UI
open FSharp.Data.Adaptive

type Model = { running : bool }
type Message = Tick

let threads (model : Model) : ThreadPool<Message> =
    if model.running then
        let rec tick () =
            proclist {
                do! Proc.Sleep 100
                yield Tick
                yield! tick ()
            }
        ThreadPool.empty |> ThreadPool.add "timer" (tick ())
    else
        ThreadPool.empty
```

Use `ThreadPool.map` to lift child messages and `ThreadPool.union` to combine independent
pools. Give concurrently running commands distinct IDs. Avoid blocking I/O in `update`;
use background commands and emit success/failure messages. Use the existing
[async example](../src/Examples/08%20-%20AnimationAsyncExample/App.fs) for task coordination,
progress messages, and cancellation rather than starting untracked work from a view.

## Direct Adaptive Updates

Ordinary app updates are already transactional. When managing separate changeable values
outside the app, mutate them inside `transact`:

```fsharp
module AdaptiveState

open FSharp.Data.Adaptive

let setCount (count : cval<int>) value =
    transact (fun () -> count.Value <- value)
```

Keep such shared state explicitly owned; it does not replace the app's immutable update
loop. See [multi-app hosting](ADVANCED.md#multi-app-hosting) for routing and lifetime concerns.

## See Also

- [UI.md](UI.md) — DOM and events
- [RENDERING.md](RENDERING.md) — hosting and runtime lifetime
- [PRIMITIVES.md](PRIMITIVES.md) — controller and animator integration
- [ADVANCED.md](ADVANCED.md) — channels, requests, and multiple apps
