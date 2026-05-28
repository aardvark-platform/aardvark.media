# RENDERING.md

Reference for Aardvark.Service, web server integration, and rendering infrastructure.

---

## Aardvark.Service Core

### Server Module

Core server functionality for hosting Aardvark applications over HTTP.

```fsharp
open Aardvark.Service

// Start server on localhost
let app = // your MutableApp
WebPart.startServerLocalhost 4321 [
    MutableApp.toWebPart runtime app
]
```

**Key Functions:**
- `Server.start`: Start server with custom configuration
- `Server.stop`: Gracefully stop server
- `WebPart.startServerLocalhost`: Quick localhost setup with port

### SuaveServer Integration

Suave-based HTTP server for Aardvark applications.

```fsharp
open Suave
open Aardvark.Service

let server =
    choose [
        MutableApp.toWebPart runtime app
        Reflection.assemblyWebPart typeof<MyApp>.Assembly
        RequestErrors.NOT_FOUND "Not found"
    ]

startWebServer defaultConfig server
```

**WebPart Functions:**
- `MutableApp.toWebPart`: Convert MutableApp to Suave WebPart
- `MutableApp.toWebPart'`: Extended version with custom options
- `Reflection.assemblyWebPart`: Serve embedded resources from assembly

### RenderTasks

Manages rendering tasks and coordinates scene updates.

```fsharp
type RenderTask = {
    runtime: IRuntime
    sceneGraph: ISg
    sizes: IMod<V2i>
}
```

**Responsibilities:**
- Scene graph compilation
- Frame rendering coordination
- Resource lifecycle management
- Performance monitoring

### Utilities and Extensions

**SuaveExtensions:**
- Request parsing helpers
- Response formatting
- WebSocket support
- Session management

**Common Utilities:**
- Resource path resolution
- MIME type detection
- Error handling patterns

---

## Server Setup

### Basic Suave Server

```fsharp
open Aardvark.Service
open Suave
open Suave.Filters
open Suave.Operators

let runtime = // IRuntime instance
let app = // MutableApp instance

let config =
    { defaultConfig with
        bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 4321 ] }

let server =
    choose [
        path "/" >=> Files.browseFileHome "index.html"
        MutableApp.toWebPart runtime app
        Reflection.assemblyWebPart typeof<App>.Assembly
        RequestErrors.NOT_FOUND "Page not found"
    ]

startWebServer config server
```

### Port Configuration

```fsharp
// Single port
WebPart.startServerLocalhost 4321 [webpart]

// Multiple bindings
let config =
    { defaultConfig with
        bindings = [
            HttpBinding.createSimple HTTP "0.0.0.0" 4321
            HttpBinding.createSimple HTTP "127.0.0.1" 8080
        ] }
```

### Embedded Resources

```fsharp
// Serve all embedded resources from assembly
Reflection.assemblyWebPart typeof<MyApp>.Assembly

// Resources must be marked as EmbeddedResource in .fsproj:
// <EmbeddedResource Include="resources/**/*" />
```

Pattern: Place resources in `resources/` folder, access via `/resources/file.js`

---

## Giraffe Integration

### Aardvark.Service.Giraffe

Giraffe-based alternative to Suave for modern ASP.NET Core integration.

```fsharp
open Giraffe
open Aardvark.Service.Giraffe

let webApp runtime app =
    choose [
        route "/" >=> htmlFile "index.html"
        MutableApp.toGiraffeHttpHandler runtime app
        GiraffeExtensions.assemblyResourceHandler typeof<App>.Assembly
        RequestErrors.NOT_FOUND "Not found"
    ]
```

### GiraffeExtensions

**Key Functions:**
- `MutableApp.toGiraffeHttpHandler`: Convert MutableApp to Giraffe handler
- `GiraffeExtensions.assemblyResourceHandler`: Serve embedded resources
- WebSocket support via `Giraffe.WebSocket`

### ASP.NET Core Setup

```fsharp
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Giraffe

let configureApp runtime app (appBuilder : IApplicationBuilder) =
    appBuilder
        .UseStaticFiles()
        .UseGiraffe(webApp runtime app)

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

WebHostBuilder()
    .UseKestrel()
    .Configure(Action<IApplicationBuilder> (configureApp runtime app))
    .ConfigureServices(configureServices)
    .Build()
    .Run()
```

