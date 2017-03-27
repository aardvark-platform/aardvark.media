namespace Viewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MViewerModel private(__initial : Viewer.ViewerModel) =
        let mutable __current = __initial
        let _files = ResetMod(__initial.files)
        let _rotation = ResetMod(__initial.rotation)
        let _scenes = ResetSet(__initial.scenes)
        let _bounds = ResetMod(__initial.bounds)
        let _camera = Demo.TestApp.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.files = _files :> IMod<_>
        member x.rotation = _rotation :> IMod<_>
        member x.scenes = _scenes :> aset<_>
        member x.bounds = _bounds :> IMod<_>
        member x.camera = _camera
        
        member x.Update(__model : Viewer.ViewerModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _files.Update(__model.files)
                _rotation.Update(__model.rotation)
                _scenes.Update(__model.scenes)
                _bounds.Update(__model.bounds)
                _camera.Update(__model.camera)
        
        static member Create(initial) = MViewerModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MViewerModel =
        let inline files (m : MViewerModel) = m.files
        let inline rotation (m : MViewerModel) = m.rotation
        let inline scenes (m : MViewerModel) = m.scenes
        let inline bounds (m : MViewerModel) = m.bounds
        let inline camera (m : MViewerModel) = m.camera
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ViewerModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let files =
                { new Lens<Viewer.ViewerModel, Microsoft.FSharp.Collections.list<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.files
                    override x.Set(r,v) = { r with files = v }
                    override x.Update(r,f) = { r with files = f r.files }
                }
            let rotation =
                { new Lens<Viewer.ViewerModel, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.rotation
                    override x.Set(r,v) = { r with rotation = v }
                    override x.Update(r,f) = { r with rotation = f r.rotation }
                }
            let scenes =
                { new Lens<Viewer.ViewerModel, Aardvark.Base.hset<Aardvark.UI.ISg<Viewer.Message>>>() with
                    override x.Get(r) = r.scenes
                    override x.Set(r,v) = { r with scenes = v }
                    override x.Update(r,f) = { r with scenes = f r.scenes }
                }
            let bounds =
                { new Lens<Viewer.ViewerModel, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.bounds
                    override x.Set(r,v) = { r with bounds = v }
                    override x.Update(r,f) = { r with bounds = f r.bounds }
                }
            let camera =
                { new Lens<Viewer.ViewerModel, Demo.TestApp.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
