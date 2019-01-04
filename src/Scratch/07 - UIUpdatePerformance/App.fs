module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model
open System.Net


let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Inc
        yield! time()
    }

let sw = System.Diagnostics.Stopwatch.StartNew()

let update (model : Model) (msg : Message) =
    match msg with
        | Inc -> 
            let s = sprintf "%d" (model.value + 1)
            { model with value = model.value + 1; updateStart = sw.Elapsed.TotalSeconds; things = model.things |> PList.map (fun _ -> s); 
                         angle = model.angle + 0.1   }
        | Go -> { model with threads = ThreadPool.add "anim" (time()) ThreadPool.empty;  }
        | Done -> { model with took = sw.Elapsed.TotalSeconds - model.updateStart }

        | Super -> { model with super = model.super + 1}
        | Tick -> { model with angle = model.angle + 0.01 }
        | GotImage i -> { model with lastImage = i }

let dependencies = 
    [
        { url = "animation.css"; name = "animation"; kind = Stylesheet }
        { url = "support.js"; name = "support"; kind = Script }
    ]

let illegalString = """
abc 
new line then \²³²]³\nsuper2929
k' " , . / \ ; : & % $ # @ *
"""
let create () =
    use wc = new WebClient ()
    let url = sprintf "http://%s:4321/rendering/screenshot/%s?w=%d&h=%d&samples=8" "localhost" "n51" 512 512
    wc.DownloadData url
        

let view (model : MModel) =

    let scene = 
        Sg.box (Mod.constant C4b.Red) (Mod.constant Box3d.Unit) 
            |> Sg.trafo (model.angle |> Mod.map Trafo3d.RotationZ)
            |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.simpleLighting
               }
     

    let renderControl =
       let cameraView = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
       let frustum = Frustum.perspective 50.0 0.1 10.0 1.0
       renderControl (Camera.create cameraView frustum |> Mod.constant) [style "width:200;height:200"; attribute "data-samples" "8"; onEvent "onRendered" [] (fun _ -> Tick)] scene

    let superChannel = model.super |> Mod.channel


    div [] [
        button [onClick (fun _ -> Super)] [text "call super"]
        br []
        text illegalString
        br []
        text (sprintf "%A" (Some dependencies))
        br []
        onBoot "doIt(__ID__)" (div [clazz "jsUpdater"] [text "nothing"])
        text "Hello World"
        br []
        button [onClick (fun _ -> Inc)] [text "Increment"]
        text "    "
        Incremental.text (model.value |> Mod.map string)
        br []
        text "last update took: "
        br []
        Incremental.text (model.took |> Mod.map string)
        br []
        require dependencies ( div [clazz "rotate-center"; style "width:50px;height:50px;background-color:red"] [text "abc"] )
        //Incremental.div AttributeMap.empty <|
        //    alist {
        //        let! a = model.value
        //        for i in 0 .. 10 do
        //            yield button [] [text "ADF"]//text (sprintf "%d" a)]
        //        yield onBoot "aardvark.processEvent('__ID__', 'finished');" (
        //            button [onEvent "finished" [] (fun _ -> Done)] [text "urdar"]
        //        )
        //    }

        Incremental.div AttributeMap.empty <|
            alist {
                let! b = model.super
                yield text (sprintf "current value: %d" b) 
                yield Incremental.div AttributeMap.empty <|
                    alist {
                        let! a = model.super
                
                        let updateData = "gabbl.onmessage = function (data) { console.log('got value ' + data); }"

                        yield text (sprintf "current value: %d" a)

                        yield onBoot' ["gabbl", superChannel] updateData (
                                  text "gubbl"
                              )
                    }
            }

        Incremental.div AttributeMap.empty (model.things |> AList.map (fun t -> button [] [text t]))

        br []

        renderControl

        br []

        Incremental.text (model.lastImage |> Mod.map string)

        br []
    ]



let threads (model : Model) = 
    let rec proc () =
        proclist {
            do! Proc.Sleep 100
            let a = create()
            yield GotImage System.DateTime.Now
            yield! proc()
        }
    ThreadPool.add "screenshots" (proc()) model.threads


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {
                threads = ThreadPool.empty
                value = 0
                took = 0.0
                updateStart = 0.0
                things = PList.ofList [for i in 1 .. 10 do yield sprintf "%d" i]
                super = 0
                angle = 0.0
                lastImage = System.DateTime.Now
            }
        update = update 
        view = view
    }
