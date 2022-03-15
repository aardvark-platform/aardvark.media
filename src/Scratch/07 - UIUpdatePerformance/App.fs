module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Inc.Model
open System.Net
open Aardvark.Base.Ag


let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Inc
        yield! time()
    }

let sw = System.Diagnostics.Stopwatch.StartNew()

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | Inc -> 
            let s = sprintf "%d" (model.value + 1)
            { model with value = model.value + 1; updateStart = sw.Elapsed.TotalSeconds; things = model.things |> IndexList.map (fun _ -> s); 
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
        

type RO(xs : aset<IRenderObject>) = 
    interface Aardvark.SceneGraph.ISg
    member x.RenderObjects = xs

[<Rule>]
type RoSem() =
    member x.RenderObjects(a : RO, scope : Ag.Scope) =
        a.RenderObjects

open System.Threading
open Aardvark.SceneGraph.Semantics
open System.Collections.Generic

let view (runtime : IRuntime) (model : AdaptiveModel) =


    let cobjects = cset()
    let hobjects = List<_>()

    let theHatefulThread = 

        let rnd = new System.Random()
        let template =
            Sg.sphere 10 (AVal.constant C4b.White) (AVal.constant 1.0)
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.Red

                }

        // extract the render object using the scene graph semantics
        let template =
            template.RenderObjects(Ag.Scope.Root) |> ASet.force |> HashSet.toList |> List.head |> unbox<RenderObject>

        Thread(ThreadStart (fun _ -> 
            let viewTrafo = 
                model.cameraState.view |> AVal.map CameraView.viewTrafo
            let projTrafo = 
                Frustum.perspective 60.0 0.1 150.0 1.0 |> Frustum.projTrafo |> AVal.constant

            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            let mkTrafo = 
                let rnd = new System.Random()
                fun _ -> Trafo3d.Translation(rnd.NextDouble()*5.0,rnd.NextDouble()*5.0,rnd.NextDouble()*5.0)


            while true do 
                if rnd.NextDouble() < 0.5 then
                    let uniforms (t : Trafo3d) =
                        UniformProvider.ofList [
                            "ModelTrafo", AVal.constant t :> IAdaptiveValue
                            "ViewTrafo", viewTrafo :> IAdaptiveValue
                            "ProjTrafo", projTrafo :> IAdaptiveValue
                        ]

                    let trafo = mkTrafo () 

                    let newRo = 
                        { template with
                            Id = newId()
                            Uniforms = uniforms trafo
                        } :> IRenderObject

                    //use r = runtime.
                    let p = runtime.PrepareRenderObject(signature,newRo)

                    System.Threading.Thread.Sleep 1

                    transact (fun _ -> 
                        cobjects.Add (p :> IRenderObject) |> ignore
                        hobjects.Add(p :> IRenderObject) |> ignore
                        printfn "cnt: %A" cobjects.Count
                    )
                else
                    if hobjects.Count > 0 then
                        transact (fun _ -> 
                            let deadOne = hobjects.[rnd.Next(0,hobjects.Count - 1)]
                            hobjects.Remove deadOne |> ignore
                            cobjects.Remove deadOne |> ignore
                        )

                System.Threading.Thread.Sleep(1000)
        ))
    
    theHatefulThread.Start()

    let mega = RO(cobjects :> aset<_>)

    let scene = 
         Sg.sphere 14 (AVal.constant C4b.White) (AVal.constant 1.0)
        //Sg.box (AVal.constant C4b.Red) (AVal.constant Box3d.Unit) 
            |> Sg.trafo (model.angle |> AVal.map Trafo3d.RotationZ)
            |> Sg.andAlso (mega :> Aardvark.SceneGraph.ISg |> Sg.noEvents)
            |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.White
               }
     

    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant) 
                    ( AttributeMap.ofList [ 
                            style "width: 400px; height:400px; background: #222"; 
                            attribute "data-samples" "8"; attribute "data-quality" "10"
                            attribute "useMapping" "false"
                            attribute "data-customLoaderImg" "url('https://upload.wikimedia.org/wikipedia/commons/5/57/Fsharp_logo.png')"
                            attribute "data-customLoaderSize" "100px"
                     ]) 
                    scene
    let superChannel = model.super |> AVal.channel


    div [] [
        require (Html.semui) (
            i [clazz "mars outline icon"] []
        )
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
        Incremental.text (model.value |> AVal.map string)
        br []
        text "last update took: "
        br []
        Incremental.text (model.took |> AVal.map string)
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

        Incremental.text (model.lastImage |> AVal.map string)

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



let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let app (runtime : IRuntime) =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {
                threads = ThreadPool.empty
                value = 0
                took = 0.0
                updateStart = 0.0
                things = IndexList.ofList [for i in 1 .. 10 do yield sprintf "%d" i]
                super = 0
                angle = 0.0
                lastImage = System.DateTime.Now
                cameraState = initialCamera
            }
        update = update 
        view = view runtime
    }
