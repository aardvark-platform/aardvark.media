(*

Box selection demo without GUI - controlled via a VS Code like command palette (Ctrl+P)

*)

open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium

type private Marker = class end

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.Init()

    let app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime
    use _ = app

    use mapp =
        App.app |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' runtime false mapp
        WebPart.ofType<Primitives.EmbeddedResources>
        WebPart.ofAssembly typeof<Marker>.Assembly
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
