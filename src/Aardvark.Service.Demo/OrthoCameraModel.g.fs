namespace OrthoCamera

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open OrthoCamera

[<AutoOpen>]
module Mutable =

    module OrthoCameraModel =
        open OrthoCameraModel
        
        
        
        type MOrthoModel(__initial : OrthoCamera.OrthoCameraModel.OrthoModel) =
            inherit obj()
            let mutable __current : Aardvark.Base.Incremental.IModRef<OrthoCamera.OrthoCameraModel.OrthoModel> = Aardvark.Base.Incremental.EqModRef<OrthoCamera.OrthoCameraModel.OrthoModel>(__initial) :> Aardvark.Base.Incremental.IModRef<OrthoCamera.OrthoCameraModel.OrthoModel>
            let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
            
            member x.camera = _camera
            
            member x.Current = __current :> IMod<_>
            member x.Update(v : OrthoCamera.OrthoCameraModel.OrthoModel) =
                if not (System.Object.ReferenceEquals(__current.Value, v)) then
                    __current.Value <- v
                    
                    Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                    
            
            static member Create(__initial : OrthoCamera.OrthoCameraModel.OrthoModel) : MOrthoModel = MOrthoModel(__initial)
            static member Update(m : MOrthoModel, v : OrthoCamera.OrthoCameraModel.OrthoModel) = m.Update(v)
            
            override x.ToString() = __current.Value.ToString()
            member x.AsString = sprintf "%A" __current.Value
            interface IUpdatable<OrthoCamera.OrthoCameraModel.OrthoModel> with
                member x.Update v = x.Update v
        
        
        
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module OrthoModel =
            [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
            module Lens =
                let camera =
                    { new Lens<OrthoCamera.OrthoCameraModel.OrthoModel, Aardvark.UI.Primitives.CameraControllerState>() with
                        override x.Get(r) = r.camera
                        override x.Set(r,v) = { r with camera = v }
                        override x.Update(r,f) = { r with camera = f r.camera }
                    }
