open System
open System.Threading
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc
open Inc.Model

open Suave
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

    let outputDir = System.Environment.CurrentDirectory

    WebPart.runServer 4321 [ 
        Suave.Files.browse outputDir
        MutableApp.toWebPart' app.Runtime false instance
    ]

    0 
