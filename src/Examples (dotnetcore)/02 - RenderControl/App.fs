module RenderControl.App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open RenderControl.Model


let initialCamera = { 
        FreeFlyController.initial with 
            view = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
    }

let update (model : Model) (msg : Message) =
    match msg with
        | ToggleBackground ->
            { model with background = (if model.background = C4b.Black then C4b.White else C4b.Black) }
        | Camera m -> 
            { model with cameraState = FreeFlyController.update model.cameraState m }
        | CenterScene -> 
            { model with cameraState = initialCamera }

let viewScene (model : AdaptiveModel) (cam : aval<Camera>) =
    let blub (view : aval<Trafo3d>) (proj : aval<Trafo3d>)=
        Sg.draw IndexedGeometryMode.TriangleList
        |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant [|V3d.OOO; V3d.IOO; V3d.OOI|])
        |> Sg.index (AVal.constant [|0;1;2|])
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.White
        }
        |> Sg.viewTrafo view
        |> Sg.projTrafo proj

    let comp() = 
        Aardvark.Service.Scene.custom (fun (values : Aardvark.Service.ClientValues) ->
            blub values.viewTrafo values.projTrafo
            |> Aardvark.SceneGraph.RuntimeSgExtensions.Sg.compile values.runtime values.signature
        )

    DomNode.RenderControl(FreeFlyController.attributes model.cameraState Camera, cam, comp(), None)

let view (model : AdaptiveModel) =
    let cam = model.cameraState.view |> AVal.map (fun view -> { cameraView = view; frustum = Frustum.perspective 60.0 0.2 500.0 1.0 })
    let renderControl = viewScene model cam


    div [style "display: grid; grid-template-rows: 40px 1fr; width: 100%; height: 100%" ] [
        div [style "grid-row: 1"] [
            text "Hello 3D"
            br []
            button [onClick (fun _ -> CenterScene)] [text "Center Scene"]
            button [onClick (fun _ -> ToggleBackground)] [text "Change Background"]
        ]
        renderControl
        br []
        text "use first person shooter WASD + mouse controls to control the 3d scene"
    ]

let threads (model : Model) = 
    FreeFlyController.threads model.cameraState |> ThreadPool.map Camera


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState = initialCamera
               background = C4b.Black
            }
        update = update 
        view = view
    }
