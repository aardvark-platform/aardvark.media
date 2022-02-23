open System
open System.Threading

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium

open Aardvark.UI.Giraffe



[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()
    
    use app = new OpenGlApplication()
    let instance = RenderControl.App.app |> App.start

    let webApp = MutableApp.toWebPart app.Runtime instance
    use cts = new CancellationTokenSource()
    let server = Server.startServer "http://localhost:4321" cts.Token webApp 

    let bind o f =
        match o with
            | None -> None
            | Some v -> f v




    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    cts.Cancel()
    instance.shutdown()

    0 
