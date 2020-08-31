open System

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI

open Suave
open Suave.WebPart
open Aardium
open RenderControl

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

    WebPart.startServer 8080 [ 
        MutableApp.toWebPart' runtime true instance
        Suave.Files.browseHome
    ] |> ignore
    

    Console.ReadLine() |> ignore

    //Aardium.run {
    //    url "http://localhost:4321/"
    //    width 1024
    //    height 768
    //    debug true
    //}
    0 
