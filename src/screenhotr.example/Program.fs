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

    let port = 4321 // search free port...
    let myUrl = sprintf "http://localhost:%i/" port
    
    WebPart.startServerLocalhost port [
        Reflection.assemblyWebPart typeof<Resources>.Assembly
        Reflection.assemblyWebPart typeof<Aardvark.UI.Primitives.EmbeddedResources>.Assembly
        MutableApp.toWebPart' app.Runtime false (App.start (App.app myUrl))
    ] |> ignore
    
    Aardium.run {
        title "Aardvark rocks \\o/"
        width 1024
        height 768
        url myUrl
    }

    0