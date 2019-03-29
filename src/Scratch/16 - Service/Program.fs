open System
open System.Threading
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc
open Inc.Model

open Suave
open Suave.WebPart
open System.Collections.Concurrent

[<EntryPoint; STAThread>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()
    Aardium.init()

    use app = new OpenGlApplication()

    let backChannel = new BlockingCollection<_>()
    let send v = backChannel.Add v

    let instance = App.app send |> App.start

    let backThread =
        Thread(ThreadStart (fun _ -> 
            while true do
                let msg = backChannel.Take()
                instance.update Guid.Empty (Seq.singleton (InstanceStatus msg))
        ))
    backThread.Name <- "backChannel thread"
    backThread.IsBackground <- true
    backThread.Start()

    WebPart.startServerLocalhost 4321 [ 
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
