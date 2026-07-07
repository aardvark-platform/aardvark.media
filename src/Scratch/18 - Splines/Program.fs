open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open SplinesTest

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.Init()

    // media apps require a runtime, which serves as renderer for your render controls.
    // you can use OpenGL or VulkanApplication.
    let useVulkan = false

    let runtime, disposable =
        if useVulkan then
            let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
            app.Runtime :> IRuntime, app :> IDisposable
        else
            let app = new Aardvark.Application.Slim.OpenGlApplication()
            app.Runtime :> IRuntime, app :> IDisposable
    use _ = disposable

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart runtime mapp
        WebPart.ofAssembly typeof<Primitives.EmbeddedResources>.Assembly
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        title "18 - Splines"
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    0