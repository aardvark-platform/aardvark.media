open screenhotr.example

open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.Service.Suave
open Aardium

type Resources = Resources

[<EntryPoint>]
let main args =
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let port = 1338
    let mediaUrl = $"http://localhost:{port}/"
    use mapp = App.app mediaUrl |> App.start

    Server.startLocalhost port mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
    ] |> ignore

    Aardium.run {
        title "Screenshotr Example"
        width 1024
        height 768
        url mediaUrl
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    0