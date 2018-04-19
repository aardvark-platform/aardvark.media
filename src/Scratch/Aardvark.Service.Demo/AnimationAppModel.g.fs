namespace AnimationDemo

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open AnimationDemo

[<AutoOpen>]
module Mutable =

    
    
    type MDemoModel(__initial : AnimationDemo.DemoModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<AnimationDemo.DemoModel> = Aardvark.Base.Incremental.EqModRef<AnimationDemo.DemoModel>(__initial) :> Aardvark.Base.Incremental.IModRef<AnimationDemo.DemoModel>
        let _animations = Aardvark.UI.Animation.Mutable.MAnimationModel.Create(__initial.animations)
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        
        member x.animations = _animations
        member x.cameraState = _cameraState
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : AnimationDemo.DemoModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Animation.Mutable.MAnimationModel.Update(_animations, v.animations)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                
        
        static member Create(__initial : AnimationDemo.DemoModel) : MDemoModel = MDemoModel(__initial)
        static member Update(m : MDemoModel, v : AnimationDemo.DemoModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<AnimationDemo.DemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let animations =
                { new Lens<AnimationDemo.DemoModel, Aardvark.UI.Animation.AnimationModel>() with
                    override x.Get(r) = r.animations
                    override x.Set(r,v) = { r with animations = v }
                    override x.Update(r,f) = { r with animations = f r.animations }
                }
            let cameraState =
                { new Lens<AnimationDemo.DemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
