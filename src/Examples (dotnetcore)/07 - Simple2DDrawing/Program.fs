open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI

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

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ] |> ignore
     
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1000
        height 800
        debug true
    }
    
    0 
