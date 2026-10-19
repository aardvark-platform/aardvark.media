# Advanced Patterns

Focused extension points for JavaScript, request-dependent content, and hosting.
Use [UI.md](UI.md) for ordinary DOM/event construction and
[RENDERING.md](RENDERING.md) for basic server setup.

## Channels and JavaScript Events

`Aardvark.UI.Channel` pushes server-side adaptive values to the browser. It is not
`System.Threading.Channels.Channel<T>` and has no generic writer/reader pair.
Use `.Channel` on an `aval` or `aset`, then attach it with `onBoot'`.
Client-to-server communication goes through DOM events or `aardvark.processEvent`.

A two-way text editor:

```fsharp
module TextEditor

open Aardvark.UI
open FSharp.Data.Adaptive

type Message = SetText of string

let view (value : aval<string>) : DomNode<Message> =
    input [onInput SetText]
    |> onBoot' ["serverValue", value.Channel] """
        const element = document.getElementById('__ID__');
        serverValue.onmessage = function(value) {
            element.value = value;
        };
    """
```

The supplied channel name becomes a JavaScript variable in boot code. Its `onmessage`
receives the decoded value, not a WebSocket `MessageEvent`. An `aval` channel sends its
current value and subsequent changed values; an `aset` channel sends serialized set deltas.
Setting the input value does not itself dispatch an input event; user edits emit `SetText`.

For custom client events, register the corresponding `onEvent` / `onEvent'` handler on the
same node. Use `aardvark.processEvent('__ID__', 'eventName', argument)` from its boot code;
parse the serialized arguments on the F# side. Do not assume arbitrary JSON message tags
are automatically converted into F# union cases.

`Channel.GetReader()` creates a non-generic `ChannelReader`. Custom implementations must
compute serialized messages in `ComputeMessages : AdaptiveToken -> string list` and release
owned resources in `Release`. Prefer built-in adaptive channels unless a custom wire format
or incremental collection protocol is actually required.

