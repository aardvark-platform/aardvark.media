namespace Aardvark.UI.Animation

module AnimationApp = 
  open Aardvark.Base
  open Aardvark.Base.Incremental
  open Aardvark.Base.Incremental.Operators
  open Aardvark.Base.Rendering
  
  open Aardvark.UI
              
  let shouldAnimate (m : AnimationModel) =
        m.animation = Animate.On && PList.count m.animations > 0
    
  let updateAnimation (m : 'm) (t : Aardvark.UI.Animation.Time) (a : Animation<'m,'s,'a>) =
        match a.state with
            | None ->
                let s = a.start m
                { a with state = Some s; startTime = Some t}, 0.0, s
            | Some s -> a, t-a.startTime.Value, s

  let update (m : AnimationModel) (msg : AnimationAction ) =
    match msg with
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
                        //{ m with cameraState = { m.cameraState with view = cameraView }; 
                        { m with cam = cameraView; animations = PList.setAt 0 anim m.animations }
            | None -> m
    | PushAnimation a -> 
        { m with animations = PList.append a m.animations }
    | RemoveAnimation i -> 
        { m with animations = PList.remove i m.animations }
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
        
        