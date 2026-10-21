(*

Box selection demo without GUI - controlled remotely via an HTTP/JSON API (http://localhost:4321/api/...)
and an MCP server (http://localhost:4321/mcp).

Connect Claude Code with:  claude mcp add --transport http aardvark-boxes http://localhost:4321/mcp

*)

open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium

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
        Rest.webPart mapp
        Mcp.webPart mapp
        MutableApp.toWebPart' runtime false mapp
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
