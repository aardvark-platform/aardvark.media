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
    Aardium.Init()

    use app = new OpenGlApplication()
    use mapp = App.app app.Runtime |> App.start

    Aardvark.UI.Config.shouldTimeJsCodeGeneration <- true
    Aardvark.UI.Config.shouldTimeUIUpdate <- true
    // Aardvark.UI.Config.shouldTimeUpdate <- true
    Aardvark.UI.Config.showTimeJsAssembly <- true
    Aardvark.UI.Config.shouldPrintDOMUpdates <- true

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
