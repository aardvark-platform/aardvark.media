open screenhotr.example

open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Suave

type Resources = Resources

[<EntryPoint>]
let main args =

    Aardvark.Init()
    Aardium.init()

    let app = new OpenGlApplication()

    let port = 1338
    let mediaUrl = sprintf "http://localhost:%i/" port
    
    WebPart.startServerLocalhost port [
        MutableApp.toWebPart' app.Runtime false (App.start (App.app mediaUrl))
    ] |> ignore
    
    Aardium.run {
        title "Screenshotr Example"
        width 1024
        height 768
        url mediaUrl
        debug true
    }

    0