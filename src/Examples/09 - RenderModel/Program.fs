open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
#if WINDOWS || NETFRAMEWORK
open Aardvark.Cef.WinForms
open System.Windows.Forms
#endif
open Aardium

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
            let app = new OpenGlApplication()
            app.Runtime :> IRuntime, app :> IDisposable
    use _ = disposable

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        WebPart.ofType<Primitives.EmbeddedResources>
        MutableApp.toWebPart' runtime false mapp
    ] |> ignore

#if WINDOWS || NETFRAMEWORK
    let useCef = false

    if useCef then
        use _ = AardvarkCef.Init()

        use form = new Form()
        form.Width <- 1000
        form.Height <- 800

        use browser = AardvarkCef.CreateBrowser("http://localhost:4321/")
        form.Controls.Add browser
        form.Text <- "09 - RenderModel"

        Application.Run form
        browser.CloseDevTools()
        form.Controls.Remove browser
    else
#else
    if true then
#endif
        Aardium.run {
            url "http://localhost:4321/"
            width 1000
            height 800
            title "09 - RenderModel"
#if DEBUG
            debug true
            log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
            debug false
#endif
        }
    0 
