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

[<EntryPoint; STAThread>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        prefix "/resources" >=> Reflection.assemblyWebPart (System.Reflection.Assembly.GetEntryAssembly())
        Suave.Files.browseHome
    ] |> ignore
     
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
