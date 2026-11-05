# Hosting and Rendering

`Aardvark.UI` contains backend-independent application, HTTP, and rendering internals.
Hosting adapters are separate packages and namespaces:

| Backend | Target | Guidance |
|---------|--------|----------|
| `Aardvark.UI.Giraffe` | .NET 8 | Recommended for .NET Core applications; ASP.NET Core / Giraffe |
| `Aardvark.UI.Suave` | .NET Standard 2.0 | For applications that cannot target .NET Core |

The backends are independent. Opening a backend namespace supplies its `Server`,
`WebPart`, and `MutableApp.toWebPart` wrappers. Use one backend explicitly in a hosting
module; do not mix their handler types.

## Server Setup

Given a caller-owned rendering runtime and an already-started mutable app:

```fsharp
module GiraffeHosting

open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Giraffe

let start (runtime : IRuntime) (app : MutableApp<'model, 'mmodel, 'msg>) =
    Server.startLocalhost 4321 app.CancellationToken [
        MutableApp.toWebPart runtime app
        WebPart.ofType<Primitives.EmbeddedResources>
    ]
```

The caller owns the app/runtime and must retain the returned server task. On normal and
exceptional exits, cancel/dispose the mutable app, await server completion, then dispose
the rendering application/runtime. This startup function does not implement that cleanup.
See [application lifetime](ARCHITECTURE.md#application-lifetime) and the
[Giraffe entry point](../src/Examples/02%20-%20RenderControl/Program.fs) for app/runtime setup.

`Server.startLocalhost` binds to loopback. For remote hosting, use `Server.start` with an
explicit URL and the appropriate authentication/network controls; selecting a remote bind
address does not provide access control.

| API | Giraffe | Suave |
|-----|---------|-------|
| Local start | `Server.startLocalhost port token content` | Same arguments |
| URL start | `Server.start url token responseCompression content` | `Server.start url token content` |
| Return value | `Task` representing server lifetime | `Task` representing server lifetime |
| Embedded resources | `WebPart.ofAssembly`, `ofType`, `ofAssemblyWith` | Same helpers |
| App handler | `MutableApp.toWebPart runtime app` | Same arguments, different handler type |
| GPU JPEG compression | `MutableApp.toWebPart' runtime useGpuCompression app` | Same arguments |

Both backends take a lifetime token rather than exposing a standalone `Server.stop` call.
A canceled Suave task may report
`OperationCanceledException` when awaited; distinguish expected cancellation from server faults.
`Aardvark.UI.Server.getFreeTcpPort IPAddress.Loopback` can discover a free port, but does not
reserve it between discovery and server startup.

For Suave, use `open Aardvark.UI.Suave` in the startup example instead. For custom Giraffe
hosts, `Server.createHost url responseCompression content` returns a host builder; preserve
WebSocket middleware and app/server lifetime handling if replacing the provided setup.

Sources: [Giraffe server](../src/Aardvark.UI.Giraffe/Server.fs),
[Suave server](../src/Aardvark.UI.Suave/Server.fs),
[Giraffe wrappers](../src/Aardvark.UI.Giraffe/WebPart.fs),
[Suave wrappers](../src/Aardvark.UI.Suave/WebPart.fs).
Examples: [Giraffe render control](../src/Examples/02%20-%20RenderControl/Program.fs),
[Suave counter](../src/Examples/01%20-%20Inc/Program.fs).

## Embedded Resources

`MutableApp.toWebPart` already serves the `Aardvark.UI` assembly. Add resource handlers for
application and primitive assemblies as needed. `WebPart.ofType<Primitives.EmbeddedResources>`
serves the primitives' bundled CSS/JavaScript; `require` in a view only requests their URLs.

Mark files as `EmbeddedResource` in the owning project. For non-.NET-Framework assemblies,
the default chooser maps names case-insensitively:

| Manifest name | URL path |
|---------------|----------|
| `AssemblyName.resources.name` | `/resources/name` |
| `AssemblyName.name` | `/name` |
| `resources/name` or `resources\name` | `/resources/name` |

Unrecognized names are ignored. MIME types are derived from file extensions.
Use `WebPart.ofAssemblyWith chooser assembly` when the manifest layout needs explicit
`HttpResource` mappings. Resource URLs in nested apps must respect their route prefix.

Sources: [HttpResource.fs](../src/Aardvark.UI/Core/HttpResource.fs),
[HTTP extensions](../src/Aardvark.UI/Core/HttpBackend.fs).

## 3D Rendering

A render control accepts an adaptive camera and an `Aardvark.UI.ISg<'msg>` scene, not an
HTML child list or a runtime argument. The runtime is supplied at hosting time.
A controller view requires a generated adaptive controller state:

```fsharp
module SceneView

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive

type Message = Camera of FreeFlyController.Message | Picked of V3d

let update (state : CameraControllerState) (message : Message) =
    match message with
    | Camera message -> FreeFlyController.update state message
    | Picked _ -> state

let view (state : AdaptiveCameraControllerState) : DomNode<Message> =
    let scene =
        Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.requirePicking
        |> Sg.withEvents [Sg.onClick Picked]

    let frustum = AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
    FreeFlyController.controlledControl state Camera frustum
        (AttributeMap.ofList [
            style "width: 100%; height: 100%"
            RenderAttribute.samples 4
            RenderAttribute.showFps true
        ])
        scene
```

The enclosing app stores `CameraControllerState` in its immutable model, maps camera
messages in `update`, and passes the adaptive counterpart to `view`. See
[controller integration](PRIMITIVES.md#camera-controllers) and the
[render-control example](../src/Examples/02%20-%20RenderControl/App.fs).

- `Sg.withEvents` attaches picking callbacks. `Sg.onClick` receives a world-space `V3d`;
  lower-level callbacks receive `SceneHit`. `SceneHit.globalPosition` / `localPosition`
  are computed from the hit ray and distance; do not assume it contains a normal or object field.
- Adapt ordinary `Aardvark.SceneGraph.ISg` scenes with `Sg.noEvents` once if necessary.
  UI scene constructors already return typed scenes; adapting them again causes a warning.
- `Incremental.renderControlWithClientValues` builds per-client scenes from
  `RenderClientValues`. Its `size`, `time`, `viewTrafo`, `projTrafo`, `session`, and `samples`
  are adaptive; `runtime` and `signature` are also available.
- `RenderClientInfo` is a rendered-frame snapshot used by render events.
  `RenderState` contains `viewTrafo` and `projTrafo`.
- `RenderCommand<'msg>` wraps `Aardvark.SceneGraph.RenderCommand`. Combine commands using
  `RenderCommand<'msg>.Ordered` and pass the resulting **command**, not a bare list, to `Sg.execute`.
- Rendering tasks, JPEG compression, and mapped-frame transfer are implementation details
  of the core render server. Prefer render-control attributes over calling internal compression helpers.

Sources: [typed scene graphs and events](../src/Aardvark.UI/SceneGraph/SgFSharp.fs),
[SceneHit](../src/Aardvark.UI/SceneGraph/Core.fs),
[render client types](../src/Aardvark.UI/RenderServer/RenderClientInfo.fs),
[render commands](../src/Aardvark.UI/RenderControl/RenderCommand.fs),
[render server](../src/Aardvark.UI/RenderServer/RenderServer.fs).

### Resource Lifetime

`ConcreteScene` owns the render-control task: it compiles on first acquisition for a
scene/framebuffer signature and disposes after the last release and
`Config.sceneDestroyTimeout`. A task compiled eagerly in view code is not automatically
owned by that scene. Dispose it explicitly, or tie its creation and disposal to an owning
adaptive resource. Lifetime propagation through framebuffer/texture outputs, such as
those from `RenderTask.renderToColor`, does not transfer ownership of the supplied task.

This distinction matters for offscreen passes. View code can run again for routing,
reconnects, and docking changes. Compiling a render-to-texture task there without an owner
creates another task on every invocation; GPU and committed memory then grow across page
loads. Prefer scene-graph composition where possible. Otherwise create a dedicated
`AdaptiveResource` whose `Create` compiles the task and acquires its output, and whose
`Destroy` releases the output before disposing the task. Supplying that resource as a
scene texture lets the render-control acquisition own the offscreen pass. Constructing the
resource is lazy; the task is compiled only when the render control acquires it.

```fsharp
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Semantics
open FSharp.Data.Adaptive

let offscreen
    (runtime : IRuntime)
    (signature : IFramebufferSignature)
    (size : aval<V2i>)
    (scene : ISg<'msg>)
    : IAdaptiveResource<ITexture> =

    let mutable state : option<IRenderTask * IAdaptiveResource<IBackendTexture>> = None

    { new AdaptiveResource<ITexture>() with
        override _.Create() =
            let task = runtime.CompileRender(signature, scene)
            try
                let color = task |> RenderTask.renderToColor size
                color.Acquire()
                state <- Some (task, color)
            with _ ->
                task.Dispose()
                reraise()

        override _.Destroy() =
            match state with
            | Some (task, color) ->
                state <- None
                try color.Release()
                finally task.Dispose()
            | None ->
                ()

        override _.Compute(token, renderToken) =
            match state with
            | Some (_, color) ->
                color.GetValue(token, renderToken) :> ITexture
            | None ->
                failwith "offscreen resource is not acquired"
    } :> IAdaptiveResource<ITexture>

let renderControl camera attributes scene =
    Incremental.renderControlWithClientValues camera attributes (fun client ->
        let color =
            offscreen client.runtime client.signature client.size scene
            :> aval<ITexture>

        Sg.fullScreenQuad
        |> Sg.texture "SceneColor" color
    )
```

When an offscreen pass must match the actual client or screenshot dimensions, build the
scene with `Incremental.renderControlWithClientValues` as above and use its adaptive `size`
and `signature`. A size stored only in application state may describe the browser viewport
but not a larger server-side screenshot.

`AdaptiveResource.map2` has a static `aval<_>` return type, but its result implements
`IAdaptiveResource` whenever either input is a resource. The `Acquire()` and `Release()`
extensions on `IAdaptiveValue` dispatch to that runtime interface, so resource lifetimes
still propagate. With two ordinary adaptive values, the result remains an ordinary value.

Use `AdaptiveResource.map` for resource-preserving projections, such as selecting color
or depth from a resource result. It forwards acquisition/release to the input resource;
custom projection wrappers are unnecessary.

Sources: [scene task ownership](../src/Aardvark.UI/RenderServer/Scene.fs),
[per-client scene values](../src/Aardvark.UI/Frontend/Tags.fs),
[AdaptiveResource interfaces, extensions, and combinators](https://github.com/aardvark-platform/aardvark.rendering/blob/master/src/Aardvark.Rendering/Resources/Adaptive/AdaptiveResource.fs),
[render-to-output lifetime](https://github.com/aardvark-platform/aardvark.rendering/blob/master/src/Aardvark.Rendering/RenderTasks/RenderToExtensions.fs).

## CEF Integration (Windows)

The Windows integration is based on **CefSharp**, with separate packages/namespaces:

| Package | API / purpose |
|---------|---------------|
| `Aardvark.Cef.WinForms` | `AardvarkCef.Init`, `CreateBrowser(address)`, and `AardvarkCefBrowser` control |
| `Aardvark.Cef.OffScreen` | `AardvarkCef.Init`, `CreateBrowser(runtime, size, mipMap, ...)`; renders browser content into a texture |
| `Aardvark.Cef.Process` | Custom subprocess binaries needed for mapped render-control frame transfer |

A WinForms window for an already-running local server (call from an STA entry point):

```fsharp
module CefWindow

open System.Windows.Forms
open Aardvark.Cef.WinForms

let run (address : string) =
    use settings = AardvarkCef.DefaultSettings
    settings.IgnoreCertificateErrors <- false
    use cef = AardvarkCef.Init(settings)
    use form = new Form(Width = 1024, Height = 768, Text = "Aardvark")
    use browser = AardvarkCef.CreateBrowser(address)
    browser.Dock <- DockStyle.Fill
    form.Controls.Add(browser)
    Application.Run(form)
```

Keep the initialization disposable alive until after browsers/forms are disposed;
it calls `AardvarkCef.Shutdown`. Defaults otherwise ignore certificate errors, so set
`IgnoreCertificateErrors` explicitly for content requiring certificate validation.
The hosting app remains responsible for server/runtime lifetime.

Mapped transfer requires the `Aardvark.Cef.Process` package at deployment; obtain it via
Paket. Aardium is a separate Electron-based client, not this CefSharp integration.

Sources: [WinForms initialization](../src/Aardvark.Cef.WinForms/AardvarkCef.fs),
[browser control](../src/Aardvark.Cef.WinForms/Browser.fs),
[offscreen initialization](../src/Aardvark.Cef.OffScreen/AardvarkCef.fs).
Examples: [WinForms inputs](../src/Examples/16%20-%20Inputs/Program.fs),
[offscreen texture](../src/Examples/19%20-%20RenderToTexture%20%28CEF%29/Program.fs).

## See Also

- [UI.md](UI.md#render-controls) — constructors, render attributes, and lifecycle events
- [ARCHITECTURE.md](ARCHITECTURE.md#application-lifetime) — app ownership and messages
- [ADVANCED.md](ADVANCED.md) — backend-independent handlers and nested routing
