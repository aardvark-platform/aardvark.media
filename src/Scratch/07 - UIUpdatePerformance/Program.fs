open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Inc

type AssemblyResources = AssemblyResources

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()
    use mapp = App.app app.Runtime |> App.start

    Aardvark.UI.Config.shouldTimeJsCodeGeneration <- true
    Aardvark.UI.Config.shouldTimeUIUpdate <- true
    // Aardvark.UI.Config.shouldTimeUpdate <- true
    Aardvark.UI.Config.showTimeJsAssembly <- true
    Aardvark.UI.Config.shouldPrintDOMUpdates <- true

    // use can use whatever suave server to start you mutable app. 
    // startServerLocalhost is one of the convinience functions which sets up 
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer. 
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.UI.Suave/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofAssembly typeof<AssemblyResources>.Assembly
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
