open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
#if WINDOWS
open Aardvark.Cef.WinForms
open System.Windows.Forms
#endif
open Input

[<EntryPoint; STAThread>]
let main argv =
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
    use _ = disposable

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' runtime false mapp
        Aardvark.UI.Primitives.Resources.toWebPart HttpBackend.Instance
    ] |> ignore

#if WINDOWS
    let useCef = false

    if useCef then
        use _ = AardvarkCef.Init()

        use form = new Form()
        form.Width <- 800
        form.Height <- 600

        use browser = AardvarkCef.CreateBrowser("http://localhost:4321/")
        form.Controls.Add browser
        form.Text <- "Examples"

        Application.Run form
        browser.CloseDevTools()
        form.Controls.Remove browser
    else
#else
    if true then
#endif
        Aardium.Init()

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
