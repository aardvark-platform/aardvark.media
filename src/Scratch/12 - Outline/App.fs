module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model
open Aardvark.UI
open Aardvark.SceneGraph



let initialCamera = { 
    FreeFlyController.initial with 
        view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
}

let rnd = System.Random()

let update (model : Model) (msg : Message) =
    match msg with
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }
        | Tick t -> 
            { model with trafo = Trafo3d.RotationZ(t * 0.1) }
        | ToggleAnimation -> 
            { model with animationEnabled = not model.animationEnabled }


module Shader =
    open FShade
   
    type SuperVertex = 
        {
            [<Position>] pos :  V4d
            [<SourceVertexIndex>] i : int
        }

    let lines (t : Triangle<SuperVertex>) =
        line {
            yield t.P0
            yield t.P1
            restartStrip()
            
            yield t.P1
            yield t.P2
            restartStrip()

            yield t.P2
            yield t.P0
            restartStrip()
        }

let viewScene (model : MModel) =

    let read =
        StencilMode(StencilOperationFunction.Keep, StencilOperationFunction.Keep, StencilOperationFunction.Keep, StencilCompareFunction.Equal, 0, 0xffu)

    let write =
        StencilMode(StencilOperationFunction.Replace, StencilOperationFunction.Keep, StencilOperationFunction.Keep, StencilCompareFunction.Always, 1, 0xffu)

    let geom =
        [
            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
            IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d(0.0,1.0,2.0), V3d.OOI, 1.0, 0.25)) C4b.Blue 20 20 |> Sg.ofIndexedGeometry
        
        ] |> Sg.ofList

    let outline = 
        geom
         |> Sg.trafo model.trafo
         |> Sg.stencilMode (Mod.constant read)
         |> Sg.pass (RenderPass.after "outline" RenderPassOrder.Arbitrary RenderPass.main)
         |> Sg.uniform "LineWidth" (Mod.constant 5.0)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.lines
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.constantColor C4f.Red
            }

    let regular = 
        geom
         |> Sg.trafo model.trafo
         |> Sg.stencilMode (Mod.constant write)
         |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
      
    Sg.ofSeq [regular; outline] |> Sg.noEvents

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
                    text "Hello 3D Contour"
                    br []
                    button [onClick (fun _ -> CenterScene)] [text "Center Scene.."]
                ]
                renderControl
                br []
                text "Animate: "
                Html.SemUi.toggleBox model.animationEnabled ToggleAnimation
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
            }
        update = update 
        view = view
    }
