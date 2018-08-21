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

type TaskId = string

[<DomainType>]
type TaskProgress = { percentage : float; [<NonIncremental>] startTime : System.DateTime }

[<DomainType>]
type Model = {
    animation : Animate
    cameraState : CameraControllerState
    animations  : plist<Animation<Model,CameraView,CameraView>>
    pending : Option<Pending>
    loadTasks : hset<TaskId>
    progress : hmap<string,TaskProgress>
}
and Message =
    | Tick of Time
    | PushAnimation of Animation<Model,CameraView,CameraView>
    | CameraMessage of FreeFlyController.Message
    | RemoveAnimation of Index
    | Ping
    | Pong
    | StartAsyncOperation
    | AsyncOperationComplete of TaskId * float
    | Progress of TaskId * float
    | StopTask of TaskId

and Pending = { message : Message; id : string}