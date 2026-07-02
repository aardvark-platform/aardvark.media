open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Inc

[<EntryPoint; STAThread>]
let main argv = 

    //FSharp.Data.Adaptive.DefaultEqualityComparer.SetProvider FSharp.Data.Adaptive.DefaultEqualityComparer.Shallow

    Aardvark.Init()
    Aardium.Init()

    Config.shouldTimeJsCodeGeneration <- true
    Config.shouldTimeUIUpdate <- true
    Config.shouldTimeUpdate <- true
    
    use app = new OpenGlApplication()
    use mapp = App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofType<Primitives.EmbeddedResources>
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