### Suave vs. Giraffe

| Feature | Suave | Giraffe |
|---------|-------|---------|
| Framework | Standalone | ASP.NET Core |
| Performance | Good | Better (Kestrel) |
| Ecosystem | Suave-specific | ASP.NET Core ecosystem |
| Setup complexity | Simple | Moderate |
| WebSockets | Built-in | Via ASP.NET Core |
| Use case | Prototypes, simple apps | Production, complex apps |

---

## 3D Rendering in UI

### RenderControl

Creates interactive 3D viewport in DOM.

```fsharp
open Aardvark.UI
open Aardvark.UI.Primitives

let view (model : MModel) =
    require (Html.semui) (
        div [] [
            RenderControl.control runtime model.camera
                [
                    SceneGraph.dynamic model.scene
                    attribute "style" "width: 800px; height: 600px"
                ]
        ]
    )
```

**Properties:**
- `camera`: Camera controller (FreeFlyController, ArcBallController, etc.)
- `attributes`: Standard HTML attributes (style, class, etc.)
- Scene graph children define rendered content

### SceneEvent and SceneHit

Handle 3D interaction events.

```fsharp
type Message =
    | Click of SceneHit
    | Move of SceneHit

let update (model : Model) (msg : Message) =
    match msg with
    | Click hit ->
        printfn "Clicked at world pos: %A" hit.globalPosition
        model
    | Move hit ->
        { model with hoverPoint = Some hit.globalPosition }

// In view:
RenderControl.control runtime model.camera [
    onMouseClick (fun hit -> Click hit)
    onMouseMove (fun hit -> Move hit)
    SceneGraph.dynamic model.scene
]
```

**SceneHit Fields:**
- `globalPosition`: World-space 3D coordinate
- `localPosition`: Object-space coordinate
- `normal`: Surface normal at hit point
- `distance`: Distance from camera
- `object`: ISg node that was hit

### Camera Integration

```fsharp
open Aardvark.UI.Primitives

// Create camera controller
let camera =
    CameraController.create CameraControllerKind.FreeFly
        (Frustum.perspective 60.0 0.1 1000.0 1.0)
        (CameraView.lookAt (V3d(10,10,10)) V3d.Zero V3d.OOI)

// Update in response to messages
type Message =
    | Camera of CameraController.Message

let update model msg =
    match msg with
    | Camera m ->
        { model with camera = CameraController.update model.camera m }

// Wire up in view
RenderControl.control runtime model.camera [
    Incremental.Scene (model.scene)
] |> onCameraChange Camera
```

**Camera Controllers:**
- `FreeFly`: First-person navigation
- `ArcBall`: Orbit around target
- `Turntable`: Limited orbit (no roll)
- `Walk`: Ground-constrained movement

### Sg Module in UI

Scene graph construction for UI rendering.

```fsharp
open Aardvark.SceneGraph
open Aardvark.UI

let scene =
    Sg.box (Mod.constant C4b.Red) (Mod.constant Box3d.Unit)
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.constantColor C4f.Red
        do! DefaultSurfaces.simpleLighting
    }
    |> Sg.trafo (Mod.map Trafo3d.Translation model.position)

// In view:
RenderControl.control runtime model.camera [
    SceneGraph.dynamic scene
]
```

**Common Patterns:**
- `SceneGraph.dynamic`: Adaptive scene graph from IMod<ISg>
- `Sg.dynamic`: Create adaptive scene nodes
- Combine with Incremental for efficient updates

---

## Resource Serving

### Embedded Resources Pattern

```fsharp
// In .fsproj:
<ItemGroup>
    <EmbeddedResource Include="resources/**/*" />
</ItemGroup>

// In server setup:
Reflection.assemblyWebPart typeof<MyApp>.Assembly
```

**Access Pattern:**
- File at `resources/scripts/app.js` → URL `/resources/scripts/app.js`
- Maintains directory structure
- Loaded from assembly at runtime

### TurboJpeg Compression

Fast JPEG encoding for streaming rendered frames.

```fsharp
open Aardvark.Service

// Compress image data
let compressedData =
    TurboJpeg.compress quality jpegData width height

// Common in render task pipelines
// Automatically used by RenderControl for frame streaming
```

**Use Cases:**
- Real-time frame streaming to browser
- Screenshot generation
- Video encoding pipelines

