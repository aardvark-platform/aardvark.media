open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Giraffe
open Aardium

open RenderControl

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()

    Config.defaultDocumentTitle <- "02 - Render Control"

    // media apps require a runtime, which serves as renderer for your render controls.
    // you can use OpenGL or VulkanApplication.
    let useVulkan = false

    let runtime, disposable =
        if useVulkan then
            let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
            app.Runtime :> IRuntime, app :> IDisposable
        else
            let app = new OpenGlApplication()
            app.Runtime :> IRuntime, app :> IDisposable
    use _ = disposable

    use app = App.app.start()
    Report.Verbosity <- 3

    Server.startLocalhost 4321 app.CancellationToken [
        MutableApp.toWebPart runtime app
        WebPart.ofType<Primitives.EmbeddedResources>
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    0