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
        let _floatValue = ResetMod.Create(__initial.floatValue)
        let _intValue = ResetMod.Create(__initial.intValue)
        let _stringValue = ResetMod.Create(__initial.stringValue)
        let _enumValue = ResetMod.Create(__initial.enumValue)
        
        member x.cameraState = _cameraState
        member x.floatValue = _floatValue :> IMod<_>
        member x.intValue = _intValue :> IMod<_>
        member x.stringValue = _stringValue :> IMod<_>
        member x.enumValue = _enumValue :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : RenderControl.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_floatValue,v.floatValue)
                ResetMod.Update(_intValue,v.intValue)
                ResetMod.Update(_stringValue,v.stringValue)
                ResetMod.Update(_enumValue,v.enumValue)
                
        
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
            let floatValue =
                { new Lens<RenderControl.Model.Model, System.Double>() with
                    override x.Get(r) = r.floatValue
                    override x.Set(r,v) = { r with floatValue = v }
                    override x.Update(r,f) = { r with floatValue = f r.floatValue }
                }
            let intValue =
                { new Lens<RenderControl.Model.Model, System.Int32>() with
                    override x.Get(r) = r.intValue
                    override x.Set(r,v) = { r with intValue = v }
                    override x.Update(r,f) = { r with intValue = f r.intValue }
                }
            let stringValue =
                { new Lens<RenderControl.Model.Model, System.String>() with
                    override x.Get(r) = r.stringValue
                    override x.Set(r,v) = { r with stringValue = v }
                    override x.Update(r,f) = { r with stringValue = f r.stringValue }
                }
            let enumValue =
                { new Lens<RenderControl.Model.Model, RenderControl.Model.EnumValue>() with
                    override x.Get(r) = r.enumValue
                    override x.Set(r,v) = { r with enumValue = v }
                    override x.Update(r,f) = { r with enumValue = f r.enumValue }
                }
