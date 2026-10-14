# AI Documentation

Guidance for the current 5.7.x APIs. Source definitions and linked examples are the
reference for exact signatures; this index is not a generated API inventory.
For build, dependency, and code-generation rules, see [AGENTS.md](../AGENTS.md).

## Task-Based Lookup

| Task | Reference |
|------|-----------|
| Define models, update functions, and apps | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Build DOM nodes, adaptive attributes, and events | [UI.md](UI.md) |
| Host an app with Giraffe or Suave | [RENDERING.md](RENDERING.md#server-setup) |
| Render and interact with a 3D scene | [RENDERING.md](RENDERING.md#3d-rendering) |
| Use camera controllers, animations, or UI components | [PRIMITIVES.md](PRIMITIVES.md) |
| Open native file and folder dialogs | [PRIMITIVES.md](PRIMITIVES.md#dialogs) |
| Integrate JavaScript and adaptive channels | [ADVANCED.md](ADVANCED.md#channels-and-javascript-events) |
| Serve request-dependent pages or host multiple apps | [ADVANCED.md](ADVANCED.md#request-dependent-pages) |
| Embed a Windows browser with CefSharp | [RENDERING.md](RENDERING.md#cef-integration-windows) |

## Type-to-Document Mapping

| Type / module | Reference |
|---------------|-----------|
| `App<'model, 'mmodel, 'msg>`, `Unpersist` | [Application structure](ARCHITECTURE.md#application-structure) |
| `MutableApp<'model, 'mmodel, 'msg>` | [Application lifetime](ARCHITECTURE.md#application-lifetime) |
| `ThreadPool<'msg>` | [Background messages](ARCHITECTURE.md#background-messages) |
| `DomNode<'msg>` | [DOM construction](UI.md#dom-construction) |
| `AttributeMap<'msg>`, `AttributeValue<'msg>` | [Attributes](UI.md#attributes) |
| `Event<'msg>` | [Events](UI.md#events) |
| `RenderControlConfig`, `RenderAttribute` | [Render controls](UI.md#render-controls) |
| `RenderClientInfo`, `RenderClientValues`, `RenderState` | [3D rendering](RENDERING.md#3d-rendering) |
| `IHttpBackend`, `IHttpRequest` | [HTTP backends](ADVANCED.md#backend-independent-http) |
| `Channel`, `ChannelReader` | [Channels](ADVANCED.md#channels-and-javascript-events) |
| `CameraControllerState`, `OrbitState` | [Camera controllers](PRIMITIVES.md#camera-controllers) |
| `Animator`, `IAnimation` | [Animation system](PRIMITIVES.md#animation-system) |
| `GoldenLayout`, `ColorPicker` | [UI components](PRIMITIVES.md#ui-components) |
| `DialogConfig`, `Dialog` | [Dialogs](PRIMITIVES.md#dialogs) |
| `AardvarkCef`, `AardvarkCefBrowser` | [CEF integration](RENDERING.md#cef-integration-windows) |

## Migrating Older Examples

See the [5.7 changelog](https://github.com/aardvark-platform/aardvark.media/wiki/Aardvark-Media-5.7-Changelog)
and [release notes](../RELEASE_NOTES.md) for later fixes and additions.

| Older API | Current API |
|-----------|-------------|
| `Aardvark.Service`, `Aardvark.Service.Giraffe` | Backend-independent internals in `Aardvark.UI`; hosting in `Aardvark.UI.Suave` / `Aardvark.UI.Giraffe` |
| `WebPart.startServerLocalhost` | Backend `Server.startLocalhost port cancellationToken content` |
| `Reflection.assemblyWebPart` | Backend `WebPart.ofAssembly`, `WebPart.ofType`, or `WebPart.ofAssemblyWith` |
| `MutableApp<'model, 'msg>` record | Disposable `MutableApp<'model, 'mmodel, 'msg>` class with PascalCase members |
| `App.startAndGetState`, `shutdown()` | `MutableApp.MutableModel`, `Dispose()` / `use` |
| `Request` | `IHttpRequest`; use `QueryParam` for a single query value |
| `ClientInfo`, `ClientValues`, `ClientState` | `RenderClientInfo`, `RenderClientValues`, `RenderState` |
| `PathUtils.ofUnixStyle` | `Path.ofUnixStyle` in `Aardvark.UI` |
| Generic `onEvent "onRendered" ...` | `onAfterRender` / `onBeforeRender` and their sequence-returning variants |
| Separate open/save dialog helpers | `Aardvark.UI.Primitives.Dialog` |
| Old CEF browser wrappers | CefSharp-based `Aardvark.Cef.WinForms` / `Aardvark.Cef.OffScreen`; `Aardvark.Cef.Process` for mapped rendering |

Use `RenderAttribute` rather than spelling render-control data attributes by hand.
The bundled frontend uses jQuery 4; check plugin compatibility when integrating other libraries.
