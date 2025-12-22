# Advanced Patterns

Advanced patterns for Aardvark.Media applications discovered in production codebases.

## DataChannel Pattern (JS Interop)

Bi-directional data synchronization between F# and JavaScript using `ChannelReader`.

### Basic Pattern

```fsharp
type Message =
    | SetValue of string
    | UpdateSelection of list<int>

let update (model: MModel) (msg: Message) =
    match msg with
    | SetValue v -> { model with value = v }
    | UpdateSelection ids -> { model with selected = HashSet.ofList ids }

let channels =
    let reader, writer = Channel.CreateUnbounded<Message>()
    writer, reader :> ChannelReader<Message>

let view (model: MModel) =
    let channelWriter = snd channels

    onBoot "$('#element').customPlugin({
        onChange: (v) => { channel.write({ tag: 'SetValue', value: v }); }
    });" [
        "channel" => channelWriter
    ]
```

### Boot vs Update Code

- **Boot code**: Runs once when DOM element is created
- **Update code**: Runs on every model change

```fsharp
// Boot: Initialize third-party component
onBoot "const picker = new CustomPicker('#picker', options);" []

// Update: Respond to model changes
onEvent "change" [] (fun v -> SetValue v)
```

### Calendar Component Example

```fsharp
type CalendarMessage =
    | SelectDate of DateTime
    | SetRange of DateTime * DateTime

let calendarView (model: MModel) =
    let onSelect = Channel.CreateUnbounded<CalendarMessage>() |> snd

    div [] [
        onBoot """
            const cal = new FullCalendar.Calendar(element, {
                selectable: true,
                select: (info) => {
                    channel.write({
                        tag: 'SetRange',
                        start: info.start,
                        end: info.end
                    });
                }
            });
            cal.render();
        """ [
            "channel" => onSelect
        ]
    ]
```

## Custom UI Components

### Require Pattern

Load JavaScript and CSS dependencies dynamically:

```fsharp
require (Html.semui @ [
    { name = "fullcalendar"; url = "fullcalendar/main.js"; kind = Script }
    { name = "fullcalendar-css"; url = "fullcalendar/main.css"; kind = Stylesheet }
]) (
    calendarView model
)
```

### OnBoot Initialization

Initialize jQuery/Fomantic components that need DOM access:

```fsharp
let dropdown (values: alist<string>) (selected: IMod<string>) =
    onBoot "$('#dropdown').dropdown();" (
        Incremental.select [
            clazz "ui dropdown"
            onChange (fun v -> SetValue v)
        ] (
            alist {
                for v in values do
                    yield option [attribute "value" v] [text v]
            }
        )
    )
```

### VirtualTree Pattern

Handle large hierarchies efficiently:

```fsharp
type TreeNode = { id: Guid; label: string; children: aset<TreeNode> }

let rec virtualTree (node: AdaptiveNode) =
    let visible = node.expanded |> AVal.map (fun e -> if e then "block" else "none")

    div [] [
        span [onClick (fun _ -> ToggleExpanded node.id)] [text node.label]
        Incremental.div
            (AttributeMap.ofAVal (visible |> AVal.map (fun v -> style $"display: {v}")))
            (alist {
                let! exp = node.expanded
                if exp then
                    for child in node.children do
                        yield virtualTree child
            })
    ]
```

## Multi-App Architecture

### Running Multiple Apps

```fsharp
// App1: Main viewer on port 4321
let app1 = {
    unpersist = Unpersist.instance
    threads = fun model -> ThreadPool.empty
    initial = Model.initial
    update = update
    view = view
}

// App2: Control panel on port 4322
let app2 = {
    unpersist = Unpersist.instance
    threads = fun model -> mailboxThreads model
    initial = ControlModel.initial
    update = controlUpdate
    view = controlView
}

[<EntryPoint>]
let main args =
    Aardvark.Init()
    Aardium.init()

    // Shared runtime
    use runtime = new Runtime()

    // Start both apps
    let instance1 = App.start app1
    let instance2 = App.start app2

    WebPart.startServerLocalhost 4321 [MutableApp.toWebPart runtime instance1] |> ignore
    WebPart.startServerLocalhost 4322 [MutableApp.toWebPart runtime instance2] |> ignore

    Aardium.run {
        url "http://localhost:4321"
        width 1920
        height 1080
    }
    0
```

