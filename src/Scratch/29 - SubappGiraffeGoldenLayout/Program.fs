
open System
open FSharp.Data.Adaptive

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Service
open Aardvark.UI
open Aardium
//open Suave
//open Suave.WebPart

//open Microsoft.AspNetCore.Http
//open FSharp.Control.Tasks
//open System.Net.WebSockets
//open System.Threading.Tasks

//open Microsoft.AspNetCore.Http

open Aardvark.UI.Giraffe
open Aardvark.Service.Giraffe


type EmbeddedResources = EmbeddedResources

[<EntryPoint>]
let main args =
    let inServerMode = true
        //(args |> Array.contains "--server")
        //    || (args |> Array.contains "-s")
            
    let dataFolder =  // TODO refactor safety
        let optFolder = Array.tryFindIndex (fun x -> String.equalsCaseInsensitive "--datafolder" x) args
        match optFolder with
        | Some i -> Some args.[i + 1] 
        | None ->
            let optFolder = Array.tryFindIndex (fun x -> String.equalsCaseInsensitive "-d" x) args
            match optFolder with
            | Some i -> Some args.[i + 1]
            | None ->
                None
            
    Aardvark.Init()
    Aardium.init()

    let app = new OpenGlApplication ()
    let win = app.CreateGameWindow ()
    
    let serverApp = Test.ServerApp.app win inServerMode dataFolder app
                        |> App.start

    let appWebpart = MutableApp.toWebPart app.Runtime serverApp
    let webparts = 
        [
            Reflection.assemblyWebPart (System.Reflection.Assembly.GetExecutingAssembly())
            appWebpart   
        ] |> Giraffe.Core.choose

    Test.Giraffe.Server.startServer "http://*:4321" Threading.CancellationToken.None  (
        webparts
    ) |> ignore

    if inServerMode then
        Console.ReadLine () |> ignore
        Log.warn "Server is running. Please use Chrome Browser."
    else 
        Aardium.run {
            title "Rail4Future Demo"
            width 1024
            height 768
            url "http://localhost:4321/"
        }
    0
