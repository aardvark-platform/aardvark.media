﻿namespace Aardvark.UI.Animation.Deprecated

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
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
type AnimationModel = {
   cam        : CameraView
   animation  : Animate
   animations : IndexList<Animation<AnimationModel,CameraView,CameraView>>
}

type AnimationAction = 
 | Tick of Time
 | PushAnimation of Animation<AnimationModel,CameraView,CameraView>
 | RemoveAnimation of Index   
        
module CameraAnimations =
    open Aardvark.Base.Trafo
    
    let initial name = 
        {
            startTime = None
            state = None
            name = name
            start = fun m -> m.cam // initially, grab camera controller state
            sample = fun (_,_) (state : CameraView) -> None
        }
    
    let zoom (dir : float) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < 1.0 then
                        let view = state.WithLocation(state.Location + dir * state.Forward * (localTime / 1.0))
                        Some (state,view)
                    else None
        }
    
    let animateLocation (destination : V3d) (duration : RelativeTime) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < duration then
                        let vec      = destination - state.Location
                        let velocity = vec.Length / duration
                        let dir      = vec.Normalized
                        
                        let location' = state.Location + dir * velocity * localTime
                        let view = state.WithLocation(location')
                        
                        Some (state,view)
                    else 
                        None
        }
    
    let animateLocationFixedLookAt (destination : V3d) (lookAt : V3d) (duration : RelativeTime) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < duration then
                        let vec      = destination - state.Location
                        let velocity = vec.Length / duration                  
                        let dir      = vec.Normalized                  
        
                        let location' = state.Location + dir * velocity * localTime
                        let forward = (lookAt - location').Normalized
        
                        let view = 
                            state 
                            |> CameraView.withLocation(location') 
                            |> CameraView.withForward forward
        
                        Some (state,view)
                    else None
        }
    
    let animateLookAt (src : V3d)(dst : V3d) (duration : RelativeTime) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < duration then
                        let vec      = dst - src
                        let velocity = vec.Length / duration
                        let dir      = vec.Normalized
        
                        let lookAt' = src + dir * velocity * localTime
                        let forward = (lookAt' - state.Location).Normalized
        
                        let view = state |> CameraView.withForward forward                  
                                        
                        Some (state,view)
                    else 
                        None
        }
    
    let animateFoward (dst : V3d) (duration : RelativeTime) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < duration then                  
                        let rot = Rot3d.RotateInto(state.Forward, dst) * localTime / duration
                        let forward' = Rot3d(rot).Transform state.Forward               
                        let view = state |> CameraView.withForward forward'
        
                        Some (state,view)
                    else 
                        None
        }
    
    let animateSky (dst : V3d) (duration : RelativeTime) (name : string) = 
        {
            (initial name) with 
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview
                    if localTime < duration then
                        let rot = Rot3d.RotateInto(state.Up, dst) * localTime / duration
                        let sky' = Rot3d(rot).Transform(state.Up)
                        let view = state |> CameraView.withUp sky'
        
                        Some (state,view)
                    else 
                        None
        }

    let private transformLocationForwardUp (pos: V3d) (dir: V3d) (up:V3d) (duration : RelativeTime) (localTime : RelativeTime) (state : CameraView) =
        let rot      = Rot3d.RotateInto(state.Forward, dir) * localTime / duration |> Rot3d |> Trafo3d
        let forward  = rot.Forward.TransformDir state.Forward

        let uprot     = Rot3d.RotateInto(state.Up, up) * localTime / duration |> Rot3d |> Trafo3d
        let up        = uprot.Forward.TransformDir state.Up
                      
        let vec       = pos - state.Location
        let velocity  = vec.Length / duration                  
        let dir       = vec.Normalized
        let location  = state.Location + dir * velocity * localTime
         
        state 
        |> CameraView.withForward forward
        |> CameraView.withUp up
        |> CameraView.withLocation location

    let animateForwardAndLocation (pos: V3d) (dir: V3d) (up:V3d) (duration: RelativeTime) (name: string) = 
        {
            (initial name) with
                sample = fun (localTime, globalTime) (state : CameraView) -> // given the state and t since start of the animation, compute a state and the cameraview                    
                    let cameraChange = not (state.Location.ApproximateEquals(pos) && state.Forward.ApproximateEquals(dir) && state.Up.ApproximateEquals(up))                    

                    if cameraChange then
                        if localTime <= duration then
                            let view = transformLocationForwardUp pos dir up duration localTime state
                            Some (state, view)
                        else                                                
                            if state.Location.IsNaN |> not then
                                //on the last iteration set current position to endpoint - localtime = duration
                                let view = 
                                    transformLocationForwardUp pos dir up duration duration state

                                let state =
                                    state
                                    |> CameraView.withLocation V3d.NaN

                                Some (state , view)
                            else
                                None
                    else
                        None
        }