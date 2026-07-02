open System
open Aardvark.Base
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.UI.Suave
open Aardium
open Inc

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.Init()

    use app = new OpenGlApplication()
    let mutable mapp : Option<MutableApp<_,_,_>> = None
    let sendMessage (msg : Inc.Model.Message) = match mapp with None -> () | Some mapp -> mapp.Update(Guid.NewGuid(), Seq.singleton msg)
    use mapp =
        let i = App.app sendMessage |> App.start
        mapp <- Some i
        i

    Server.startLocalhost 4321 mapp.CancellationToken [
        MutableApp.toWebPart' app.Runtime false mapp
    ] |> ignore

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
#if DEBUG
        debug true
        log (fun msg -> Report.Line(2, $"[Aardium] {msg}"))
#else
        debug false
#endif
    }

    0 