### Communication Patterns

**Mailbox for cross-app messaging:**

```fsharp
type CrossAppMessage =
    | DataUpdated of string
    | SelectionChanged of Set<Guid>

let mailbox = MailboxProcessor.Start(fun inbox ->
    async {
        while true do
            let! msg = inbox.Receive()
            // Broadcast to all apps
            transact (fun () ->
                app1Model.Value <- App1.update app1Model.Value msg
                app2Model.Value <- App2.update app2Model.Value msg
            )
    })

// In app1 update:
| SomeAction ->
    mailbox.Post (DataUpdated "new data")
    model
```

**Shared state:**

```fsharp
let sharedSelection = cval (Set.empty<Guid>)

// App1 writes
transact (fun () -> sharedSelection.Value <- newSelection)

// App2 reads
let view (model: MModel) =
    let selection = sharedSelection :> aval<_>
    Incremental.div
        (AttributeMap.ofList [clazz "selection-count"])
        (alist {
            let! sel = selection
            yield text $"Selected: {Set.count sel}"
        })
```

### WebPart Composition

```fsharp
let apiPart =
    path "/api/data" >=> Successful.OK (Json.serialize data)

let appPart runtime instance =
    MutableApp.toWebPart runtime instance

let fullApp runtime instance =
    choose [
        apiPart
        appPart runtime instance
        RequestErrors.NOT_FOUND "Not found"
    ]

WebPart.startServerLocalhost 4321 [fullApp runtime instance]
```

## Rendering Patterns

### Render-to-Texture for Shadow Maps

```fsharp
let shadowMapSignature =
    runtime.CreateFramebufferSignature [
        DefaultSemantic.Depth, TextureFormat.Depth24Stencil8
    ]

let shadowMap =
    let size = V2i(2048, 2048)

    Sg.fullScreenQuad
    |> Sg.shader {
        do! Shader.depthOnly
    }
    |> Sg.viewTrafo (lightView |> AVal.map CameraView.viewTrafo)
    |> Sg.projTrafo (lightProj |> AVal.map Frustum.projTrafo)
    |> Sg.compile runtime shadowMapSignature
    |> RenderTask.renderToColor size

// Use in main render:
Sg.sphere 5 (Mod.constant C4b.White) (Mod.constant 1.0)
|> Sg.diffuseTexture shadowMap
|> Sg.shader {
    do! Shader.shadowMapping
}
```

### Custom Scene Graph Semantics

```fsharp
[<Rule>]
type CustomSem() =
    member x.OriginOffset(e: Sg.OriginOffsetNode, scope: Ag.Scope) : IMod<V3d> =
        e.Offset

    member x.ModelTrafo(offset: IMod<V3d>, inner: IMod<Trafo3d>) : IMod<Trafo3d> =
        adaptive {
            let! o = offset
            let! i = inner
            return Trafo3d.Translation(o) * i
        }

type Sg.OriginOffsetNode(offset: IMod<V3d>, child: ISg) =
    interface ISg with
        member x.Children = [child]
        member x.RenderObjects(_) = RenderObjectSemantics.Dynamic
    member x.Offset = offset

module Sg =
    let originOffset (offset: IMod<V3d>) (sg: ISg) =
        Sg.OriginOffsetNode(offset, sg) :> ISg
```

### Large-Scale Coordinate Handling

```fsharp
// Origin-relative rendering for planet-scale coordinates
let renderWithOrigin (origin: V3d) (positions: V3d[]) =
    let relative = positions |> Array.map (fun p -> p - origin)

    Sg.ofArray relative
    |> Sg.trafo (Mod.constant (Trafo3d.Translation origin))
```

### Geometry Pooling

```fsharp
type GeometryPool(runtime: IRuntime, maxInstances: int) =
    let instances = clist<Instance>()

    let sg =
        instances
        |> AList.toASet
        |> ASet.map (fun inst ->
            Sg.ofIndexedGeometry inst.geometry
            |> Sg.trafo (Mod.constant inst.trafo)
        )
        |> Sg.set
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }

    member x.Add(geom: IndexedGeometry, trafo: Trafo3d) =
        if instances.Count < maxInstances then
            transact (fun () -> instances.Add { geometry = geom; trafo = trafo })

    member x.Scene = sg
```