**Quality Settings:**
- `1-100`: JPEG quality (85-95 typical for real-time)
- Lower quality = smaller files, faster encoding

### SharedMemory Utilities

Inter-process memory sharing for high-performance scenarios.

```fsharp
open Aardvark.Service.SharedMemory

// Create shared memory region
use shm = new SharedMemory("MyRegion", sizeInBytes)

// Write data
shm.Write(data, offset)

// Read from another process
use shmReader = SharedMemory.Open("MyRegion")
let data = shmReader.Read(offset, length)
```

**Use Cases:**
- CEF process communication
- Large data transfer between processes
- Zero-copy frame sharing

### DownloadTools

Helpers for file download endpoints.

```fsharp
open Aardvark.Service

// Serve file for download
let downloadHandler filename contentType data =
    Writers.setMimeType contentType
    >=> Writers.setHeader "Content-Disposition" (sprintf "attachment; filename=\"%s\"" filename)
    >=> Successful.OK data

// Example usage
path "/download/scene.obj" >=> downloadHandler "scene.obj" "model/obj" objData
```

---

## CEF Integration (Windows)

### Aardvark.Cef

Chromium Embedded Framework integration for embedded browser windows.

```fsharp
open Aardvark.Cef

// Initialize CEF
Cef.init()

// Create browser
let browser = new Browser(url = "http://localhost:4321")

// Cleanup
browser.Dispose()
Cef.shutdown()
```

**Architecture:**
- Multi-process (main + renderer processes)
- GPU acceleration support
- WebGL and full HTML5 support

### Aardvark.Cef.WinForms

WinForms control for CEF browser.

```fsharp
open Aardvark.Cef.WinForms

// Create control
let control = new CefBrowserControl(url = "http://localhost:4321")

// Add to form
form.Controls.Add(control)
control.Dock <- DockStyle.Fill

// Cleanup
control.Dispose()
```

### Browser Type

Main browser instance management.

```fsharp
type Browser(url : string) =
    member x.Load(newUrl : string) : unit
    member x.Reload() : unit
    member x.ExecuteJavaScript(code : string) : unit
    member x.ShowDevTools() : unit
    interface IDisposable
```

**Key Methods:**
- `Load`: Navigate to URL
- `Reload`: Refresh current page
- `ExecuteJavaScript`: Run JS in page context
- `ShowDevTools`: Open Chrome DevTools

### Client Type

Handles browser events and callbacks.

```fsharp
type Client() =
    member x.OnLoadEnd : IEvent<LoadEndEventArgs>
    member x.OnLoadError : IEvent<LoadErrorEventArgs>
    member x.OnConsoleMessage : IEvent<ConsoleMessageEventArgs>
    interface IDisposable

let client = new Client()
client.OnLoadEnd.Add(fun args -> printfn "Page loaded: %s" args.Url)

let browser = new Browser(url, client = client)
```

**Event Types:**
- `OnLoadEnd`: Page finished loading
- `OnLoadError`: Load failed
- `OnConsoleMessage`: JavaScript console output
- `OnBeforePopup`: Popup window requested

### Process Architecture

CEF uses multi-process architecture:

```
Main Process (Your App)
├── Browser Process (CEF)
│   ├── Renderer Process (Page 1)
│   ├── Renderer Process (Page 2)
│   └── GPU Process
```

**Implications:**
- Each browser instance spawns processes
- Dispose browsers properly to avoid leaks
- SharedMemory used for efficient IPC
- GPU process handles WebGL/rendering

### Setup Example

```fsharp
open Aardvark.Cef
open Aardvark.Cef.WinForms
open System.Windows.Forms

[<EntryPoint>]
let main argv =
    Cef.init()

    use form = new Form(Width = 1024, Height = 768, Text = "Aardvark CEF")
    use browser = new CefBrowserControl(url = "http://localhost:4321")

    browser.Dock <- DockStyle.Fill
    form.Controls.Add(browser)

    Application.Run(form)

    Cef.shutdown()
    0
```

---

## Complete Setup Examples

### Suave + CEF Desktop App

