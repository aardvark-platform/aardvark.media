namespace Aardvark.UI.Animation

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Animation

[<AutoOpen>]
module Mutable =

    
    
    type MTaskProgress(__initial : Aardvark.UI.Animation.TaskProgress) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Animation.TaskProgress> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Animation.TaskProgress>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Animation.TaskProgress>
        let _percentage = ResetMod.Create(__initial.percentage)
        
        member x.percentage = _percentage :> IMod<_>
        member x.startTime = __current.Value.startTime
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Animation.TaskProgress) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_percentage,v.percentage)
                
        
        static member Create(__initial : Aardvark.UI.Animation.TaskProgress) : MTaskProgress = MTaskProgress(__initial)
        static member Update(m : MTaskProgress, v : Aardvark.UI.Animation.TaskProgress) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Animation.TaskProgress> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TaskProgress =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let percentage =
                { new Lens<Aardvark.UI.Animation.TaskProgress, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.percentage
                    override x.Set(r,v) = { r with percentage = v }
                    override x.Update(r,f) = { r with percentage = f r.percentage }
                }
            let startTime =
                { new Lens<Aardvark.UI.Animation.TaskProgress, System.DateTime>() with
                    override x.Get(r) = r.startTime
                    override x.Set(r,v) = { r with startTime = v }
                    override x.Update(r,f) = { r with startTime = f r.startTime }
                }
    
    
    type MAnimationModel(__initial : Aardvark.UI.Animation.AnimationModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Animation.AnimationModel> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Animation.AnimationModel>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Animation.AnimationModel>
        let _cam = ResetMod.Create(__initial.cam)
        let _animation = ResetMod.Create(__initial.animation)
        let _animations = MList.Create(__initial.animations)
        
        member x.cam = _cam :> IMod<_>
        member x.animation = _animation :> IMod<_>
        member x.animations = _animations :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Animation.AnimationModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_cam,v.cam)
                ResetMod.Update(_animation,v.animation)
                MList.Update(_animations, v.animations)
                
        
        static member Create(__initial : Aardvark.UI.Animation.AnimationModel) : MAnimationModel = MAnimationModel(__initial)
        static member Update(m : MAnimationModel, v : Aardvark.UI.Animation.AnimationModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Animation.AnimationModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module AnimationModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let cam =
                { new Lens<Aardvark.UI.Animation.AnimationModel, Aardvark.Base.CameraView>() with
                    override x.Get(r) = r.cam
                    override x.Set(r,v) = { r with cam = v }
                    override x.Update(r,f) = { r with cam = f r.cam }
                }
            let animation =
                { new Lens<Aardvark.UI.Animation.AnimationModel, Aardvark.UI.Animation.Animate>() with
                    override x.Get(r) = r.animation
                    override x.Set(r,v) = { r with animation = v }
                    override x.Update(r,f) = { r with animation = f r.animation }
                }
            let animations =
                { new Lens<Aardvark.UI.Animation.AnimationModel, Aardvark.Base.plist<Aardvark.UI.Animation.Animation<Aardvark.UI.Animation.AnimationModel,Aardvark.Base.CameraView,Aardvark.Base.CameraView>>>() with
                    override x.Get(r) = r.animations
                    override x.Set(r,v) = { r with animations = v }
                    override x.Update(r,f) = { r with animations = f r.animations }
                }