### Depth Bias for Layered Rendering

```fsharp
// Render wireframe over solid without z-fighting
let layeredView (model: MModel) =
    Sg.ofList [
        // Solid geometry
        Sg.sphere 5 (Mod.constant C4b.Gray) (Mod.constant 1.0)

        // Wireframe overlay with depth bias
        Sg.sphere 5 (Mod.constant C4b.Red) (Mod.constant 1.0)
        |> Sg.effect [
            toEffect DefaultSurfaces.trafo
            toEffect (
                """
                void main() {
                    gl_Position = gl_Position;
                    gl_Position.z -= 0.0001;
                }
                """, ShaderStage.Fragment
            )
        ]
        |> Sg.fillMode (Mod.constant FillMode.Line)
    ]
```

## Performance Patterns

### Adaptive Collection Performance

`ASet` and `AMap` handle hundreds of elements efficiently:

```fsharp
// Efficiently update large collections
let items = cset<Item>()  // Thousands of items

// Good: Incremental updates
transact (fun () ->
    items.Add newItem
    items.Remove oldItem
)

// Bad: Replacing entire collection
transact (fun () ->
    items.Clear()
    items.UnionWith newItems  // Triggers full rebuild
)
```

### Scene Graph Compilation Caching

```fsharp
// Cache compiled render objects
let cache = Dictionary<Guid, IRenderObject>()

let getCached (id: Guid) (create: unit -> ISg) =
    match cache.TryGetValue(id) with
    | true, ro -> ro
    | false, _ ->
        let sg = create()
        let ro = sg |> Sg.compile runtime signature
        cache.[id] <- ro
        ro
```

### AdaptiveToken Manual Evaluation

```fsharp
// Avoid over-evaluation in hot paths
let token = AdaptiveToken()

let expensiveComputation (input: aval<int>) =
    token.Use(fun () ->
        let value = input.GetValue(token)
        // Expensive work
        complexCalculation value
    )

// Control when evaluation happens
token.MarkOutdated()
let result = expensiveComputation input
```

## Aardium Integration

### Initialization Variants

```fsharp
// Simple: Default config
Aardium.init()

// Advanced: Specify chromium path
Aardium.initAt @"C:\CustomPath\chromium"
```

### Configuration Options

```fsharp
Aardium.run {
    url "http://localhost:4321"
    width 1920
    height 1080
    debug true  // Enable DevTools
    title "My Aardvark App"
}
```

### Platform Detection

```fsharp
let startApp() =
    if System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        Aardium.init()
        Aardium.run {
            url "http://localhost:4321"
            width 1280
            height 720
        }
    else
        printfn "Open browser at http://localhost:4321"
        System.Threading.Thread.Sleep(-1)
```

## Gotchas

| Issue | Solution |
|-------|----------|
| `onBoot` code runs every update | Move initialization to `onBoot`, updates to `onChange` |
| ChannelReader messages dropped | Use unbounded channel or check `TryWrite` return value |
| Large ASet performance degradation | Batch updates in single `transact`, avoid `Clear()` + `UnionWith` |
| Z-fighting in overlaid geometry | Use depth bias or `DepthTestMode.None` for overlay |
| Memory leak from cached RenderObjects | Dispose old objects when removing from cache |
| JavaScript `require` fails silently | Check browser console, verify URL paths are correct |
| Multi-app shared state not updating | Ensure both apps reference same `cval` instance, not copies |
| Scene graph recompilation on every frame | Use `Sg.dynamic` or cache compiled `IRenderObject` instances |
| Adaptive token stale values | Call `token.MarkOutdated()` before evaluation |
| Origin-relative coordinates still imprecise | Use `float32` for GPU buffers, `float64` for CPU calculations |

---

## See Also

- [UI.md](UI.md) - onBoot, require, DomNode construction
- [ARCHITECTURE.md](ARCHITECTURE.md) - Multi-app structure, ThreadPool
- [RENDERING.md](RENDERING.md) - WebPart composition, CEF integration
- [PRIMITIVES.md](PRIMITIVES.md) - Animation system, camera controllers

---

*Document Size: ~12 KB*
*Last Updated: 2025-12-22*
