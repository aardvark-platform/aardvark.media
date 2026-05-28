# Architecture

Reference for ELM-based architecture in aardvark.media.

---

## ELM Architecture Overview

aardvark.media implements a unidirectional data flow pattern inspired by the ELM language:

```
┌─────────┐      ┌────────┐      ┌──────┐
│  Model  │─────▶│ Update │─────▶│ View │
└─────────┘      └────────┘      └──────┘
     ▲               ▲                │
     │               │                │
     └───────────────┴────────────────┘
              Messages
```

- **Model**: Immutable state
- **Update**: Pure function `Model → Message → Model`
- **View**: Pure function `AdaptiveModel → DomNode<Message>`
- **Messages**: User actions flow back to update model

### Adaptive/Incremental Updates

Uses `FSharp.Data.Adaptive` for efficient incremental updates:

| Type | Purpose | Example |
|------|---------|---------|
| `aval<'T>` | Single adaptive value | `aval { let! count = model.count in return count * 2 }` |
| `aset<'T>` | Adaptive set | `ASet.ofList [1; 2; 3]` |
| `alist<'T>` | Adaptive list (ordered) | `AList.ofList items` |
| `amap<'K,'V>` | Adaptive map | `AMap.ofList [(1, "one"); (2, "two")]` |

Only changed values trigger UI updates—no full re-render on every state change.

---

## App&lt;'model, 'mmodel, 'msg&gt; Type

Core application definition in `Aardvark.UI/App.fs:14-21`:

```fsharp
type App<'model, 'mmodel, 'msg> =
    {
        unpersist   : Unpersist<'model, 'mmodel>
        initial     : 'model
        threads     : 'model -> ThreadPool<'msg>
        update      : 'model -> 'msg -> 'model
        view        : 'mmodel -> DomNode<'msg>
    }
```

### Fields

| Field | Type | Purpose |
|-------|------|---------|
| `unpersist` | `Unpersist<'model, 'mmodel>` | Bridges immutable model to adaptive model |
| `initial` | `'model` | Starting state |
| `threads` | `'model -> ThreadPool<'msg>` | Background work (timers, async ops) |
| `update` | `'model -> 'msg -> 'model` | State transition function |
| `view` | `'mmodel -> DomNode<'msg>` | Renders adaptive model to DOM |

### Example: Minimal App

```fsharp
// Model.fs
type Model = { value : int }
type Message = Inc

[<ModelType>]
type AdaptiveModel = { value : aval<int> }

// App.fs
let update (model : Model) (msg : Message) =
    match msg with
    | Inc -> { model with value = model.value + 1 }

let view (model : AdaptiveModel) =
    div [] [
        button [onClick (fun _ -> Inc)] [text "Increment"]
        Incremental.text (model.value |> AVal.map string)
    ]

let app : App<Model, AdaptiveModel, Message> =
    {
        unpersist = Unpersist.instance
        initial = { value = 0 }
        threads = fun _ -> ThreadPool.empty
        update = update
        view = view
    }
```

---

## Unpersist&lt;'model, 'mmodel&gt;

Manages synchronization between immutable model (`'model`) and adaptive model (`'mmodel`).

**Definition** (`Aardvark.UI/Core.fs:1259-1263`):

```fsharp
type Unpersist<'model, 'mmodel> =
    {
        create : 'model -> 'mmodel
        update : 'mmodel -> 'model -> unit
    }
```

### Automatic Unpersist with `[<ModelType>]`

Annotate adaptive model with `[<ModelType>]` to auto-generate `Create` and `Update` methods:

```fsharp
type Model = { count : int; name : string }

[<ModelType>]
type AdaptiveModel = { count : aval<int>; name : aval<string> }

// Use:
let app = {
    unpersist = Unpersist.instance  // Auto-wired
    // ...
}
```