Sources: [Channel.fs](../src/Aardvark.UI/Dom/Channel.fs),
[onBoot' implementation](../src/Aardvark.UI/Frontend/Tags.fs).
Example: [D3 integration](../src/Examples/03%20-%20D3Integration/App.fs).

## JavaScript Dependencies and Cleanup

Attach `Reference` lists with `require`, initialize a component with `onBoot`, and release
its listeners/plugins in `onShutdown`. These are node wrappers. See
[GoldenLayout's view](../src/Aardvark.UI.Primitives/Golden/GoldenLayout.fs) for dependency
loading, layout creation in boot code, and layout destruction during shutdown.

jQuery is already bundled with the frontend. For additional libraries, provide their
script/style references and serve their URLs. `require Html.semui` supplies Fomantic-UI
references from `Aardvark.UI.Primitives`; the host must serve the primitives assembly.
Manually assembled Fomantic widgets need plugin initialization. Built-in components may
already attach it—check their implementation before adding another boot handler.

## Request-Dependent Pages

`page` / `DomNode.Page` takes `IHttpRequest -> DomNode<'msg>`. Read a single query value
with `QueryParam`; inspect `QueryParams` when multiple or zero values matter.

```fsharp
module RequestedPage

open Aardvark.UI

let view : DomNode<unit> =
    page (fun request ->
        let name = request.QueryParam "name" |> Option.defaultValue "World"
        div [] [text $"Hello {name}"]
    )
```

For panel pages, `pages` maps the `page` query parameter to `Pages.Page id`; absence maps
to `Pages.Body`. GoldenLayout uses this pattern to render panel documents.
A request chooses page content; it does not provide independent per-client application
state. `MutableApp` remains shared among its connected clients.

Sources: [IHttpRequest](../src/Aardvark.UI/Core/HttpBackend.fs),
[page helpers](../src/Aardvark.UI/Frontend/Tags.fs),
[GoldenLayout example](../src/Examples/13%20-%20GoldenLayout/App.fs).

## Backend-Independent HTTP

Backend wrappers are sufficient when hosting with just one backend. Library code that
must work with either uses `IHttpBackend<'HttpContext, 'HttpHandler>` from `Aardvark.UI`:

```fsharp
module PortableHttp

open Aardvark.UI

let greeting (http : IHttpBackend<'context, 'handler>) : 'handler =
    http.compose (http.route "/hello") (
        http.request (fun request ->
            request.QueryParam "name"
            |> Option.defaultValue "World"
            |> sprintf "Hello %s"
            |> http.text
        )
    )
```

Pass `Aardvark.UI.Giraffe.HttpBackend.Instance` or
`Aardvark.UI.Suave.HttpBackend.Instance` to create a native handler.
The core `MutableApp.toWebPart http runtime app` also takes a backend explicitly;
backend-specific wrappers supply it for you.

`IHttpRequest` exposes `Path`, `Body : Stream`, `Method`, `Header`, `Headers`, `QueryParam`,
and `QueryParams : Map<string, string list>`. `BodyData` / `BodyUtf8` are asynchronous
extensions. Prefer `http.bindBody`, `bindJson`, or `mapJson` over synchronously reading an
ASP.NET Core request stream. The interface also supports routing, response types, files,
resource assemblies, and WebSocket handshakes; consult its source for exact overloads.

Sources: [HTTP interface and extensions](../src/Aardvark.UI/Core/HttpBackend.fs),
[Giraffe adapter](../src/Aardvark.UI.Giraffe/HttpBackend.fs),
[Suave adapter](../src/Aardvark.UI.Suave/HttpBackend.fs).

## Multi-App Hosting

Use the backend's prefix-rewriting route helper, not a path filter that leaves the
prefix intact. A Giraffe route composition for two independently started apps:

```fsharp
module MultipleApps

open Giraffe
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Giraffe

let routes (runtime : IRuntime)
           (viewer : MutableApp<'viewer, 'adaptiveViewer, 'viewerMsg>)
           (editor : MutableApp<'editor, 'adaptiveEditor, 'editorMsg>) : WebPart =
    choose [
        subRoute "/viewer" (MutableApp.toWebPart runtime viewer)
        subRoute "/editor" (MutableApp.toWebPart runtime editor)
    ]
```

Mount each app's application/primitive resources under the matching prefix as needed.
For Suave, `Suave.Filters.pathStarts` alone is not equivalent; use the backend's
`HttpBackend.Instance.subRoute` when composing a prefixed app handler.

The owner must keep both apps and the shared rendering application alive. Give a shared
server an explicitly owned lifetime token when it should outlive either individual app;
cancel it, dispose the apps, await server shutdown, then release the rendering application.
Do not claim that sharing a runtime requires one runtime per app.

For cross-app messages, route them through the destination app's `Update` method and a
well-defined message translation. If separate changeable adaptive state is genuinely
required, give it an explicit owner and mutate it inside `transact`. Do not bypass both
apps' update loops by writing their immutable models directly.

Example: [Giraffe subRoute hosting](../src/Scratch/21%20-%20Giraffe/Program.fs).

## Scene Graph Extensions

- Use `Sg.dynamic` for adaptive typed scenes and `Sg.map` to lift scene messages.
- Use `Sg.withEvents` / `Sg.withGlobalEvents` for interaction; do not simulate scene hits
  by attaching HTML mouse handlers with incompatible signatures.
- Use `RenderCommand` composition for ordered rendering and clears; see
  [3D rendering](RENDERING.md#3d-rendering).
- Custom semantics belong to the scene graph API. Start from
  [UI scene graph types](../src/Aardvark.UI/SceneGraph/Core.fs) and
  [semantics](../src/Aardvark.UI/SceneGraph/Semantics.fs), rather than inventing members on `ISg`.
- Let the render server own scene compilation and task disposal unless extending that
  pipeline explicitly. Caching a compiled render task as an `IRenderObject` is not valid.

For Aardium client setup, see [example entry points](../src/Examples/02%20-%20RenderControl/Program.fs)
and the [Aardium repository](https://github.com/aardvark-community/aardium).
It is independent of the server backend and CefSharp packages.

## See Also

- [ARCHITECTURE.md](ARCHITECTURE.md) — model updates, background commands, and ownership
- [UI.md](UI.md) — attributes, event callbacks, and node wrappers
- [PRIMITIVES.md](PRIMITIVES.md) — built-in components and docking
