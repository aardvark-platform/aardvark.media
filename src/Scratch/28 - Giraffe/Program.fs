open System
open System.Threading
open Giraffe

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Giraffe
open Aardium


open Saturn
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.WebSockets
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting

let runDirect () =
    use app = new OpenGlApplication()
    use mapp = RenderControl.App.app |> App.start

    Server.start "http://localhost:4321" mapp.CancellationToken [
        MutableApp.toWebPart app.Runtime mapp
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

let runHost () =

    use app = new OpenGlApplication()
    use mapp = RenderControl.App.app |> App.start

    let host =
        Server.createHost "http://localhost:4321" [
            MutableApp.toWebPart app.Runtime mapp
        ]

    host.Build().Run()

    0 

let runWithRoute () =
    Aardium.init()

    use app = new OpenGlApplication()
    use mapp = RenderControl.App.app |> App.start

    Server.start "http://localhost:4321" mapp.CancellationToken [
        subRoute "/test" (MutableApp.toWebPart app.Runtime mapp)
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/test/"
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

let runWithSaturn () = 
    Aardium.init()

    use app = new OpenGlApplication()
    let mapp = RenderControl.App.app |> App.start

    let renderApp = MutableApp.toWebPart' app.Runtime false mapp

    let app = 
        choose [
            subRoute "/render"  renderApp
            route "/helloWorld" >=> (Giraffe.Core.text "Hello World from Saturn")
        ]

    let app =
        application {
            url "http://*:8085"
            use_router app
            use_gzip
            memory_cache
            app_config (fun ab -> ab.UseWebSockets().UseMiddleware<WebSockets.WebSocketMiddleware>())
        }

    use serverApp = app.Build()
    let server = serverApp.StartAsync(mapp.CancellationToken)

    Aardium.run {
        url "http://localhost:8085/render/"
        width 1024
        height 768
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    mapp.Dispose()
    server.Wait()

    0 


[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()

    // runHost ()
    // runWithRoute()
    runWithSaturn()
