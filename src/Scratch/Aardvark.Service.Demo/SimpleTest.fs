module SimpleTestApp

open SimpleTest
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

type Action = 
    | Inc 
    | Dec
    | Toggle
    | CameraAction of CameraController.Message


let update (m : Model) (a : Action) =
    match a with
        | Inc -> { m with value = m.value + 1.0 }
        | Dec -> { m with value = m.value - 1.0 } 
        | Toggle -> { m with sphereFirst = not m.sphereFirst}
        | CameraAction a -> { m with cameraModel = CameraController.update m.cameraModel a }

let cam = 
    Camera.create (CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI) (Frustum.perspective 60.0 0.1 10.0 1.0)


let threeD (m : MModel) =

    let t =
        adaptive {
            let! t = m.value
            return Trafo3d.RotationZ(t * 0.1)
        }

    let sg =
        Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
        |> Sg.requirePicking
        |> Sg.noEvents
        //|> Sg.pickable (PickShape.Box Box3d.Unit)       
        |> Sg.trafo t
        |> Sg.withEvents [
                Sg.onMouseDown (fun _ _ -> Inc)
          ]
        |> Sg.effect [
                    toEffect DefaultSurfaces.trafo
                    //toEffect DefaultSurfaces.diffuseTexture
                    toEffect <| DefaultSurfaces.vertexColor
                ]

    let other = 
        Sg.sphere' 5 C4b.Red 1.0 |> Sg.noEvents |> Sg.effect [ toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor]
            

    let frustum = Frustum.perspective 60.0 0.1 100.0 1.0

    let att = AttributeMap.ofList [ attribute "style" "width:70%; height: 70%"]
    let control = Incremental.renderControl (AVal.constant Unchecked.defaultof<_>) att sg

    let cmds = 
        alist {
            let! sphereFirst = m.sphereFirst
            if not sphereFirst then
                yield SceneGraph sg
                yield Clear(None,Some (AVal.constant 1.0))
                yield SceneGraph other
            else
                yield SceneGraph other
                yield Clear(None,Some (AVal.constant 1.0))
                yield SceneGraph sg
        }

    let control = DomNode.RenderControl(att,(AVal.constant Unchecked.defaultof<_>),cmds,None)

    CameraController.withControls m.cameraModel CameraAction (AVal.constant frustum) control


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
        //text (AVal.force s)
        br []
        button [onMouseClick (fun _ -> Inc)] [text "inc"]
        button [onMouseClick (fun _ -> Dec)] [text "dec"]
        div [] [text "sphereFirst: ";  Html.SemUi.toggleBox m.sphereFirst Toggle]
        br []
        threeD m
    ]

let app =
    {
        unpersist = Unpersist.instance
        threads = fun (model : Model) -> CameraController.threads model.cameraModel |> ThreadPool.map CameraAction
        initial = { value = 1.0; cameraModel = CameraController.initial; sphereFirst = true }
        update = update
        view = view
    }

let start() = App.start app