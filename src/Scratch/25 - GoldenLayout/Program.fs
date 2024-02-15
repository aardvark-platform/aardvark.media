open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Primitives.Golden
open Aardium
open Inc

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()
    
    use app = new OpenGlApplication()
    let instance = App.app |> App.start

    // use can use whatever suave server to start you mutable app. 
    // startServerLocalhost is one of the convinience functions which sets up 
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer. 
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.Service/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    Suave.WebPart.startServerLocalhost 4321 [ 
        Aardvark.UI.Primitives.Resources.WebPart
        MutableApp.toWebPart' app.Runtime false instance
        GoldenLayout.WebPart.suave
        Suave.Files.browseHome
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] %s{msg}"))
    }

    //use ctrl = new AardvarkCefBrowser()
    //ctrl.Dock <- DockStyle.Fill
    //form.Controls.Add ctrl
    //ctrl.StartUrl <- "http://localhost:4321/"
    //ctrl.ShowDevTools()
    //form.Text <- "Examples"
    //form.Icon <- Icons.aardvark 

    //Application.Run form
    0 
