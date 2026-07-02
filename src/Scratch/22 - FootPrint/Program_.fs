open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc

open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()
    let instance = App.app |> App.start

    WebPart.startServerLocalhost 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }

    //use ctrl = new AardvarkCefBrowser()
    //ctrl.Dock <- DockStyle.Fill
    //form.Controls.Add ctrl
    //ctrl.StartUrl <- "http://localhost:4321/"
    //ctrl.ShowDevTools()
    //form.Text <- "Examples"
    //form.Icon <- Icons.aardvark 

    //Application.Run form
    0 
