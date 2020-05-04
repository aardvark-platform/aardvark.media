namespace Aardvark.UI.Animation

module AnimationApp = 
  open Aardvark.Base
  open FSharp.Data.Adaptive
  open FSharp.Data.Adaptive.Operators
  open Aardvark.Base.Rendering
  
  open Aardvark.UI
              
  let shouldAnimate (m : AnimationModel) =
        m.animation = Animate.On && IndexList.count m.animations > 0
    
  let updateAnimation (m : 'm) (t : Aardvark.UI.Animation.Time) (a : Animation<'m,'s,'a>) =
        match a.state with
            | None ->
                let s = a.start m
                { a with state = Some s; startTime = Some t}, 0.0, s
            | Some s -> a, t-a.startTime.Value, s

  let update (m : AnimationModel) (msg : AnimationAction ) =
    match msg with
    | Tick t when shouldAnimate m -> 
        match IndexList.tryAt 0 m.animations with
            | Some anim -> 
                // initialize animation (if needed)
                let (anim,localTime,state) = updateAnimation m t anim
                match anim.sample(localTime, t) state with 
                    | None -> { m with animations = IndexList.removeAt 0 m.animations } // animation stops, so pop it
                    | Some (s,cameraView) -> 
                        // feed in new state
                        let anim = { anim with state = Some s } 
                        // activate result in camera state
                        //{ m with cameraState = { m.cameraState with view = cameraView }; 
                        { m with cam = cameraView; animations = IndexList.setAt 0 anim m.animations }
            | None -> m
    | PushAnimation a -> 
        { m with animations = IndexList.add a m.animations }
    | RemoveAnimation i -> 
        { m with animations = IndexList.remove i m.animations }
    | Tick _ -> m // not allowed to animate      

  let totalTime = System.Diagnostics.Stopwatch.StartNew()

  let rec time() =
      proclist {
          do! Proc.Sleep 10
          yield Tick totalTime.Elapsed.TotalSeconds
          yield! time()
      }
    
  module ThreadPool =
      let unionMany xs = List.fold ThreadPool.union ThreadPool.empty xs
    
      let threads (m : AnimationModel) =                                        
        // handling of continous animations
        
        if shouldAnimate m then
          ThreadPool.add "timer" (time()) ThreadPool.empty
        else
          ThreadPool.empty
        
        