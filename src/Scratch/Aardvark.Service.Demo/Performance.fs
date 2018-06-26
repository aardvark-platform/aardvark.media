module PerformanceApp

open Performance
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Action = 
    | Inc 
    //| Dec
    | CameraAction of CameraController.Message

let addObj m =
    match PList.tryAt 0 m.objects with
        | None -> m
        | Some o -> 
            { m with visible = PList.append o m.visible; objects = PList.removeAt 0 m.objects }

let update (m : Model) (a : Action) =
    match a with
        | Inc ->
            let mutable m = m
            for i in 0 .. 500 do
                m <- addObj m
            m
        | CameraAction a -> { m with cameraState = CameraController.update m.cameraState a }

let cam = 
    Camera.create (CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI) (Frustum.perspective 60.0 0.1 10.0 1.0)


let threeD (m : MModel) =

    let box = Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
    let objects =
        aset {
            for o in m.visible |> AList.toASet do
                yield box |> Sg.trafo (Mod.constant o)
        } |> Sg.set

    let sg =
        objects
        |> Sg.noEvents
        |> Sg.effect [
                    toEffect DefaultSurfaces.trafo
                    //toEffect DefaultSurfaces.diffuseTexture
                    toEffect <| DefaultSurfaces.constantColor C4f.Red 
                    toEffect <| DefaultSurfaces.simpleLighting 
                ]
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
    CameraController.controlledControl m.cameraState CameraAction
        (Mod.constant frustum) 
        (AttributeMap.ofList [ attribute "style" "width:100%; height: 90%"]) sg

let view (m : MModel) =
    div [] [
        text "constant text"
        br []
        Incremental.text (m.visible |> AList.toMod |> (Mod.map (string << PList.count)))
        //text (Mod.force s)
        br []
        button [onMouseClick (fun _ -> Inc)] [text "inc"]
        //button [onMouseClick (fun _ -> Dec)] [text "dec"]
        br []
        threeD m
    ]

let app =
    let objects =
        [ for x in -10 .. 10 do
            for y in -10 .. 10 do
                for z in -10 .. 10 do
                    yield Trafo3d.Translation(float x, float y, float z)
        ] |> PList.ofList
    {
        unpersist = Unpersist.instance
        threads = 
            fun (model : Model) -> CameraController.threads model.cameraState |> ThreadPool.map CameraAction
        initial = update { visible = PList.empty ; objects = objects; cameraState = CameraController.initial } Inc 
        update = update
        view = view
    } 


let start() = App.start app