namespace RenderControl.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open RenderControl.Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : RenderControl.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<RenderControl.Model.Model> = Aardvark.Base.Incremental.EqModRef<RenderControl.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<RenderControl.Model.Model>
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _rot = ResetMod.Create(__initial.rot)
        
        member x.cameraState = _cameraState
        member x.rot = _rot :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : RenderControl.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_rot,v.rot)
                
        
        static member Create(__initial : RenderControl.Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : RenderControl.Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<RenderControl.Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let cameraState =
                { new Lens<RenderControl.Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
            let rot =
                { new Lens<RenderControl.Model.Model, System.Double>() with
                    override x.Get(r) = r.rot
                    override x.Set(r,v) = { r with rot = v }
                    override x.Update(r,f) = { r with rot = f r.rot }
                }
