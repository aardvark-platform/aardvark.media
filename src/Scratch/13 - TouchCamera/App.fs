namespace TouchCamera

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.TouchStick

module TouchStickApp =
    open Aardvark.Application

    let rec update (m : TouchStickModel) (msg : TouchStickMessage) =
    
        match msg with
            | SwitchExpo -> { m with cameraState = { m.cameraState with freeFlyConfig = { m.cameraState.freeFlyConfig with touchScalesExponentially = not m.cameraState.freeFlyConfig.touchScalesExponentially }}}

            | Camera im -> { m with cameraState = FreeFlyController.update m.cameraState im}

    let viewScene (model : AdaptiveTouchStickModel) =
        let sponza() =
            Loader.Assimp.load @"D:\temp\sponza\sponza_cm.obj"
                |> Sg.adapter
                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, -V3d.OIO, V3d.OOO))
                |> Sg.scale 0.01
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.diffuseTexture
                    do! DefaultSurfaces.simpleLighting
                }
        IndexedGeometryPrimitives.solidCoordinateBox 100.0
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                }
            |> Sg.andAlso(
                Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
                 |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.vertexColor
                        do! DefaultSurfaces.simpleLighting
                    })
        
    let view (m : AdaptiveTouchStickModel) =
            
        div [] [
            div [] [
                Incremental.text (m.cameraState.freeFlyConfig.touchScalesExponentially |> AVal.map (fun e -> if e then "Exponential" else "Linear"))
                button [onClick (fun _ -> SwitchExpo); attribute "style" "width:20vw;height:5vh"] [text "SWITCH"]
            ]
            
            FreeFlyController.controlledControl m.cameraState Camera (Frustum.perspective 80.0 0.1 1000.0 1.0 |> AVal.constant) 
                (AttributeMap.ofList [  
                    style "width: 100vw; height:100vh"

                    attribute "showFPS" "true";
                    attribute "data-samples" "8"
                ]
                ) 
                (viewScene m)
        ]
    let threads (m : TouchStickModel) = 
        ThreadPool.empty
    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = TouchStickModel.initial
            update = update 
            view = view
        }
