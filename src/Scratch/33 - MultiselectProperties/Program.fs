open System

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Primitives.Golden

open Suave
open Suave.WebPart
open Aardium

[<EntryPoint; STAThread>]
let main argv = 
    
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    Config.defaultDocumentTitle <- "Multiselect Properties"

    WebPart.startServerLocalhost 4321 [ 
        Aardvark.UI.Primitives.Resources.WebPart
        MutableApp.toWebPart' app.Runtime false instance
        GoldenLayout.WebPart.suave
        Suave.Files.browseHome
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        title "Multiselect Properties"
        dynamicTitle true
        debug true
    }

    0