**Behind the scenes** (`App.fs:27,93`):
- `create`: Builds `AdaptiveModel` from `Model` (wraps fields in `AVal.init`)
- `update`: Syncs changes to existing `AdaptiveModel` via `transact`

---

## App Initialization

### App.start vs App.startAndGetState

```fsharp
// Simple: just start the app
let instance : MutableApp<'model, 'msg> =
    app |> App.start

// Advanced: get both adaptive model and app control
let (mmodel, instance) : 'mmodel * MutableApp<'model, 'msg> =
    app.startAndGetState()
```

**MutableApp** (`App.fs:144-152`) provides:
- `model : aval<'model>` – Current state
- `ui : DomNode<'msg>` – Root DOM node
- `update : Guid -> seq<'msg> -> unit` – Send messages
- `updateSync : Guid -> seq<'msg> -> unit` – Synchronous update
- `shutdown : unit -> unit` – Cleanup
- `messages : IEvent<'msg>` – Observe outgoing messages

### Hosting with Suave WebPart

```fsharp
// Program.fs
use runtime = new OpenGlApplication().Runtime
let instance = app |> App.start

WebPart.startServerLocalhost 4321 [
    MutableApp.toWebPart runtime instance
    Suave.Files.browseHome
] |> ignore
```

**toWebPart** (`MutableApp.fs:343-344`) creates:
- `/` – HTML template
- `/events` – WebSocket for UI updates
- `/rendering` – 3D scene rendering service

**toWebPart' with GPU compression** (`MutableApp.fs:93`):
```fsharp
MutableApp.toWebPart' runtime true instance  // JPEG compression for scenes
```

---

## ThreadPool&lt;'msg&gt;

Manages background operations that yield messages back to the update loop.

**Location**: Defined in FSharp.Control.Incremental (dependency), used throughout examples.

### Basic Pattern

```fsharp
let threads (model : Model) =
    if model.animationEnabled then
        let rec tick() =
            proclist {
                do! Proc.Sleep 10
                yield Tick
                yield! tick()
            }
        ThreadPool.add "timer" (tick()) ThreadPool.empty
    else
        ThreadPool.empty
```

### proclist Computation Expression

Builder for async message streams:

```fsharp
proclist {
    do! Proc.Sleep 100           // Delay
    do! Proc.SwitchToNewThread() // Run in background
    yield Message1               // Emit message
    let! result = Proc.Await asyncTask  // Await async/MVar
    yield! otherProclist         // Chain
}
```

| Operation | Purpose |
|-----------|---------|
| `Proc.Sleep ms` | Non-blocking delay |
| `Proc.Await (MVar.takeAsync mvar)` | Wait for MVar result |
| `Proc.SwitchToNewThread()` | Move to background thread |
| `yield msg` | Emit message to update loop |
| `yield! proc` | Recursively chain procs |

### ThreadPool Operations

```fsharp
ThreadPool.empty                          // No threads
ThreadPool.add "id" proc pool             // Add/replace thread by ID
ThreadPool.union pool1 pool2              // Merge pools
ThreadPool.map f pool                     // Transform messages
```

**Thread lifecycle** (`App.fs:49-63`):
- Starting thread: `adjustThreads` calls `Command.Start(emit)`
- Stopping thread: `Command.Stop()` on removal
- Messages flow via `emit : 'msg -> unit` callback

---

## MVar Pattern for Async Results

`MVar` (mutable variable) coordinates async work with UI updates.

### Example: Background Intersection Sampling

```fsharp
let update model msg =
    match msg with
    | StartSampling ->
        let result = MVar.empty()

        // Async work (off-thread)
        async {
            let points = computeIntersections()
            MVar.put result (Choice1Of2 points)
        } |> Async.Start

        // UI thread monitors result
        let proc =
            proclist {
                let! r = Proc.Await (MVar.takeAsync result)
                match r with
                | Choice1Of2 points -> yield UpdatePoints points
                | Choice2Of2 error -> yield ShowError error
            }

        { model with threads = ThreadPool.add "sampling" proc model.threads }
```

