open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc

open Suave
open Suave.WebPart

open System.Threading
open Aardvark.Rendering.Vulkan

[<EntryPoint; STAThread>]
let main argv = 
    let port,processId = 
        match argv with
            | [|port;processId|] -> 
                match System.Int32.TryParse port, System.Int32.TryParse processId with
                    | (true,v), (true, pId) -> v, pId
                    | _ -> 
                        failwith "usage: port parentProcessId"
            | _ -> 4321, -1

    if processId > 0 then
        let killThread = 
            Thread(ThreadStart(fun _ -> 
                try
                    let p = System.Diagnostics.Process.GetProcessById processId
                    p.WaitForExit()
                    System.Environment.Exit(2)
                with e -> printfn "%A" e
            ))
        killThread.Start()


    let uri = sprintf "http://localhost:%d/" port
    Log.line "Service will run here: %s" uri


    Ag.initialize()
    Aardvark.Init()
    Aardium.init()
    //System.Diagnostics.Debugger.Launch()

    use app = new HeadlessVulkanApplication(true)
    let instance = Inc.Master.app |> App.start

    WebPart.startServer port [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ] |> ignore

    if processId < 0 then
        Aardium.run {
            url "http://localhost:4321/"
            width 1024
            height 768
            debug true
        }
    else Console.ReadLine() |> ignore

    0 
