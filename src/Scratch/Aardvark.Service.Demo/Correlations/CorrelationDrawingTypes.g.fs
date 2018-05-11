namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MStyle(__initial : CorrelationDrawing.Style) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Style> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Style>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Style>
        let _color = Aardvark.UI.Mutable.MColorInput.Create(__initial.color)
        let _thickness = Aardvark.UI.Mutable.MNumericInput.Create(__initial.thickness)
        
        member x.color = _color
        member x.thickness = _thickness
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Style) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Mutable.MColorInput.Update(_color, v.color)
                Aardvark.UI.Mutable.MNumericInput.Update(_thickness, v.thickness)
                
        
        static member Create(__initial : CorrelationDrawing.Style) : MStyle = MStyle(__initial)
        static member Update(m : MStyle, v : CorrelationDrawing.Style) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Style> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Style =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let color =
                { new Lens<CorrelationDrawing.Style, Aardvark.UI.ColorInput>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let thickness =
                { new Lens<CorrelationDrawing.Style, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.thickness
                    override x.Set(r,v) = { r with thickness = v }
                    override x.Update(r,f) = { r with thickness = f r.thickness }
                }
    
    
    type MRenderingParameters(__initial : CorrelationDrawing.RenderingParameters) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.RenderingParameters> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.RenderingParameters>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.RenderingParameters>
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.RenderingParameters) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_cullMode,v.cullMode)
                
        
        static member Create(__initial : CorrelationDrawing.RenderingParameters) : MRenderingParameters = MRenderingParameters(__initial)
        static member Update(m : MRenderingParameters, v : CorrelationDrawing.RenderingParameters) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.RenderingParameters> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RenderingParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let fillMode =
                { new Lens<CorrelationDrawing.RenderingParameters, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let cullMode =
                { new Lens<CorrelationDrawing.RenderingParameters, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
    
    
    type MSemantic(__initial : CorrelationDrawing.Semantic) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Semantic> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Semantic>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Semantic>
        let _label = ResetMod.Create(__initial.label)
        let _size = ResetMod.Create(__initial.size)
        let _style = MStyle.Create(__initial.style)
        let _geometry = ResetMod.Create(__initial.geometry)
        let _semanticType = ResetMod.Create(__initial.semanticType)
        
        member x.label = _label :> IMod<_>
        member x.size = _size :> IMod<_>
        member x.style = _style
        member x.geometry = _geometry :> IMod<_>
        member x.semanticType = _semanticType :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Semantic) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_label,v.label)
                ResetMod.Update(_size,v.size)
                MStyle.Update(_style, v.style)
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_semanticType,v.semanticType)
                
        
        static member Create(__initial : CorrelationDrawing.Semantic) : MSemantic = MSemantic(__initial)
        static member Update(m : MSemantic, v : CorrelationDrawing.Semantic) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Semantic> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Semantic =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let label =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.label
                    override x.Set(r,v) = { r with label = v }
                    override x.Update(r,f) = { r with label = f r.label }
                }
            let size =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.double>() with
                    override x.Get(r) = r.size
                    override x.Set(r,v) = { r with size = v }
                    override x.Update(r,f) = { r with size = f r.size }
                }
            let style =
                { new Lens<CorrelationDrawing.Semantic, CorrelationDrawing.Style>() with
                    override x.Get(r) = r.style
                    override x.Set(r,v) = { r with style = v }
                    override x.Update(r,f) = { r with style = f r.style }
                }
            let geometry =
                { new Lens<CorrelationDrawing.Semantic, CorrelationDrawing.GeometryType>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let semanticType =
                { new Lens<CorrelationDrawing.Semantic, CorrelationDrawing.SemanticType>() with
                    override x.Get(r) = r.semanticType
                    override x.Set(r,v) = { r with semanticType = v }
                    override x.Update(r,f) = { r with semanticType = f r.semanticType }
                }
    
    
    type MAnnotation(__initial : CorrelationDrawing.Annotation) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Annotation> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Annotation>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Annotation>
        let _geometry = ResetMod.Create(__initial.geometry)
        let _projection = ResetMod.Create(__initial.projection)
        let _semantic = MSemantic.Create(__initial.semantic)
        let _points = MList.Create(__initial.points)
        let _segments = MList.Create(__initial.segments, (fun v -> MList.Create(v)), (fun (m,v) -> MList.Update(m, v)), (fun v -> v :> alist<_>))
        let _visible = ResetMod.Create(__initial.visible)
        let _text = ResetMod.Create(__initial.text)
        
        member x.geometry = _geometry :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.semantic = _semantic
        member x.points = _points :> alist<_>
        member x.segments = _segments :> alist<_>
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Annotation) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_projection,v.projection)
                MSemantic.Update(_semantic, v.semantic)
                MList.Update(_points, v.points)
                MList.Update(_segments, v.segments)
                ResetMod.Update(_visible,v.visible)
                ResetMod.Update(_text,v.text)
                
        
        static member Create(__initial : CorrelationDrawing.Annotation) : MAnnotation = MAnnotation(__initial)
        static member Update(m : MAnnotation, v : CorrelationDrawing.Annotation) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Annotation> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Annotation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let geometry =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.GeometryType>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let projection =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let semantic =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.Semantic>() with
                    override x.Get(r) = r.semantic
                    override x.Set(r,v) = { r with semantic = v }
                    override x.Update(r,f) = { r with semantic = f r.semantic }
                }
            let points =
                { new Lens<CorrelationDrawing.Annotation, Aardvark.Base.plist<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
            let segments =
                { new Lens<CorrelationDrawing.Annotation, Aardvark.Base.plist<Aardvark.Base.plist<Aardvark.Base.V3d>>>() with
                    override x.Get(r) = r.segments
                    override x.Set(r,v) = { r with segments = v }
                    override x.Update(r,f) = { r with segments = f r.segments }
                }
            let visible =
                { new Lens<CorrelationDrawing.Annotation, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.visible
                    override x.Set(r,v) = { r with visible = v }
                    override x.Update(r,f) = { r with visible = f r.visible }
                }
            let text =
                { new Lens<CorrelationDrawing.Annotation, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.text
                    override x.Set(r,v) = { r with text = v }
                    override x.Update(r,f) = { r with text = f r.text }
                }
    
    
    type MBorder(__initial : CorrelationDrawing.Border) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Border> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Border>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Border>
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        
        member x.annotations = _annotations :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Border) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MList.Update(_annotations, v.annotations)
                
        
        static member Create(__initial : CorrelationDrawing.Border) : MBorder = MBorder(__initial)
        static member Update(m : MBorder, v : CorrelationDrawing.Border) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Border> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Border =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let annotations =
                { new Lens<CorrelationDrawing.Border, Aardvark.Base.plist<CorrelationDrawing.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
    
    
    type MLogModel(__initial : CorrelationDrawing.LogModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.LogModel> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.LogModel>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.LogModel>
        let _id = ResetMod.Create(__initial.id)
        let _range = ResetMod.Create(__initial.range)
        
        member x.id = _id :> IMod<_>
        member x.range = _range :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.LogModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_id,v.id)
                ResetMod.Update(_range,v.range)
                
        
        static member Create(__initial : CorrelationDrawing.LogModel) : MLogModel = MLogModel(__initial)
        static member Update(m : MLogModel, v : CorrelationDrawing.LogModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.LogModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module LogModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let id =
                { new Lens<CorrelationDrawing.LogModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
            let range =
                { new Lens<CorrelationDrawing.LogModel, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.range
                    override x.Set(r,v) = { r with range = v }
                    override x.Update(r,f) = { r with range = f r.range }
                }
    
    
    type MCorrelationDrawingModel(__initial : CorrelationDrawing.CorrelationDrawingModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationDrawingModel> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.CorrelationDrawingModel>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.CorrelationDrawingModel>
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _working = MOption.Create(__initial.working, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _projection = ResetMod.Create(__initial.projection)
        let _geometry = ResetMod.Create(__initial.geometry)
        let _semantics = MMap.Create(__initial.semantics, (fun v -> MSemantic.Create(v)), (fun (m,v) -> MSemantic.Update(m, v)), (fun v -> v))
        let _semanticsList = MList.Create(__initial.semanticsList, (fun v -> MSemantic.Create(v)), (fun (m,v) -> MSemantic.Update(m, v)), (fun v -> v))
        let _selectedSemantic = MOption.Create(__initial.selectedSemantic)
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _exportPath = ResetMod.Create(__initial.exportPath)
        
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.working = _working :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.geometry = _geometry :> IMod<_>
        member x.semantics = _semantics :> amap<_,_>
        member x.semanticsList = _semanticsList :> alist<_>
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
                MMap.Update(_semantics, v.semantics)
                MList.Update(_semanticsList, v.semanticsList)
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
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.option<CorrelationDrawing.Annotation>>() with
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
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,CorrelationDrawing.Semantic>>() with
                    override x.Get(r) = r.semantics
                    override x.Set(r,v) = { r with semantics = v }
                    override x.Update(r,f) = { r with semantics = f r.semantics }
                }
            let semanticsList =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Aardvark.Base.plist<CorrelationDrawing.Semantic>>() with
                    override x.Get(r) = r.semanticsList
                    override x.Set(r,v) = { r with semanticsList = v }
                    override x.Update(r,f) = { r with semanticsList = f r.semanticsList }
                }
            let selectedSemantic =
                { new Lens<CorrelationDrawing.CorrelationDrawingModel, Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>() with
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
