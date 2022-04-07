open System

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI

open Suave
open Suave.WebPart
open Suave.Filters
open Suave.Successful
open Suave.Operators
open Aardium

type Resources = Resources

[<EntryPoint; STAThread>]
let main argv = 
    
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    WebPart.startServerLocalhost 4321 [ 
        Reflection.assemblyWebPart typeof<Resources>.Assembly
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ] |> ignore
     
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
