open screenhotr.example

open System
open FSharp.Data.Adaptive

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Service
open Aardvark.UI
open Aardium
open Suave
open Suave.WebPart

type Resources = Resources

[<EntryPoint>]
let main args =

    Aardvark.Init()
    Aardium.init()

    let app = new OpenGlApplication()

    let port = 1337 
    let mediaUrl = sprintf "http://localhost:%i/" port
    
    WebPart.startServerLocalhost port [
        MutableApp.toWebPart' app.Runtime false (App.start (App.app mediaUrl))
    ] |> ignore
    
    Aardium.run {
        title "Screenshotr Example"
        width 1024
        height 768
        url mediaUrl
    }

    0