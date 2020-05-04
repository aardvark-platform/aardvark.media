namespace Model

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Primitives
open Adaptify

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | UpdateConfig of DockConfig
    | Undo
    | Redo    
    | SetCullMode of CullMode
    | ToggleFill
    | SetFiles of list<string>

[<ModelType>]
type Model = 
    {
        [<NonAdaptive>]
        past : Option<Model>

        [<NonAdaptive>]
        future : Option<Model>

        cameraState : CameraControllerState

        cullMode : CullMode
        fill : bool

        dockConfig : DockConfig

        files : IndexList<string>
    }