```fsharp
open Aardvark.Base
open Aardvark.Service
open Aardvark.UI
open Aardvark.Cef
open Aardvark.Cef.WinForms
open Suave
open System.Windows.Forms

[<EntryPoint>]
let main argv =
    Aardvark.Init()
    Cef.init()

    use runtime = new Runtime()

    let app = // your MutableApp setup

    // Start server
    let cts = new System.Threading.CancellationTokenSource()
    let server = WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart runtime app
        Reflection.assemblyWebPart typeof<App>.Assembly
    ]

    // Create window
    use form = new Form(Width = 1280, Height = 720)
    use control = new CefBrowserControl(url = "http://localhost:4321")
    control.Dock <- DockStyle.Fill
    form.Controls.Add(control)

    Application.Run(form)

    // Cleanup
    cts.Cancel()
    Cef.shutdown()
    runtime.Dispose()
    0
```

### Giraffe Server with RenderControl

```fsharp
open Giraffe
open Aardvark.Service.Giraffe
open Aardvark.UI
open Microsoft.AspNetCore.Hosting

let view runtime (model : MModel) =
    html [] [
        head [] [
            script [ _src "/resources/aardvark.js" ] []
        ]
        body [] [
            RenderControl.control runtime model.camera [
                style "width: 100vw; height: 100vh"
                SceneGraph.dynamic model.scene
            ]
        ]
    ]

let webApp runtime app =
    choose [
        route "/" >=> (MutableApp.toGiraffeHttpHandler runtime app)
        GiraffeExtensions.assemblyResourceHandler typeof<App>.Assembly
    ]

[<EntryPoint>]
let main argv =
    use runtime = new Runtime()
    let app = // MutableApp setup

    WebHostBuilder()
        .UseKestrel()
        .Configure(fun appBuilder ->
            appBuilder.UseGiraffe(webApp runtime app) |> ignore)
        .ConfigureServices(fun services ->
            services.AddGiraffe() |> ignore)
        .Build()
        .Run()

    0
```

---

## Gotchas

### Server Port Conflicts
- Default port 4321 may be in use
- Check firewall rules on first run
- Use `netstat -ano | findstr :4321` to diagnose

### CEF Process Lifecycle
- Always call `Cef.init()` before creating browsers
- Always call `Cef.shutdown()` on exit
- Dispose all Browser instances before shutdown
- Zombie processes if cleanup skipped

### Embedded Resources Not Found
- Verify `<EmbeddedResource Include="..." />` in .fsproj
- Check Build Action in Visual Studio (must be EmbeddedResource)
- Path must match URL exactly (case-sensitive on some systems)

### RenderControl Not Updating
- Ensure scene graph uses `Mod<T>` or `IMod<T>` for reactive values
- Check that model updates trigger via `transact`
- Verify RenderControl has explicit width/height (CSS or attributes)

### Camera Controller Not Responding
- Must wire up `onCameraChange` handler
- Update loop must call `CameraController.update`
- Check that messages flow through update function

### WebSocket Connection Failures
- Ensure server supports WebSocket upgrade
- Check that RenderControl JavaScript is loaded
- Verify no proxy/firewall blocking WebSocket

### Performance Issues
- RenderControl quality setting (lower for better FPS)
- Scene complexity (use LoD for large scenes)
- TurboJpeg quality (reduce for faster streaming)
- Check browser GPU acceleration enabled

---

## Reference Table

| Component | Package | Purpose |
|-----------|---------|---------|
| Server | Aardvark.Service | Core HTTP server functionality |
| SuaveServer | Aardvark.Service | Suave integration |
| GiraffeExtensions | Aardvark.Service.Giraffe | Giraffe/ASP.NET Core integration |
| RenderControl | Aardvark.UI | 3D viewport in browser |
| TurboJpeg | Aardvark.Service | Fast JPEG compression |
| SharedMemory | Aardvark.Service | IPC memory sharing |
| Browser | Aardvark.Cef | CEF browser instance |
| CefBrowserControl | Aardvark.Cef.WinForms | WinForms CEF control |

## See Also

- [UI.md](UI.md) - RenderControl, DomNode, view construction
- [ARCHITECTURE.md](ARCHITECTURE.md) - App structure, MutableApp.toWebPart
- [PRIMITIVES.md](PRIMITIVES.md) - Camera controllers for 3D scenes
- [ADVANCED.md](ADVANCED.md) - Multi-app hosting, WebPart composition

---

*Document Size: ~16 KB*
*Last Updated: 2025-12-22*
