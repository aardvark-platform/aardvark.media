open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc

open Suave
open Suave.WebPart

[<EntryPoint; STAThread>]
let main argv = 
    let port = 
        match argv with
            | [|port|] -> 
                match System.Int32.TryParse port with
                    | (true,v) -> v
                    | _ -> 
                        Log.warn "could not parse port."
                        4321
            | _ -> 4321

    
    let uri = sprintf "http://localhost:%d/" port
    Log.line "Service will run here: %s" uri

    Ag.initialize()
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()
    let instance = App.app |> App.start

    WebPart.runServer port [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ] |> ignore

    0 
