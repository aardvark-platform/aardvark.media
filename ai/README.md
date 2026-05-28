# Aardvark.Media AI Documentation Index

## Task-Based Lookup

| Task | Document | Size |
|------|----------|------|
| Build interactive UI elements | [UI.md](UI.md) | ~21 KB |
| Configure 3D rendering | [RENDERING.md](RENDERING.md) | ~16 KB |
| Add camera controls or layout | [PRIMITIVES.md](PRIMITIVES.md) | ~20 KB |
| Structure application state | [ARCHITECTURE.md](ARCHITECTURE.md) | ~15 KB |
| Custom components or JS interop | [ADVANCED.md](ADVANCED.md) | ~12 KB |
| Set up web server | [RENDERING.md](RENDERING.md#server-setup) | ~16 KB |
| Create animation | [PRIMITIVES.md](PRIMITIVES.md#animation-system) | ~20 KB |
| Handle events | [UI.md](UI.md#event-handling) | ~21 KB |
| Manage background tasks | [ARCHITECTURE.md](ARCHITECTURE.md#threadpool) | ~15 KB |

## Type-to-Document Mapping

| Type | Document |
|------|----------|
| `App<'model,'mmodel,'msg>` | [ARCHITECTURE.md](ARCHITECTURE.md#app-type) |
| `DomNode<'msg>` | [UI.md](UI.md#domnode) |
| `FreeFlyController` | [PRIMITIVES.md](PRIMITIVES.md#freeflycontroller) |
| `ArcBallController` | [PRIMITIVES.md](PRIMITIVES.md#arcballcontroller) |
| `OrbitController` | [PRIMITIVES.md](PRIMITIVES.md#orbitcontroller) |
| `MutableApp` | [ARCHITECTURE.md](ARCHITECTURE.md#app-initialization) |
| `Unpersist` | [ARCHITECTURE.md](ARCHITECTURE.md#unpersist) |
| `RenderControlConfig` | [UI.md](UI.md#rendercontrol---3d-rendering) |
| `RenderTask` | [RENDERING.md](RENDERING.md#rendertasks) |
| `Attribute<'msg>` | [UI.md](UI.md#attributes-module) |
| `AttributeMap<'msg>` | [UI.md](UI.md#attributemap-operations) |
| `Event<'msg>` | [UI.md](UI.md#event-handling) |
| `ThreadPool<'msg>` | [ARCHITECTURE.md](ARCHITECTURE.md#threadpool) |
| `CameraControllerState` | [PRIMITIVES.md](PRIMITIVES.md#cameramodel) |
| `Animator<'Model>` | [PRIMITIVES.md](PRIMITIVES.md#animator) |
| `IAnimation<'Model,'Value>` | [PRIMITIVES.md](PRIMITIVES.md#animation-system) |
| `GoldenLayout` | [PRIMITIVES.md](PRIMITIVES.md#goldenlayout) |
| `ColorPicker` | [PRIMITIVES.md](PRIMITIVES.md#colorpicker) |
| `ChannelReader<'msg>` | [ADVANCED.md](ADVANCED.md#datachannel-pattern) |
| `Browser` (CEF) | [RENDERING.md](RENDERING.md#cef-integration-windows) |
| `WebPart` | [RENDERING.md](RENDERING.md#suaveserver-integration) |

## Document Overview

- **UI.md**: DomNode construction, tags, attributes, events, incremental DOM, RenderControl
- **RENDERING.md**: RenderTask pipeline, Aardvark.Service, server configuration, CEF integration
- **PRIMITIVES.md**: Camera controllers, animation system, GoldenLayout, ColorPicker, dialogs
- **ARCHITECTURE.md**: ELM pattern, App type hierarchy, MutableApp, Unpersist, ThreadPool model
- **ADVANCED.md**: DataChannel communication, JS interop, multi-app scenarios, custom scene graphs

## See Also

- [AGENTS.md](../AGENTS.md) - Build commands, dependency management, project structure
- [.claude/CLAUDE.md](../.claude/CLAUDE.md) - Project-specific instructions
