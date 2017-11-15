namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI

[<AutoOpen>]
module Mutable =

    
    
    type MTransformation(__initial : Aardvark.UI.Transformation) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Transformation> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Transformation>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Transformation>
        let _workingPose = ResetMod.Create(__initial.workingPose)
        let _pose = ResetMod.Create(__initial.pose)
        let _previewTrafo = ResetMod.Create(__initial.previewTrafo)
        let _mode = ResetMod.Create(__initial.mode)
        let _hovered = MOption.Create(__initial.hovered)
        let _grabbed = MOption.Create(__initial.grabbed)
        
        member x.workingPose = _workingPose :> IMod<_>
        member x.pose = _pose :> IMod<_>
        member x.previewTrafo = _previewTrafo :> IMod<_>
        member x.mode = _mode :> IMod<_>
        member x.hovered = _hovered :> IMod<_>
        member x.grabbed = _grabbed :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Transformation) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_workingPose,v.workingPose)
                ResetMod.Update(_pose,v.pose)
                ResetMod.Update(_previewTrafo,v.previewTrafo)
                ResetMod.Update(_mode,v.mode)
                MOption.Update(_hovered, v.hovered)
                MOption.Update(_grabbed, v.grabbed)
                
        
        static member Create(__initial : Aardvark.UI.Transformation) : MTransformation = MTransformation(__initial)
        static member Update(m : MTransformation, v : Aardvark.UI.Transformation) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Transformation> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Transformation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let workingPose =
                { new Lens<Aardvark.UI.Transformation, Aardvark.UI.Pose>() with
                    override x.Get(r) = r.workingPose
                    override x.Set(r,v) = { r with workingPose = v }
                    override x.Update(r,f) = { r with workingPose = f r.workingPose }
                }
            let pose =
                { new Lens<Aardvark.UI.Transformation, Aardvark.UI.Pose>() with
                    override x.Get(r) = r.pose
                    override x.Set(r,v) = { r with pose = v }
                    override x.Update(r,f) = { r with pose = f r.pose }
                }
            let previewTrafo =
                { new Lens<Aardvark.UI.Transformation, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.previewTrafo
                    override x.Set(r,v) = { r with previewTrafo = v }
                    override x.Update(r,f) = { r with previewTrafo = f r.previewTrafo }
                }
            let mode =
                { new Lens<Aardvark.UI.Transformation, Aardvark.UI.TrafoMode>() with
                    override x.Get(r) = r.mode
                    override x.Set(r,v) = { r with mode = v }
                    override x.Update(r,f) = { r with mode = f r.mode }
                }
            let hovered =
                { new Lens<Aardvark.UI.Transformation, Microsoft.FSharp.Core.Option<Aardvark.UI.Axis>>() with
                    override x.Get(r) = r.hovered
                    override x.Set(r,v) = { r with hovered = v }
                    override x.Update(r,f) = { r with hovered = f r.hovered }
                }
            let grabbed =
                { new Lens<Aardvark.UI.Transformation, Microsoft.FSharp.Core.Option<Aardvark.UI.PickPoint>>() with
                    override x.Get(r) = r.grabbed
                    override x.Set(r,v) = { r with grabbed = v }
                    override x.Update(r,f) = { r with grabbed = f r.grabbed }
                }
