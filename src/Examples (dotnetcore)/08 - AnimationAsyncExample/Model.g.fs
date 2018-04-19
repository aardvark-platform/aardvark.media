namespace AnimationModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open AnimationModel

[<AutoOpen>]
module Mutable =

    
    
    type MTaskProgress(__initial : AnimationModel.TaskProgress) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<AnimationModel.TaskProgress> = Aardvark.Base.Incremental.EqModRef<AnimationModel.TaskProgress>(__initial) :> Aardvark.Base.Incremental.IModRef<AnimationModel.TaskProgress>
        let _percentage = ResetMod.Create(__initial.percentage)
        
        member x.percentage = _percentage :> IMod<_>
        member x.startTime = __current.Value.startTime
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : AnimationModel.TaskProgress) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_percentage,v.percentage)
                
        
        static member Create(__initial : AnimationModel.TaskProgress) : MTaskProgress = MTaskProgress(__initial)
        static member Update(m : MTaskProgress, v : AnimationModel.TaskProgress) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<AnimationModel.TaskProgress> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TaskProgress =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let percentage =
                { new Lens<AnimationModel.TaskProgress, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.percentage
                    override x.Set(r,v) = { r with percentage = v }
                    override x.Update(r,f) = { r with percentage = f r.percentage }
                }
            let startTime =
                { new Lens<AnimationModel.TaskProgress, System.DateTime>() with
                    override x.Get(r) = r.startTime
                    override x.Set(r,v) = { r with startTime = v }
                    override x.Update(r,f) = { r with startTime = f r.startTime }
                }
    
    
    type MModel(__initial : AnimationModel.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<AnimationModel.Model> = Aardvark.Base.Incremental.EqModRef<AnimationModel.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<AnimationModel.Model>
        let _animation = ResetMod.Create(__initial.animation)
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _animations = MList.Create(__initial.animations)
        let _pending = MOption.Create(__initial.pending)
        let _loadTasks = MSet.Create(__initial.loadTasks)
        let _progress = MMap.Create(__initial.progress, (fun v -> MTaskProgress.Create(v)), (fun (m,v) -> MTaskProgress.Update(m, v)), (fun v -> v))
        
        member x.animation = _animation :> IMod<_>
        member x.cameraState = _cameraState
        member x.animations = _animations :> alist<_>
        member x.pending = _pending :> IMod<_>
        member x.loadTasks = _loadTasks :> aset<_>
        member x.progress = _progress :> amap<_,_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : AnimationModel.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_animation,v.animation)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                MList.Update(_animations, v.animations)
                MOption.Update(_pending, v.pending)
                MSet.Update(_loadTasks, v.loadTasks)
                MMap.Update(_progress, v.progress)
                
        
        static member Create(__initial : AnimationModel.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : AnimationModel.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
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
            let pending =
                { new Lens<AnimationModel.Model, Microsoft.FSharp.Core.Option<AnimationModel.Pending>>() with
                    override x.Get(r) = r.pending
                    override x.Set(r,v) = { r with pending = v }
                    override x.Update(r,f) = { r with pending = f r.pending }
                }
            let loadTasks =
                { new Lens<AnimationModel.Model, Aardvark.Base.hset<AnimationModel.TaskId>>() with
                    override x.Get(r) = r.loadTasks
                    override x.Set(r,v) = { r with loadTasks = v }
                    override x.Update(r,f) = { r with loadTasks = f r.loadTasks }
                }
            let progress =
                { new Lens<AnimationModel.Model, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,AnimationModel.TaskProgress>>() with
                    override x.Get(r) = r.progress
                    override x.Set(r,v) = { r with progress = v }
                    override x.Update(r,f) = { r with progress = f r.progress }
                }
