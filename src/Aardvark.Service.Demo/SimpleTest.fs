module SimpleTestApp

open SimpleTest
open Aardvark.Base
open Aardvark.Base.Incremental


type Action = 
    | Inc 
    | Dec
    | CameraAction of Demo.CameraController.Message

let update (m : Model) (a : Action) =
    match a with
        | Inc -> { m with value = m.value + 1.0 }
        | Dec -> { m with value = m.value - 1.0 } 
        | CameraAction a -> { m with cameraModel = Demo.CameraController.update m.cameraModel a }

let cam = 
    Camera.create (CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI) (Frustum.perspective 60.0 0.1 10.0 1.0)

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI

let threeD (m : MModel) =

    let t =
        adaptive {
            let! t = m.value
            return Trafo3d.RotationZ(t * 0.1)
        }

    let sg =
        Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
        |> Sg.noEvents
        |> Sg.pickable (PickShape.Box Box3d.Unit)       
        |> Sg.trafo t
        |> Sg.withEvents [
                Sg.onMouseDown (fun _ _ -> Inc)
          ]
        |> Sg.effect [
                    toEffect DefaultSurfaces.trafo
                    //toEffect DefaultSurfaces.diffuseTexture
                    toEffect <| DefaultSurfaces.constantColor C4f.Red 
                ]
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
    Demo.CameraController.controlledControl m.cameraModel CameraAction
        (Mod.constant frustum) 
        (AttributeMap.ofList [ attribute "style" "width:70%; height: 100%"]) sg

let view (m : MModel) =
    let s =
        adaptive {
            let! v = m.value
            return string v
        }
    printfn "exectued some things..."
    div [] [
        text "constant text"
        br []
        Incremental.text s
        //text (Mod.force s)
        br []
        button [onMouseClick (fun _ -> Inc)] [text "inc"]
        button [onMouseClick (fun _ -> Dec)] [text "dec"]
        br []
        threeD m
    ]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun (model : Model) -> Demo.CameraController.threads model.cameraModel |> ThreadPool.map CameraAction
        initial = { value = 1.0; cameraModel = Demo.CameraController.initial }
        update = update
        view = view
    }

let start() = App.start app