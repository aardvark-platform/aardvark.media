open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI

open Suave
open Suave.WebPart
open Aardium

type Resources = Resources

[<EntryPoint; STAThread>]
let main argv = 
    
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    WebPart.startServer 4321 [ 
        Reflection.assemblyWebPart typeof<Resources>.Assembly
        Reflection.assemblyWebPart typeof<Aardvark.UI.Primitives.EmbeddedResources>.Assembly
        MutableApp.toWebPart' app.Runtime false instance
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
