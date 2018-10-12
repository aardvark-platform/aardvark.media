namespace Model

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Message = 
    | Camera of FreeFlyController.Message
    | CenterScene
    | UpdateConfig of DockConfig
    | Undo
    | Redo
    | SetCullMode of CullMode
    | ToggleFill
    | SetFiles of list<string>

[<DomainType>]
type Model = 
    {
        [<NonIncremental>]
        past : Option<Model>

        [<NonIncremental>]
        future : Option<Model>

        cameraState : CameraControllerState

        cullMode : CullMode
        fill : bool

        dockConfig : DockConfig

        files : plist<string>
    }