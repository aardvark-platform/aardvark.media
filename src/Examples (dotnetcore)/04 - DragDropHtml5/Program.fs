﻿open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Suave
open Suave.WebPart

type Resources = Resources

[<EntryPoint; STAThread>]
let main argv = 

    
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    WebPart.startServerLocalhost 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Reflection.assemblyWebPart typeof<Resources>.Assembly
    ] |> ignore
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
