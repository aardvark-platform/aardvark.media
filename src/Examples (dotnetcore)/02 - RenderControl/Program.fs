open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI

open Aardium
open RenderControl
open Aardvark.UI.Giraffe


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
            let app = new OpenGlApplication()
            app.Runtime :> IRuntime, app :> IDisposable
    use __ = disposable
    
    let app = App.app

    let instance = 
        app |> App.start

    Server.startServer "http://*:4321" Threading.CancellationToken.None  (
        MutableApp.toWebPart runtime instance
    ) |> ignore
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }


    0 
