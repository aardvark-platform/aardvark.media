open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Aardvark.Service.Suave

type Resources = Resources

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofAssembly typeof<Resources>.Assembly
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
