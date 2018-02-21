namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MCorrelationDrawingModel(__initial : CorrelationDrawing.CorrelationDrawingModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationDrawingModel> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.CorrelationDrawingModel>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationDrawingModel>
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _working = MOption.Create(__initial.working, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _projection = ResetMod.Create(__initial.projection)
        let _geometry = ResetMod.Create(__initial.geometry)
        let _semantics = MList.Create(__initial.semantics, (fun v -> MSemantic.Create(v)), (fun (m,v) -> MSemantic.Update(m, v)), (fun v -> v))
        let _selectedSemantic = MOption.Create(__initial.selectedSemantic, (fun v -> MSemantic.Create(v)), (fun (m,v) -> MSemantic.Update(m, v)), (fun v -> v))
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _exportPath = ResetMod.Create(__initial.exportPath)
        
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.working = _working :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.geometry = _geometry :> IMod<_>
        member x.semantics = _semantics :> alist<_>
        member x.selectedSemantic = _selectedSemantic :> IMod<_>
        member x.annotations = _annotations :> alist<_>
        member x.exportPath = _exportPath :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.CorrelationDrawingModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                MOption.Update(_working, v.working)
                ResetMod.Update(_projection,v.projection)
                ResetMod.Update(_geometry,v.geometry)
                MList.Update(_semantics, v.semantics)
                MOption.Update(_selectedSemantic, v.selectedSemantic)
                MList.Update(_annotations, v.annotations)
                ResetMod.Update(_exportPath,v.exportPath)
                
        
        static member Create(__initial : CorrelationDrawing.CorrelationDrawingModel) : MCorrelationDrawingModel = MCorrelationDrawingModel(__initial)
        static member Update(m : MCorrelationDrawingModel, v : CorrelationDrawing.CorrelationDrawingModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.CorrelationDrawingModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CorrelationDrawingModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let draw =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let working =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.Option<CorrelationDrawing.Annotation>>() with
                    override x.Get(r) = r.working
                    override x.Set(r,v) = { r with working = v }
                    override x.Update(r,f) = { r with working = f r.working }
                }
            let projection =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, CorrelationDrawing.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let geometry =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, CorrelationDrawing.GeometryType>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let semantics =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Aardvark.Base.plist<CorrelationDrawing.Semantic>>() with
                    override x.Get(r) = r.semantics
                    override x.Set(r,v) = { r with semantics = v }
                    override x.Update(r,f) = { r with semantics = f r.semantics }
                }
            let selectedSemantic =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.Option<CorrelationDrawing.Semantic>>() with
                    override x.Get(r) = r.selectedSemantic
                    override x.Set(r,v) = { r with selectedSemantic = v }
                    override x.Update(r,f) = { r with selectedSemantic = f r.selectedSemantic }
                }
            let annotations =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Aardvark.Base.plist<CorrelationDrawing.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
            let exportPath =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.exportPath
                    override x.Set(r,v) = { r with exportPath = v }
                    override x.Update(r,f) = { r with exportPath = f r.exportPath }
                }
    
    
    type MCorrelationAppModel(__initial : CorrelationDrawing.CorrelationAppModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationAppModel> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.CorrelationAppModel>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationAppModel>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _drawing = MCorrelationDrawingModel.Create(__initial.drawing)
        let _history = ResetMod.Create(__initial.history)
        let _future = ResetMod.Create(__initial.future)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.drawing = _drawing
        member x.history = _history :> IMod<_>
        member x.future = _future :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.CorrelationAppModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                MCorrelationDrawingModel.Update(_drawing, v.drawing)
                _history.Update(v.history)
                _future.Update(v.future)
                
        
        static member Create(__initial : CorrelationDrawing.CorrelationAppModel) : MCorrelationAppModel = MCorrelationAppModel(__initial)
        static member Update(m : MCorrelationAppModel, v : CorrelationDrawing.CorrelationAppModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.CorrelationAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CorrelationAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<CorrelationDrawing.CorrelationAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<CorrelationDrawing.CorrelationAppModel, CorrelationDrawing.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let drawing =
                { new Lens<CorrelationDrawing.CorrelationAppModel, CorrelationDrawing.CorrelationDrawingModel>() with
                    override x.Get(r) = r.drawing
                    override x.Set(r,v) = { r with drawing = v }
                    override x.Update(r,f) = { r with drawing = f r.drawing }
                }
            let history =
                { new Lens<CorrelationDrawing.CorrelationAppModel, Microsoft.FSharp.Core.Option<CorrelationDrawing.CorrelationAppModel>>() with
                    override x.Get(r) = r.history
                    override x.Set(r,v) = { r with history = v }
                    override x.Update(r,f) = { r with history = f r.history }
                }
            let future =
                { new Lens<CorrelationDrawing.CorrelationAppModel, Microsoft.FSharp.Core.Option<CorrelationDrawing.CorrelationAppModel>>() with
                    override x.Get(r) = r.future
                    override x.Set(r,v) = { r with future = v }
                    override x.Update(r,f) = { r with future = f r.future }
                }
