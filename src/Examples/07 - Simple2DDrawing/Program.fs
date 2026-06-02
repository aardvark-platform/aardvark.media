open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Simple2DDrawing

[<EntryPoint; STAThread>]
let main argv = 
    
    Aardvark.Init()
    Aardium.Init()

    use app = new OpenGlApplication()

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofAssembly typeof<Model>.Assembly
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1000
        height 800
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    0 
