﻿open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()

    let instance = 
        App.app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Reflection.assemblyWebPart (System.Reflection.Assembly.GetEntryAssembly())
    ]  
    
    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
