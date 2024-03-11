open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardium
open NotificationsExample

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.init()

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
    use __ = disposable

    let app = App.app

    let instance =
        app |> App.start

    Suave.WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart runtime instance
        Suave.Reflection.assemblyWebPart typeof<Primitives.EmbeddedResources>.Assembly
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        title "28 - Notifications"
        debug true
    }

    0