**Key MVar operations**:
- `MVar.empty()` – Create
- `MVar.put mvar value` – Write (unblocks readers)
- `MVar.takeAsync mvar` – Read async (blocks until available)
- `MVar.take mvar` – Read sync (blocks)

---

## WebPart Composition with Suave

Combine aardvark.media routes with custom endpoints.

### Basic Composition

```fsharp
let customApi : WebPart =
    path "/api/data" >=> Successful.OK "custom data"

WebPart.startServerLocalhost 4321 [
    customApi
    MutableApp.toWebPart runtime instance
    Suave.Files.browseHome
] |> ignore
```

### Multi-App Architecture

Host multiple apps on different paths:

```fsharp
// Separate apps
let viewerApp = App.start viewerAppDef
let editorApp = App.start editorAppDef

let routes =
    choose [
        pathStarts "/viewer" >=> MutableApp.toWebPart runtime viewerApp
        pathStarts "/editor" >=> MutableApp.toWebPart runtime editorApp
        path "/" >=> Redirection.redirect "/viewer"
    ]

WebPart.startServer 8080 [routes]
```

**Gotcha**: Each app requires separate runtime or shared runtime must be thread-safe.

### Shared Runtime Pattern

```fsharp
use app = new OpenGlApplication()
let runtime = app.Runtime

let viewer = viewerApp |> App.start
let editor = editorApp |> App.start

WebPart.startServerLocalhost 4321 [
    pathStarts "/viewer" >=> MutableApp.toWebPart runtime viewer
    pathStarts "/editor" >=> MutableApp.toWebPart runtime editor
]
```

---

## Complete Working Examples

### 1. Basic Counter

```fsharp
// Model.fs
module Counter.Model

type Model = { count : int }
type Message = Increment | Decrement

[<ModelType>]
type AdaptiveModel = { count : aval<int> }

// App.fs
module Counter.App
open Aardvark.UI

let update model msg =
    match msg with
    | Increment -> { model with count = model.count + 1 }
    | Decrement -> { model with count = model.count - 1 }

let view (m : AdaptiveModel) =
    div [] [
        button [onClick (fun _ -> Decrement)] [text "-"]
        Incremental.text (m.count |> AVal.map (sprintf " %d "))
        button [onClick (fun _ -> Increment)] [text "+"]
    ]

let app =
    {
        unpersist = Unpersist.instance
        initial = { count = 0 }
        threads = fun _ -> ThreadPool.empty
        update = update
        view = view
    }

// Program.fs
open Aardvark.Application.Slim
open Aardvark.UI
open Suave

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use app = new OpenGlApplication()
    let instance = Counter.App.app |> App.start

    WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart app.Runtime instance
    ] |> ignore

    System.Console.ReadLine() |> ignore
    0
```

### 2. Animation with ThreadPool

```fsharp
type Model = { time : float; running : bool }
type Message = Tick of float | ToggleRunning

[<ModelType>]
type AdaptiveModel = { time : aval<float>; running : aval<bool> }

let update model msg =
    match msg with
    | Tick t -> { model with time = t }
    | ToggleRunning -> { model with running = not model.running }

let threads (model : Model) =
    if model.running then
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let rec loop() =
            proclist {
                do! Proc.Sleep 16  // ~60 FPS
                yield Tick sw.Elapsed.TotalSeconds
                yield! loop()
            }
        ThreadPool.add "timer" (loop()) ThreadPool.empty
    else
        ThreadPool.empty

let view (m : AdaptiveModel) =
    div [] [
        button [onClick (fun _ -> ToggleRunning)] [
            Incremental.text (m.running |> AVal.map (fun r -> if r then "Stop" else "Start"))
        ]
        Incremental.text (m.time |> AVal.map (sprintf " Time: %.2f"))
    ]

let app =
    {
        unpersist = Unpersist.instance
        initial = { time = 0.0; running = false }
        threads = threads
        update = update
        view = view
    }
```

