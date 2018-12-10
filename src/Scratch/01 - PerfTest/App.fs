module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model
open Aardvark.UI



let initialCamera = { 
    FreeFlyController.initial with 
        view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
}

let rnd = System.Random()

let update (model : Model) (msg : Message) =
    if rnd.NextDouble() * 1000.0 < model.updateLoad.value then System.Threading.Thread.Sleep 100
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | Tick t -> 
            { model with trafo = Trafo3d.RotationZ(t * 0.1) }
        | ToggleAnimation -> 
            { model with animationEnabled = not model.animationEnabled }
        | SetUpdateLoad s -> 
            { model with updateLoad = Numeric.update model.updateLoad s  }
        | SetGpuLoad s -> 
            { model with gpuLoad = Numeric.update model.gpuLoad s }
        | SetModLoad s -> 
            { model with modLoad = Numeric.update model.modLoad s }


module Shader =
    open FShade

    type UniformScope with
        member x.GpuLoad : int = uniform?GpuLoad
        member x.BadMod : int = uniform?BadMod

    let badShader (v : Effects.Vertex) =
        fragment {
            let load = uniform.GpuLoad
            let mutable strange = v.tc.X * float uniform.BadMod
            for i in 0 .. load  do
                strange <- sin strange * cos strange
            return v.c + 0.01 * strange * V4d.IIII
        }

let viewScene (model : MModel) =

    let waitTime = Mod.map2 (fun _ time -> System.Threading.Thread.Sleep (int time);5) model.trafo model.modLoad.value

    Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
     |> Sg.trafo model.trafo
     |> Sg.uniform "GpuLoad" (Mod.map int model.gpuLoad.value)
     |> Sg.uniform "BadMod" waitTime
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
            do! Shader.badShader
        }


let mymap (f : 'a -> 'b) (ui : DomNode<'a>) : DomNode<'b> =
    let app =
        {
            initial = ()
            update = fun () _ -> ()
            view = fun () -> ui
            unpersist = { create = id; update = constF ignore }
            threads = fun () -> ThreadPool.empty
        }

    subApp' (fun _ msg -> Seq.singleton (f msg)) (fun _ _ -> Seq.empty) [] app

// variant with html5 grid layouting (currently not working in our cef)
let view (model : MModel) =
    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ attribute "showFPS" "true"; attribute "data-renderalways" "1"; style "width: 100%; height:80%; "]) 
                    (viewScene model)

    body [] [
        require Html.semui (
            div [] [
                div [] [
                    text "Hello 3D"
                    br []
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene.."]
                ]
                renderControl
                br []
                text "Animate: "
                Html.SemUi.toggleBox model.animationEnabled ToggleAnimation
                br []
                text "GPU Load: "
                Numeric.numericField (SetGpuLoad >> Seq.singleton) AttributeMap.empty model.gpuLoad Slider
                br [] 
                text "Update Load: "
                Numeric.numericField (SetUpdateLoad >> Seq.singleton) AttributeMap.empty model.updateLoad Slider
                br [] 
                text "Mod Load: "
                Numeric.numericField (SetModLoad  >> Seq.singleton) AttributeMap.empty model.modLoad Slider
                br [] 
            ]
        )
    ]

let totalTime = System.Diagnostics.Stopwatch.StartNew()
let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Tick totalTime.Elapsed.TotalSeconds
        yield! time()
    }

let threads (model : Model) = 
    let cameraController = FreeFlyController.threads model.cameraState |> ThreadPool.map Camera
    if model.animationEnabled then
        ThreadPool.union cameraController (ThreadPool.add "timeroida" (time()) ThreadPool.empty)
    else cameraController


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
               trafo = Trafo3d.Identity
               animationEnabled = true

               gpuLoad = { min = 0.0; max = 100000.0; value = 0.0; step = 100.0; format = "{0:0}" }
               modLoad = { min = 0.0; max = 1000.0; value = 0.0; step = 1.0; format = "{0:0}" }
               updateLoad = { min = 0.0; max = 1000.0; value = 0.0; step = 1.0; format = "{0:0}" }
            }
        update = update 
        view = view
    }
