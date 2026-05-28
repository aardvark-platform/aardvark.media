open System
open System.Threading
open Giraffe

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium


open Aardvark.UI.Giraffe
open Saturn
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.WebSockets
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting

let runDirect () =

    use app = new OpenGlApplication()
    let instance = RenderControl.App.app |> App.start

    let webApp = MutableApp.toWebPart app.Runtime instance
    use cts = new CancellationTokenSource()
    let server = Server.startServer "http://localhost:4321" cts.Token webApp 

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    cts.Cancel()
    instance.shutdown()

    0 

let runHost () =

    use app = new OpenGlApplication()
    let instance = RenderControl.App.app |> App.start

    let webApp = MutableApp.toWebPart app.Runtime instance
    use cts = new CancellationTokenSource()
    let host = Server.createHost "http://localhost:4321" webApp

    host.Build().Run()
    
    cts.Cancel()
    instance.shutdown()

    0 

let runWithRoute () = 

    use app = new OpenGlApplication()
    let instance = RenderControl.App.app |> App.start

    let webApp = subRoute "/test"  (MutableApp.toWebPart app.Runtime instance)
    use cts = new CancellationTokenSource()
    let server = Server.startServer "http://localhost:4321" cts.Token webApp 


    Aardium.run {
        url "http://localhost:4321/test/"
        width 1024
        height 768
        debug true
    }
    cts.Cancel()
    instance.shutdown()

    0 



let runWithSaturn () = 

    use app = new OpenGlApplication()
    let instance = RenderControl.App.app |> App.start

    use cts = new CancellationTokenSource()

    let renderApp, disposeRenderApp = MutableApp.toWebPart' app.Runtime false instance

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
    let server = serverApp.StartAsync(cts.Token)


    Aardium.run {
        url "http://localhost:8085/render/"
        width 1024
        height 768
        debug true
    }

    disposeRenderApp.Dispose()
    cts.Cancel()
    instance.shutdown()
    server.Wait()

    0 


[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    //Aardium.init()

    runHost ()
    //runWithRoute()
    //runWithSaturn()

