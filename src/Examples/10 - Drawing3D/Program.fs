(*

Thomas Ortners Drawing Example

*)

open System
open System.Windows.Forms

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.UI

open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv = 

    Xilium.CefGlue.ChromiumUtilities.unpackCef()
    Chromium.init argv

    Ag.initialize()
    Aardvark.Init()

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

    use form = new Form(Width = 1024, Height = 768)

    let app = App.app

    let instance = 
        app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' runtime false instance
        Suave.Files.browseHome
    ]  


    use ctrl = new AardvarkCefBrowser()
    ctrl.Dock <- DockStyle.Fill
    form.Controls.Add ctrl
    ctrl.StartUrl <- "http://localhost:4321/"
    ctrl.ShowDevTools()
    form.Text <- "Examples"
    form.Icon <- Icons.aardvark 

    Application.Run form
    0 
