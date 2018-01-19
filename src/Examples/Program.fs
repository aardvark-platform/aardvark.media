open Examples

open System
open System.Windows.Forms
open Suave
open Suave.Filters
open Suave.Operators

open Aardvark.UI
open Aardvark.Base
open Aardvark.Application.WinForms

[<EntryPoint>]
let main argv = 

    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv
    //Aardvark.Cef.Internal.Cef.init' false

    let useVulkan = false

    Ag.initialize()
    Aardvark.Init()

    let app, runtime = 
        if useVulkan then
             let app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(false) 
             app :> IDisposable, app.Runtime :> IRuntime
         else 
             let app = new OpenGlApplication()
             app :> IDisposable, app.Runtime :> IRuntime
    use app = app
    
    use form = new Form(Width = 1024, Height = 768)

    let app = MultiviewApp.app

    let mapp = app |> App.start

    WebPart.startServer 4321 [ 
        Suave.Filters.prefix "/simple" >=> MutableApp.toWebPart' runtime true mapp
        Suave.Filters.prefix "/complex" >=> MutableApp.toWebPart' runtime true mapp
        Suave.Files.browseHome
    ] 

    //Console.ReadLine() |> ignore
    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/simple/"
    ctrl.ShowDevTools()

    Application.Run form
    0 
