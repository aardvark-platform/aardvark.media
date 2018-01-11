module AnimationApp


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering

open Aardvark.UI
open Aardvark.UI.Primitives

open AnimationModel

// TODO: head tail patterns for plist
// update at for plist


module Lens =
    let update (l : Lens<'m,'a>) (s : 'm) (f : 'a -> 'a) =
        l.Update(s,f)


let shouldAnimate (m : Model) =
    m.animation = Animate.On && PList.count m.animations > 0

let updateAnimation (m : 'm) (t : Time) (a : Animation<'m,'s,'a>) =
    match a.state with
        | None ->
            let s = a.start m
            { a with state = Some s; startTime = Some t}, 0.0, s
        | Some s -> a, t-a.startTime.Value, s

module CameraAnimations =
    let zoom (dir : float) (name : string) = 
        {
            startTime = None
            state = None
            name = name
            start = fun m -> m.cameraState.view // initially, grab camera controller state
            sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                if localTime < 1.0 then
                    let view = state.WithLocation(state.Location + 
                                                  dir * state.Forward * (localTime / 1.0))
                    Some (state,view)
                else None
        }

let update (m : Model) (msg : Message )  =
    match msg with
        | CameraMessage msg when not (shouldAnimate m) -> 
            CameraController.update' msg |> Lens.update Model.Lens.cameraState m 
        | Tick t when shouldAnimate m -> 
            match PList.tryAt 0 m.animations with
                | Some anim -> 
                    // initialize animation (if needed)
                    let (anim,localTime,state) = updateAnimation m t anim
                    match anim.sample(localTime, t) state with 
                        | None -> { m with animations = PList.removeAt 0 m.animations } // animation stops, so pop it
                        | Some (s,cameraView) -> 
                            // feed in new state
                            let anim = { anim with state = Some s } 
                            // activate result in camera state
                            { m with cameraState = { m.cameraState with view = cameraView }; 
                                     animations = PList.setAt 0 anim m.animations }
                | None -> m
        | PushAnimation a -> 
            { m with animations = PList.append a m.animations }
        | RemoveAnimation i -> 
            { m with animations = PList.remove i m.animations }
        | Tick _ -> m // not allowed to animate
        | CameraMessage _ -> m // not allowed to camera around
        | TellMeMore -> 
            printfn "tell me more tell me more..."
            { m with pending = None }
        | AskForMore -> 
            printfn "asked for more"
            { m with pending = Some TellMeMore }
    

let viewScene (m : MModel) =
    Sg.box ~~C4b.DarkRed ~~Box3d.Unit
      |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
       }

let view (m : MModel) =
    body [ style "background: #1B1C1E"] [
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl m.cameraState CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ attribute "style" "width:85%; height: 100%; float: left;"]) (viewScene m)

                div [style "width:15%; height: 100%; float:right"] [
                    Html.SemUi.stuffStack [
                        b [] [text "Camera actions:"]; br[]; 
                        
                        button [clazz "ui button"; onClick (fun _ ->  PushAnimation (CameraAnimations.zoom  1.0 "zoom in  (1s)"))] [text "Zoom in"]
                        button [clazz "ui button"; onClick (fun _ ->  PushAnimation (CameraAnimations.zoom -1.0 "zoom out (1s)"))] [text "Zoom away"]

                        br[]; br[]; b [] [text "Pending animations (click to abort)"]
                        Incremental.div AttributeMap.empty <| AList.mapi (fun i a ->
                            button [onClick (fun _ -> RemoveAnimation i)] [text a.name]
                        ) m.animations

                        button [onClick (fun _ -> AskForMore)] [text "Tell me more"]
                    ]
                ]
            ]
        )
    ]


let totalTime = System.Diagnostics.Stopwatch.StartNew()
let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Tick totalTime.Elapsed.TotalSeconds
        yield! time()
    }

let threads (m : Model) = 
    let cameraAnimations = CameraController.threads m.cameraState |> ThreadPool.map CameraMessage
       
    let pendingActions =
        proclist {
            match m.pending with
                | None -> ()
                | Some a -> 
                    printfn "starting"
                    let heavyComputation =
                        async {
                            do! Async.SwitchToThreadPool()
                            let mutable a = 0
                            let mutable cnt = 10000000
                            do 
                                for i in 0 .. cnt do 
                                    if i % 10000 = 0 then printfn "%f percent" ((float i / float cnt)*100.0)
                                    a <- a + 1
                            return a
                        }
                    let! result = Proc.Await heavyComputation
                    printfn "done: %A" result
                    yield a
        }

    let pendingActions = ThreadPool.add (System.Guid.NewGuid() |> string) pendingActions cameraAnimations

    if shouldAnimate m then
        ThreadPool.add "timer" (time()) pendingActions
    else
        pendingActions


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState =  
                   { CameraController.initial with 
                        view = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
                   }
               animations = PList.empty
               animation = Animate.On
               pending = None
            }
        update = update 
        view = view
    }
