namespace Inc.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Inc.Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Inc.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.Model> = Aardvark.Base.Incremental.EqModRef<Inc.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.Model>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _cylinders = ResetMod.Create(__initial.cylinders)
        let _hitPoint = MOption.Create(__initial.hitPoint)
        let _isShift = ResetMod.Create(__initial.isShift)
        
        member x.camera = _camera
        member x.cylinders = _cylinders :> IMod<_>
        member x.hitPoint = _hitPoint :> IMod<_>
        member x.isShift = _isShift :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                ResetMod.Update(_cylinders,v.cylinders)
                MOption.Update(_hitPoint, v.hitPoint)
                ResetMod.Update(_isShift,v.isShift)
                
        
        static member Create(__initial : Inc.Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Inc.Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Inc.Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<Inc.Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let cylinders =
                { new Lens<Inc.Model.Model, Aardvark.Base.Cylinder3d[]>() with
                    override x.Get(r) = r.cylinders
                    override x.Set(r,v) = { r with cylinders = v }
                    override x.Update(r,f) = { r with cylinders = f r.cylinders }
                }
            let hitPoint =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.hitPoint
                    override x.Set(r,v) = { r with hitPoint = v }
                    override x.Update(r,f) = { r with hitPoint = f r.hitPoint }
                }
            let isShift =
                { new Lens<Inc.Model.Model, System.Boolean>() with
                    override x.Get(r) = r.isShift
                    override x.Set(r,v) = { r with isShift = v }
                    override x.Update(r,f) = { r with isShift = f r.isShift }
                }
