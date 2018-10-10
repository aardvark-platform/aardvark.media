open System
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc

open Suave
open Suave.WebPart
open Aardvark.Rendering.Vulkan

open Aardvark.Base.Incremental

open Inc.Model


let dehateObject (m : Object) : IMod<IObject> =
    m.trafo |> Mod.map (fun t -> { itrafo = t })

//let dehateScene1 (s : Scene) : IMod<IScene> = 
//    let reader = s.objects.GetReader()
//    Mod.custom (fun t -> 
//        printfn "running dehate"

//        let _ = reader.GetOperations t

//        let objects = [ for o in reader.State do yield { itrafo = o.trafo.GetValue t }]
        
//        { iobjects = objects |> HSet.ofSeq }
//    )

let dehateScene2 (s : Scene) : IMod<IScene> = 
    let reader = (s.objects |> ASet.mapM dehateObject).GetReader()
    Mod.custom (fun t -> 
        printfn "running dehate"

        let _ = reader.GetOperations t
 
        { iobjects = reader.State  }
    )


let dehateScene3 (s : Scene) : IMod<IScene> = 
    adaptive {
        let objects = 
            aset {
                for o in s.objects do
                    let! t = o.trafo
                    yield { itrafo = t }
            }
        let! o = ASet.toMod objects
        return { iobjects = o  }
    }

let dehateScene99 (s : Scene) : IMod<IScene> = 
    let reader = (s.objects |> ASet.mapM dehateObject).GetReader()
    let mutable objects = HRefSet.empty
    Mod.custom (fun t -> 
        let deltas = reader.GetOperations(t)
        let (a,b) = deltas |> HRefSet.applyDelta objects
        { iobjects = a  }
    )

open System.Collections.Generic
let dehateScene22 (s : Scene) : IMod<IScene> = 
    let cache = Dictionary<Object,IMod<IObject>>()
    let r = s.objects.GetReader()
    Mod.custom (fun t -> 
        printfn "running dehate"

        for d in r.GetOperations(t) do
            match d with
                | Rem(1, v) -> 
                    cache.Remove v |> ignore
                | Add(1,v) -> 
                    let i = v.trafo |> Mod.map (fun t -> { itrafo = t })
                    cache.[v] <- i
 
        { iobjects = r.State |> Seq.map (fun o -> cache.[o].GetValue t) |> HRefSet.ofSeq }
    )

let dehateScene (s : Scene) : IMod<IScene> = 
    let reader = s.objects.GetReader()
    Mod.custom (fun t -> 
        printfn "running dehate"

        reader.GetOperations(t) |> ignore
       

        { iobjects = reader.State |> HRefSet.map (fun o -> printfn "mk"; { itrafo = o.trafo.GetValue(t)})  }
    )

[<EntryPoint; STAThread>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()

    let cnt = 200000
    let trafos = [| for i in 1.. cnt do yield Mod.init (sprintf "A%d" i) |]
    let objects = 
        [
            for t in trafos do
                yield { trafo = t }
        ] |> CSet.ofList

    let scene = { objects = objects }
    let iscene = dehateScene scene
    let mscene = MIScene.Create({iobjects=HRefSet.empty})
    let sw = System.Diagnostics.Stopwatch()
    sw.Start()
    let mscene = iscene |> Mod.unsafeRegisterCallbackKeepDisposable (fun s -> mscene.Update s; sw.Stop())
    printfn "initial took: %A" (sw.Elapsed.TotalSeconds)
    
    sw.Start()
    transact (fun _ -> 
        trafos.[cnt - 10].Value <- "B"
    )
    
    printfn "updating single value took: %A" (sw.Elapsed.TotalSeconds)

    System.Environment.Exit 0

    Aardium.init()

    use app = new HeadlessVulkanApplication()
    let instance = App.app |> App.start

    // use can use whatever suave server to start you mutable app. 
    // startServerLocalhost is one of the convinience functions which sets up 
    // a server without much boilerplate.
    // there is also WebPart.startServer and WebPart.runServer. 
    // look at their implementation here: https://github.com/aardvark-platform/aardvark.media/blob/master/src/Aardvark.Service/Suave.fs#L10
    // if you are unhappy with them, you can always use your own server config.
    // the localhost variant does not require to allow the port through your firewall.
    // the non localhost variant runs in 127.0.0.1 which enables remote acces (e.g. via your mobile phone)
    WebPart.startServerLocalhost 4321 [ 
        MutableApp.toWebPart' app.Runtime false instance
        Suave.Files.browseHome
    ]  

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }

    //use ctrl = new AardvarkCefBrowser()
    //ctrl.Dock <- DockStyle.Fill
    //form.Controls.Add ctrl
    //ctrl.StartUrl <- "http://localhost:4321/"
    //ctrl.ShowDevTools()
    //form.Text <- "Examples"
    //form.Icon <- Icons.aardvark 

    //Application.Run form
    0 
