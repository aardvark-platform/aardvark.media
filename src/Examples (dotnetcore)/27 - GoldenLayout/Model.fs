namespace Golden.Model

open Aardvark.UI.Primitives
open Adaptify

type Message =
    | Camera of FreeFlyController.Message
    | CenterScene
    | LayoutChanged
    | GoldenLayout of Golden.GoldenLayout.Message

[<ModelType>]
type Model =
    {
        title : string
        cameraState : CameraControllerState
        golden : Golden.GoldenLayout
    }