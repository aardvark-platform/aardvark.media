namespace Viewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Viewer

[<AutoOpen>]
module Mutable =

    
    
    type MViewerModel(__initial : Viewer.ViewerModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.ModRef<Viewer.ViewerModel> = Aardvark.Base.Incremental.Mod.init(__initial)
        let _files = ResetMod.Create(__initial.files)
        let _rotation = ResetMod.Create(__initial.rotation)
        let _scenes = MSet.Create(__initial.scenes)
        let _bounds = ResetMod.Create(__initial.bounds)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        
        member x.files = _files :> IMod<_>
        member x.rotation = _rotation :> IMod<_>
        member x.scenes = _scenes :> aset<_>
        member x.bounds = _bounds :> IMod<_>
        member x.camera = _camera
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Viewer.ViewerModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_files,v.files)
                ResetMod.Update(_rotation,v.rotation)
                MSet.Update(_scenes, v.scenes)
                ResetMod.Update(_bounds,v.bounds)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_cullMode,v.cullMode)
                
        
        static member Create(__initial : Viewer.ViewerModel) : MViewerModel = MViewerModel(__initial)
        static member Update(m : MViewerModel, v : Viewer.ViewerModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Viewer.ViewerModel> with
            member x.Update v = x.Update v
    
    
    
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
                { new Lens<Viewer.ViewerModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let fillMode =
                { new Lens<Viewer.ViewerModel, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let cullMode =
                { new Lens<Viewer.ViewerModel, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