### 3. Multi-App Hosting

```fsharp
// CounterApp.fs
module CounterApp =
    let app = (* counter app definition *)

// ClockApp.fs
module ClockApp =
    let app = (* clock app definition *)

// Program.fs
[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use runtime = new OpenGlApplication().Runtime

    let counter = CounterApp.app |> App.start
    let clock = ClockApp.app |> App.start

    let routes =
        choose [
            pathStarts "/counter" >=> MutableApp.toWebPart runtime counter
            pathStarts "/clock" >=> MutableApp.toWebPart runtime clock
            path "/" >=> Redirection.redirect "/counter"
            Suave.Files.browseHome
        ]

    WebPart.startServerLocalhost 4321 [routes] |> ignore

    printfn "Counter: http://localhost:4321/counter"
    printfn "Clock: http://localhost:4321/clock"
    System.Console.ReadLine() |> ignore
    0
```

---

## Gotchas

### 1. Forgetting `[<ModelType>]`

**Problem**: Unpersist.instance won't compile without code generation.

```fsharp
// ❌ Missing annotation
type AdaptiveModel = { count : aval<int> }

// ✅ Correct
[<ModelType>]
type AdaptiveModel = { count : aval<int> }
```

### 2. Thread ID Collisions

**Problem**: Using same ID replaces existing thread.

```fsharp
// ❌ Both use "timer" – second overwrites first
ThreadPool.add "timer" proc1 ThreadPool.empty
|> ThreadPool.add "timer" proc2  // proc1 is lost

// ✅ Unique IDs
ThreadPool.add "timer1" proc1 ThreadPool.empty
|> ThreadPool.add "timer2" proc2
```

### 3. Blocking in Update Function

**Problem**: Update must be fast; blocking freezes UI.

```fsharp
// ❌ Blocks update thread
let update model msg =
    match msg with
    | LoadData ->
        let data = Http.RequestString "http://slow-api.com"  // BLOCKS
        { model with data = data }

// ✅ Use threads instead
let threads model =
    match model.pendingLoad with
    | Some url ->
        let proc =
            proclist {
                do! Proc.SwitchToNewThread()
                let data = Http.RequestString url
                yield DataLoaded data
            }
        ThreadPool.add "loader" proc ThreadPool.empty
    | None -> ThreadPool.empty
```

### 4. Forgetting transact for Adaptive Updates

**Problem**: Adaptive values need transaction context.

```fsharp
// ❌ Won't update dependents
let mutable count = AVal.init 0
count.Value <- 5  // No change propagation

// ✅ Use transact
transact (fun () -> count.Value <- 5)
```

**Note**: `Unpersist.update` already wraps updates in `transact` (`App.fs:72-94`), so manual `transact` only needed for direct adaptive manipulation.

### 5. Runtime Disposal Before Shutdown

**Problem**: Disposing runtime before stopping apps causes crashes.

```fsharp
// ❌ Wrong order
use runtime = new OpenGlApplication()
let instance = app |> App.start
runtime.Dispose()  // Runtime dies, instance still running

// ✅ Dispose after shutdown
use runtime = new OpenGlApplication()
let instance = app |> App.start
instance.shutdown()
runtime.Dispose()
```

**Better**: Use `use` for automatic disposal after scope exit.

---

## See Also

- [UI.md](UI.md) - DomNode, view construction, events
- [PRIMITIVES.md](PRIMITIVES.md) - Camera controllers, animations
- [RENDERING.md](RENDERING.md) - Server setup, WebPart composition
- [ADVANCED.md](ADVANCED.md) - Multi-app patterns, mailbox communication
- Examples: `src/Examples (dotnetcore)/01 - Inc/`, `08 - AnimationAsyncExample/`

---

*Document Size: ~15 KB*
*Last Updated: 2025-12-22*
