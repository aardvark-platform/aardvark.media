open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Primitives.Golden
open Aardium

open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()
    let instance = BoxTreeView.App.app |> App.start

    WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart' app.Runtime false instance
        Aardvark.UI.Primitives.Resources.WebPart
        GoldenLayout.WebPart.suave
        Reflection.assemblyWebPart <| Reflection.Assembly.GetEntryAssembly()
        Suave.Files.browseHome
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1280
        height 800
        title "Box Tree View"
        debug true
    }

    0
