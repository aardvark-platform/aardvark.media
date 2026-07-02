open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Niobe

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.Init()

    let useVulkan = false

    let runtime, disposable =
        if useVulkan then
            let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(true)
            app.Runtime :> IRuntime, app :> IDisposable
        else
            let app = new OpenGlApplication()
            app.Runtime :> IRuntime, app :> IDisposable
    use _ = disposable

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' runtime false mapp
        WebPart.ofAssembly typeof<Aardvark.UI.Primitives.EmbeddedResources>.Assembly
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
