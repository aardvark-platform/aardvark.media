open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open GeoJsonViewer

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.Init()
            
    let sites = [
        @"http://minerva1.eox.at:8600/opensearch/collections/MAHLI/json/"
        @"http://minerva1.eox.at:8600/opensearch/collections/FrontHazcam/json/"
        @"http://minerva1.eox.at:8600/opensearch/collections/Mastcam/json/"
        @"http://minerva1.eox.at:8600/opensearch/collections/APXS/json/"
    ]

    let data = sites |> MinervaGeoJSON.loadMultiple

    use app = new OpenGlApplication()
    use mapp = App.app data |> App.start

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
        WebPart.ofAssembly typeof<Primitives.EmbeddedResources>.Assembly
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
