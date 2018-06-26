namespace Examples.MultiviewModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Examples.MultiviewModel

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Examples.MultiviewModel.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Examples.MultiviewModel.Model> = Aardvark.Base.Incremental.EqModRef<Examples.MultiviewModel.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Examples.MultiviewModel.Model>
        let _camera1 = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera1)
        let _camera2 = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera2)
        let _camera3 = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera3)
        
        member x.camera1 = _camera1
        member x.camera2 = _camera2
        member x.camera3 = _camera3
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Examples.MultiviewModel.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera1, v.camera1)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera2, v.camera2)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera3, v.camera3)
                
        
        static member Create(__initial : Examples.MultiviewModel.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Examples.MultiviewModel.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Examples.MultiviewModel.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera1 =
                { new Lens<Examples.MultiviewModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera1
                    override x.Set(r,v) = { r with camera1 = v }
                    override x.Update(r,f) = { r with camera1 = f r.camera1 }
                }
            let camera2 =
                { new Lens<Examples.MultiviewModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera2
                    override x.Set(r,v) = { r with camera2 = v }
                    override x.Update(r,f) = { r with camera2 = f r.camera2 }
                }
            let camera3 =
                { new Lens<Examples.MultiviewModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera3
                    override x.Set(r,v) = { r with camera3 = v }
                    override x.Update(r,f) = { r with camera3 = f r.camera3 }
                }
