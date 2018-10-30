module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        | Inc -> { model with value = model.value + 1 }
        | Camera m -> { model with cameraState = FreeFlyController.update model.cameraState m }


let viewScene (model : MModel) =
    let rand = System.Random()
    let spheres = 
        [ for i in 0 .. 1000 do
            let color = C4f(rand.NextDouble(),rand.NextDouble(),rand.NextDouble(),rand.NextDouble()) |> C4b |> Mod.constant
            yield Sg.sphere 4 color (Mod.constant 1.0) |> Sg.translate (rand.NextDouble()*15.0) (rand.NextDouble()*15.0) (rand.NextDouble() * 15.0)
        ]
    Sg.ofSeq spheres
     |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
     |> Sg.blendMode (Mod.constant BlendMode.Blend)

let view (model : MModel) =

    let scene = viewScene model
    let renderControl =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 400px; height:400px; background: #000"; attribute "data-samples" "8"]) 
                    scene
    let renderControl2 =
       FreeFlyController.controlledControl model.cameraState Camera (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ style "width: 400px; height:400px; background: #000"; attribute "data-samples" "8"; attribute "useMapping" "false"]) 
                    scene
    div [] [
        renderControl
        br []
        renderControl2
    ]


let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               value = 0
               cameraState = FreeFlyController.initial
            }
        update = update 
        view = view
    }
