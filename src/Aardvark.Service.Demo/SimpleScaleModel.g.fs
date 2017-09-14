namespace SimpleScaleModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open SimpleScaleModel

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : SimpleScaleModel.Model) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = PRo3DModels.Mutable.MRenderingParameters.Create(__initial.rendering)
        let _scale = Aardvark.UI.Mutable.MV3dInput.Create(__initial.scale)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.scale = _scale
        
        member x.Update(v : SimpleScaleModel.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                PRo3DModels.Mutable.MRenderingParameters.Update(_rendering, v.rendering)
                Aardvark.UI.Mutable.MV3dInput.Update(_scale, v.scale)
                
        
        static member Create(__initial : SimpleScaleModel.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : SimpleScaleModel.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<SimpleScaleModel.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<SimpleScaleModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<SimpleScaleModel.Model, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let scale =
                { new Lens<SimpleScaleModel.Model, Aardvark.UI.V3dInput>() with
                    override x.Get(r) = r.scale
                    override x.Set(r,v) = { r with scale = v }
                    override x.Update(r,f) = { r with scale = f r.scale }
                }
