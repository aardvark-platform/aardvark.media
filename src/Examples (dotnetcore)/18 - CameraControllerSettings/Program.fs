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
    let mutable w = 0
    let mutable c = 0
    System.Threading.ThreadPool.GetMaxThreads(&w,&c)
    printfn "%A %A " w c
    System.Threading.ThreadPool.GetMinThreads(&w,&c)
    printfn "%A %A " w c
    System.Threading.ThreadPool.SetMinThreads(1,1) |> printfn "oida: %A"
    System.Threading.ThreadPool.SetMaxThreads(12,12) |> printfn "oida: %A"
    Ag.initialize()
    Aardvark.Init()
    Aardium.init()

    // media apps require a runtime, which serves as renderer for your render controls.
    // you can use OpenGL or VulkanApplication.
    let useVulkan = true

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



    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' runtime false instance
        Suave.Files.browseHome
    ]  
    

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
