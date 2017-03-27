namespace Viewer

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Demo.TestApp
open Demo

type Message =
    | OpenFile of list<string>
    | TimeElapsed
    | Import
    | Cancel
    | CameraMessage of CameraController.Message

[<DomainType>]
type ViewerModel =
    {
        files : list<string>
        rotation : float
        scenes : hset<ISg<Message>>
        bounds : Box3d
        camera : Demo.TestApp.CameraControllerState
    }
