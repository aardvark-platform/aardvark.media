open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open DrawRects

open Suave
open Suave.WebPart

open MBrace.FsPickler
open MBrace.FsPickler.Json

type EmbeddedResources = EmbeddedResources

[<EntryPoint; STAThread>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    Aardium.init()
    

    use app = new OpenGlApplication()
    let instance = DrawRectsApp.app app.Runtime |> App.start

    WebPart.startServerLocalhost 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Reflection.assemblyWebPart typeof<EmbeddedResources>.Assembly
        Reflection.assemblyWebPart typeof<Aardvark.UI.Primitives.EmbeddedResources>.Assembly
        Suave.Files.browseHome
    ]  

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }

    0 
