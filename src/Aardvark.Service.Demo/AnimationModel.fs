namespace AnimationModel

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

[<DomainType>]
type Model = {
    animation : Animate
    cameraState : CameraControllerState
    animations  : plist<Animation<Model,CameraView,CameraView>>
    pending : Option<Message>
}
and Message =
    | Tick of Time
    | PushAnimation of Animation<Model,CameraView,CameraView>
    | CameraMessage of CameraControllerMessage
    | RemoveAnimation of Index
    | TellMeMore
    | AskForMore