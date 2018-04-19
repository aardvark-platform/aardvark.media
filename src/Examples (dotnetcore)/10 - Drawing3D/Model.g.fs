namespace DrawingModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DrawingModel

[<AutoOpen>]
module Mutable =

    
    
    type MRenderingParameters(__initial : DrawingModel.RenderingParameters) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawingModel.RenderingParameters> = Aardvark.Base.Incremental.EqModRef<DrawingModel.RenderingParameters>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawingModel.RenderingParameters>
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawingModel.RenderingParameters) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_cullMode,v.cullMode)
                
        
        static member Create(__initial : DrawingModel.RenderingParameters) : MRenderingParameters = MRenderingParameters(__initial)
        static member Update(m : MRenderingParameters, v : DrawingModel.RenderingParameters) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawingModel.RenderingParameters> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RenderingParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let fillMode =
                { new Lens<DrawingModel.RenderingParameters, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let cullMode =
                { new Lens<DrawingModel.RenderingParameters, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
    
    
    type MSimpleDrawingAppModel(__initial : DrawingModel.SimpleDrawingAppModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawingModel.SimpleDrawingAppModel> = Aardvark.Base.Incremental.EqModRef<DrawingModel.SimpleDrawingAppModel>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawingModel.SimpleDrawingAppModel>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _points = ResetMod.Create(__initial.points)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.points = _points :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawingModel.SimpleDrawingAppModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                ResetMod.Update(_points,v.points)
                
        
        static member Create(__initial : DrawingModel.SimpleDrawingAppModel) : MSimpleDrawingAppModel = MSimpleDrawingAppModel(__initial)
        static member Update(m : MSimpleDrawingAppModel, v : DrawingModel.SimpleDrawingAppModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawingModel.SimpleDrawingAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SimpleDrawingAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<DrawingModel.SimpleDrawingAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<DrawingModel.SimpleDrawingAppModel, DrawingModel.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let draw =
                { new Lens<DrawingModel.SimpleDrawingAppModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<DrawingModel.SimpleDrawingAppModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let points =
                { new Lens<DrawingModel.SimpleDrawingAppModel, Microsoft.FSharp.Collections.list<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
