namespace Aardvark.AnimationModel

open Aardvark.Base                 
open Aardvark.Base.Incremental 
open Aardvark.UI.Primitives

type Time = float
type RelativeTime = Time

type Animation<'m,'s,'a> = {
    state     : Option<'s>
    name      : string
    startTime : Option<Time>
    start     : 'm -> 's
    sample    : RelativeTime * Time -> 's -> Option<'s * 'a>
}

type Animate = On = 0 | Off = 1

type TaskId = string

[<DomainType>]
type TaskProgress = { percentage : float; [<NonIncremental>] startTime : System.DateTime }

[<DomainType>]
type Model = {
    cameraState : CameraControllerState
    animation : Animate
    animations  : plist<Animation<Model,CameraView,CameraView>>   
}
and Message =
    | CameraMessage of CameraControllerMessage
    | Tick of Time
    | PushAnimation of Animation<Model,CameraView,CameraView>
    | RemoveAnimation of Index       