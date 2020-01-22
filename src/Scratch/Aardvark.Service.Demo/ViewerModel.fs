namespace Viewer

open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Demo.TestApp
open Demo

type Message =
    | OpenFile of list<string>
    | TimeElapsed
    | Import
    | Cancel
    | CameraMessage of CameraController.Message
    | SetFillMode of FillMode
    | SetCullMode of CullMode


[<ModelType>]
type ViewerModel =
    {

        files : list<string>
        rotation : float
        scenes : HashSet<ISg<Message>>
        bounds : Box3d
        camera : CameraControllerState
        fillMode : FillMode
        cullMode : CullMode
    }
