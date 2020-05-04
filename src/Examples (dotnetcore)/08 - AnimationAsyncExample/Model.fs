namespace AnimationModel

open Aardvark.Base                 
open FSharp.Data.Adaptive 
open Aardvark.UI.Primitives
open Adaptify

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

[<ModelType>]
type TaskProgress = { percentage : float; [<NonAdaptive>] startTime : System.DateTime }

[<ModelType>]
type Model = {
    animation : Animate
    cameraState : CameraControllerState
    animations  : IndexList<Animation<Model,CameraView,CameraView>>
    pending : Option<Pending>
    loadTasks : HashSet<TaskId>
    progress : HashMap<string,TaskProgress>
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