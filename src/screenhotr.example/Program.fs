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

[<EntryPoint>]
let main args =
    Aardvark.Init()
    Aardium.init()

    let app = new OpenGlApplication()

    WebPart.startServerLocalhost 4321 [
        MutableApp.toWebPart' app.Runtime false (App.start App.app)
    ] |> ignore
    
    Aardium.run {
        title "Aardvark rocks \\o/"
        width 1024
        height 768
        url "http://localhost:4321/"
    }

    0