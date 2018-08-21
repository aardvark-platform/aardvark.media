module App


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
            FreeFlyController.update' msg |> Lens.update Model.Lens.cameraState m 
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
        | Pong -> 
            printfn "got pong"
            { m with pending = None }
        | Ping -> 
            printfn "ping. sending pong to thread pool"
            { m with pending = Some { message = Pong; id = System.Guid.NewGuid() |> string } }
        | StartAsyncOperation -> 
            let name = System.Guid.NewGuid() |> string
            printfn "starting async operation: %s" name
            { m with loadTasks = HSet.add name m.loadTasks; progress = HMap.add name { percentage = 0.0; startTime = System.DateTime.Now } m.progress }
        | AsyncOperationComplete (name,result) -> 
            printfn "operation complete: %s, result was: %A" name result
            { m with loadTasks = HSet.remove name m.loadTasks; progress = HMap.remove name m.progress }
        | Message.Progress(name,percentage) ->    
            let update progress =
                match progress with
                    | None -> Log.warn "non existing task reported progress."; None // just cancelled task reported progress
                    | Some old -> Some { old with percentage = percentage }
            { m with progress = HMap.alter name update m.progress }
        | StopTask name -> 
            printfn "stopping task: %s" name
            { m with loadTasks = HSet.remove name m.loadTasks; progress = HMap.remove name m.progress }
    

let viewScene (m : MModel) =
    Sg.box ~~C4b.DarkRed ~~Box3d.Unit
      |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
       }

let view (m : MModel) =
    let doubleClick (callback : V3d -> seq<'msg>) =
        "ondblclick", AttributeValue.Event {
            clientSide = fun send id -> 
                "aardvark.getWorldPosition('" + id + "', {x : event.clientX, y: event.clientY }, function(d) { " + send id ["d"] + " })"
            serverSide = fun client id args -> 
                match args with
                    | h :: _ ->
                        let v : V3d = Pickler.json.UnPickleOfString h
                        Log.warn "wp: %A" v
                        callback v
                    | _ ->
                        Seq.empty
        }   

    let callback (v : V3d) =
        Seq.empty

    body [ style "background: #1B1C1E"] [
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                FreeFlyController.controlledControl m.cameraState CameraMessage (Frustum.perspective 60.0 0.1 100.0 1.0 |> Mod.constant) 
                    (AttributeMap.ofList [ 
                        attribute "style" "width:85%; height: 100%; float: left;";
                        attribute "data-samples" "8"
                        attribute "showFPS" "true"
                        doubleClick callback
                     ]) (viewScene m)

                div [style "width:15%; height: 100%; float:right"] [
                    Html.SemUi.stuffStack [
                        b [] [text "Camera actions:"]; br[]; 
                        
                        button [clazz "ui button"; onClick (fun _ ->  PushAnimation (CameraAnimations.zoom  1.0 "zoom in  (1s)"))] [text "Zoom in"]
                        button [clazz "ui button"; onClick (fun _ ->  PushAnimation (CameraAnimations.zoom -1.0 "zoom out (1s)"))] [text "Zoom away"]

                        br[]; br[]; b [] [text "Pending animations (click to abort)"]
                        Incremental.div AttributeMap.empty <| AList.mapi (fun i a ->
                            button [onClick (fun _ -> RemoveAnimation i)] [text a.name]
                        ) m.animations

                        button [clazz "ui button"; onClick (fun _ -> Ping)] [text "Tell me more"]
                        br []
                        button [clazz "ui button"; onClick (fun _ -> StartAsyncOperation)] [text "Start async operation"]

                        br[]; br[]; b [] [text "Pending operations (click to abort)"]

                        Incremental.div AttributeMap.empty <| AList.mapi (fun i (taskName:string,progress:MTaskProgress) ->
                            let text = progress.percentage |> Mod.map (fun percentage -> sprintf "task %A: %A" (taskName.Substring(0,3)) percentage)
                            button [clazz "ui button"; onClick (fun _ -> StopTask taskName)] [Incremental.text text]
                        ) (m.progress |> AMap.toASet |> ASet.sortBy (fun (n,p) -> p.startTime))
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

module ThreadPool =
    let unionMany xs = List.fold ThreadPool.union ThreadPool.empty xs

let threads (m : Model) = 
    // handling of continous camera animations (camera controller)
    let cameraAnimations = FreeFlyController.threads m.cameraState |> ThreadPool.map CameraMessage
       
    // handling of self messages (example)
    let pendingActions msg str =
        proclist {
            printfn "got ping, ponging back"
            yield msg
        }

    let pendingMessages =
        match m.pending with
            | None -> ThreadPool.empty
            | Some p -> 
                ThreadPool.add p.id (pendingActions p.message p.id)  ThreadPool.empty

    // handling of asynchronous load tasks
    let asynchronousLoadOperations =

        let task =
            async {
                let! _ = Async.OnCancel (fun _ -> printfn "other computation needs compensation code")
                return 2
            } |> Async.StartAsTask

        let asyncOperationWithProgress (taskName : string) =
            proclist {
                // optionally, we can run computations in separate threads....
                do! Async.SwitchToNewThread()
                let cnt = 1000
                // it is possible here to start other sub tasks...
                let! someOtherComputation = task
                do! Async.Sleep 100
                // cleanup code for cancellation needs to be registered via Async.OnCancel
                let! _ = Async.OnCancel (fun _ -> printfn "operation got interrupted...")
                let mutable iter = 0
                for i in 0 .. cnt do
                    do! Proc.Sleep 10
                    if i % 10 = 0 then 
                        let perc = (float i / float cnt)*100.0
                        let msg = sprintf "working on it: %f percent" perc
                        // we made progress here. send intermediate result to update
                        yield Progress(taskName,perc)
                    iter <- iter + 1
                // we are finished here.. send result to update
                yield AsyncOperationComplete (taskName, float iter)
            }

        HSet.fold (fun pool operationId -> 
            ThreadPool.add operationId (asyncOperationWithProgress operationId) pool
        ) ThreadPool.empty m.loadTasks

    // handling of continous animations
    let animations = 
        if shouldAnimate m then
            ThreadPool.add "timer" (time()) ThreadPool.empty
        else
            ThreadPool.empty

    // combining all threads
    ThreadPool.unionMany [
        cameraAnimations
        animations
        pendingMessages
        asynchronousLoadOperations
    ]


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               cameraState =  
                   { FreeFlyController.initial with 
                        view = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
                   }
               animations = PList.empty
               animation = Animate.On
               pending = None
               loadTasks = HSet.empty
               progress = HMap.empty
            }
        update = update 
        view = view
    }
