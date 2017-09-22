namespace AnimationModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open AnimationModel

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : AnimationModel.Model) =
        inherit obj()
        let mutable __current = __initial
        let _animation = ResetMod.Create(__initial.animation)
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _animations = MList.Create(__initial.animations)
        
        member x.animation = _animation :> IMod<_>
        member x.cameraState = _cameraState
        member x.animations = _animations :> alist<_>
        
        member x.Update(v : AnimationModel.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_animation,v.animation)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                MList.Update(_animations, v.animations)
                
        
        static member Create(__initial : AnimationModel.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : AnimationModel.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<AnimationModel.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let animation =
                { new Lens<AnimationModel.Model, AnimationModel.Animate>() with
                    override x.Get(r) = r.animation
                    override x.Set(r,v) = { r with animation = v }
                    override x.Update(r,f) = { r with animation = f r.animation }
                }
            let cameraState =
                { new Lens<AnimationModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
            let animations =
                { new Lens<AnimationModel.Model, Aardvark.Base.plist<AnimationModel.Animation<AnimationModel.Model,Aardvark.Base.CameraView,Aardvark.Base.CameraView>>>() with
                    override x.Get(r) = r.animations
                    override x.Set(r,v) = { r with animations = v }
                    override x.Update(r,f) = { r with animations = f r.animations }
                }
