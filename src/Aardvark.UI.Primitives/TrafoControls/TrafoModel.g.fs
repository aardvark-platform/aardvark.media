namespace Aardvark.UI.Trafos

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Trafos

[<AutoOpen>]
module Mutable =

    
    
    type MTransformation(__initial : Aardvark.UI.Trafos.Transformation) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Trafos.Transformation> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Trafos.Transformation>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Trafos.Transformation>
        let _workingPose = ResetMod.Create(__initial.workingPose)
        let _pose = ResetMod.Create(__initial.pose)
        let _previewTrafo = ResetMod.Create(__initial.previewTrafo)
        let _scale = ResetMod.Create(__initial.scale)
        let _preTransform = ResetMod.Create(__initial.preTransform)
        let _mode = ResetMod.Create(__initial.mode)
        let _hovered = MOption.Create(__initial.hovered)
        let _grabbed = MOption.Create(__initial.grabbed)
        
        member x.workingPose = _workingPose :> IMod<_>
        member x.pose = _pose :> IMod<_>
        member x.previewTrafo = _previewTrafo :> IMod<_>
        member x.scale = _scale :> IMod<_>
        member x.preTransform = _preTransform :> IMod<_>
        member x.mode = _mode :> IMod<_>
        member x.hovered = _hovered :> IMod<_>
        member x.grabbed = _grabbed :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Trafos.Transformation) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_workingPose,v.workingPose)
                ResetMod.Update(_pose,v.pose)
                ResetMod.Update(_previewTrafo,v.previewTrafo)
                ResetMod.Update(_scale,v.scale)
                ResetMod.Update(_preTransform,v.preTransform)
                ResetMod.Update(_mode,v.mode)
                MOption.Update(_hovered, v.hovered)
                MOption.Update(_grabbed, v.grabbed)
                
        
        static member Create(__initial : Aardvark.UI.Trafos.Transformation) : MTransformation = MTransformation(__initial)
        static member Update(m : MTransformation, v : Aardvark.UI.Trafos.Transformation) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Trafos.Transformation> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Transformation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let workingPose =
                { new Lens<Aardvark.UI.Trafos.Transformation, Aardvark.UI.Trafos.Pose>() with
                    override x.Get(r) = r.workingPose
                    override x.Set(r,v) = { r with workingPose = v }
                    override x.Update(r,f) = { r with workingPose = f r.workingPose }
                }
            let pose =
                { new Lens<Aardvark.UI.Trafos.Transformation, Aardvark.UI.Trafos.Pose>() with
                    override x.Get(r) = r.pose
                    override x.Set(r,v) = { r with pose = v }
                    override x.Update(r,f) = { r with pose = f r.pose }
                }
            let previewTrafo =
                { new Lens<Aardvark.UI.Trafos.Transformation, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.previewTrafo
                    override x.Set(r,v) = { r with previewTrafo = v }
                    override x.Update(r,f) = { r with previewTrafo = f r.previewTrafo }
                }
            let scale =
                { new Lens<Aardvark.UI.Trafos.Transformation, System.Double>() with
                    override x.Get(r) = r.scale
                    override x.Set(r,v) = { r with scale = v }
                    override x.Update(r,f) = { r with scale = f r.scale }
                }
            let preTransform =
                { new Lens<Aardvark.UI.Trafos.Transformation, Aardvark.UI.Trafos.Pose>() with
                    override x.Get(r) = r.preTransform
                    override x.Set(r,v) = { r with preTransform = v }
                    override x.Update(r,f) = { r with preTransform = f r.preTransform }
                }
            let mode =
                { new Lens<Aardvark.UI.Trafos.Transformation, Aardvark.UI.Trafos.TrafoMode>() with
                    override x.Get(r) = r.mode
                    override x.Set(r,v) = { r with mode = v }
                    override x.Update(r,f) = { r with mode = f r.mode }
                }
            let hovered =
                { new Lens<Aardvark.UI.Trafos.Transformation, Microsoft.FSharp.Core.Option<Aardvark.UI.Trafos.Axis>>() with
                    override x.Get(r) = r.hovered
                    override x.Set(r,v) = { r with hovered = v }
                    override x.Update(r,f) = { r with hovered = f r.hovered }
                }
            let grabbed =
                { new Lens<Aardvark.UI.Trafos.Transformation, Microsoft.FSharp.Core.Option<Aardvark.UI.Trafos.PickPoint>>() with
                    override x.Get(r) = r.grabbed
                    override x.Set(r,v) = { r with grabbed = v }
                    override x.Update(r,f) = { r with grabbed = f r.grabbed }
                }
