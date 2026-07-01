open System
open System.Threading
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Inc
open Inc.Model

open System.Collections.Concurrent

[<EntryPoint; STAThread>]
let main argv =
    Aardvark.Init()
    Aardium.Init()

    use app = new OpenGlApplication()

    let backChannel = new BlockingCollection<_>()
    let send v = backChannel.Add v

    use mapp = App.app send |> App.start

    let backThread =
        Thread(ThreadStart (fun _ -> 
            while true do
                let msg = backChannel.Take()
                mapp.Update(Guid.Empty, (Seq.singleton (InstanceStatus msg)))
        ))
    backThread.Name <- "backChannel thread"
    backThread.IsBackground <- true
    backThread.Start()

    let outputDir = System.Environment.CurrentDirectory

    Server.startLocalhost 4321 CancellationToken.None [
        Suave.Files.browse outputDir
        MutableApp.toWebPart' app.Runtime false mapp
    ] |> _.Wait()

    0 
