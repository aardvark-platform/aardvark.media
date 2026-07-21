open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Primitives.Golden
open Aardvark.UI.Giraffe
open Aardium
open Golden

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.Init()

    use app = new OpenGlApplication()
    use mapp = App.app |> App.start

    mapp.DocumentTitle <- App.initialTitle

    let http = HttpBackend.Instance

    Server.startLocalhost 4321 mapp.CancellationToken [
        Aardvark.UI.Primitives.Resources.toWebPart http
        MutableApp.toWebPart' app.Runtime false mapp
        GoldenLayout.toWebPart http
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        title App.initialTitle
        dynamicTitle true
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
        log (fun msg -> Report.Line(2, $"[Aardium] %s{msg}"))
    }

    0 
