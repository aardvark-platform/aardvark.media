namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Model> = Aardvark.Base.Incremental.EqModRef<Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Model>
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _trafo = ResetMod.Create(__initial.trafo)
        let _animationEnabled = ResetMod.Create(__initial.animationEnabled)
        let _gpuLoad = Aardvark.UI.Mutable.MNumericInput.Create(__initial.gpuLoad)
        let _modLoad = Aardvark.UI.Mutable.MNumericInput.Create(__initial.modLoad)
        let _updateLoad = Aardvark.UI.Mutable.MNumericInput.Create(__initial.updateLoad)
        
        member x.cameraState = _cameraState
        member x.trafo = _trafo :> IMod<_>
        member x.animationEnabled = _animationEnabled :> IMod<_>
        member x.gpuLoad = _gpuLoad
        member x.modLoad = _modLoad
        member x.updateLoad = _updateLoad
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_trafo,v.trafo)
                ResetMod.Update(_animationEnabled,v.animationEnabled)
                Aardvark.UI.Mutable.MNumericInput.Update(_gpuLoad, v.gpuLoad)
                Aardvark.UI.Mutable.MNumericInput.Update(_modLoad, v.modLoad)
                Aardvark.UI.Mutable.MNumericInput.Update(_updateLoad, v.updateLoad)
                
        
        static member Create(__initial : Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let cameraState =
                { new Lens<Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
            let trafo =
                { new Lens<Model.Model, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.trafo
                    override x.Set(r,v) = { r with trafo = v }
                    override x.Update(r,f) = { r with trafo = f r.trafo }
                }
            let animationEnabled =
                { new Lens<Model.Model, System.Boolean>() with
                    override x.Get(r) = r.animationEnabled
                    override x.Set(r,v) = { r with animationEnabled = v }
                    override x.Update(r,f) = { r with animationEnabled = f r.animationEnabled }
                }
            let gpuLoad =
                { new Lens<Model.Model, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.gpuLoad
                    override x.Set(r,v) = { r with gpuLoad = v }
                    override x.Update(r,f) = { r with gpuLoad = f r.gpuLoad }
                }
            let modLoad =
                { new Lens<Model.Model, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.modLoad
                    override x.Set(r,v) = { r with modLoad = v }
                    override x.Update(r,f) = { r with modLoad = f r.modLoad }
                }
            let updateLoad =
                { new Lens<Model.Model, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.updateLoad
                    override x.Set(r,v) = { r with updateLoad = v }
                    override x.Update(r,f) = { r with updateLoad = f r.updateLoad }
                }
