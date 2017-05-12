namespace PRo3DModels

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open PRo3DModels

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MBookmark private(__initial : PRo3DModels.Bookmark) =
        let mutable __current = __initial
        let _id = ResetMod(__initial.id)
        let _point = ResetMod(__initial.point)
        let _color = ResetMod(__initial.color)
        let _camState = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camState)
        let _visible = ResetMod(__initial.visible)
        let _text = ResetMod(__initial.text)
        
        member x.id = _id :> IMod<_>
        member x.point = _point :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.camState = _camState
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Update(__model : PRo3DModels.Bookmark) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _id.Update(__model.id)
                _point.Update(__model.point)
                _color.Update(__model.color)
                _camState.Update(__model.camState)
                _visible.Update(__model.visible)
                _text.Update(__model.text)
        
        static member Create(initial) = MBookmark(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MBookmark =
        let inline id (m : MBookmark) = m.id
        let inline point (m : MBookmark) = m.point
        let inline color (m : MBookmark) = m.color
        let inline camState (m : MBookmark) = m.camState
        let inline visible (m : MBookmark) = m.visible
        let inline text (m : MBookmark) = m.text
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Bookmark =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let id =
                { new Lens<PRo3DModels.Bookmark, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
            let point =
                { new Lens<PRo3DModels.Bookmark, Aardvark.Base.V3d>() with
                    override x.Get(r) = r.point
                    override x.Set(r,v) = { r with point = v }
                    override x.Update(r,f) = { r with point = f r.point }
                }
            let color =
                { new Lens<PRo3DModels.Bookmark, Aardvark.Base.C4b>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let camState =
                { new Lens<PRo3DModels.Bookmark, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camState
                    override x.Set(r,v) = { r with camState = v }
                    override x.Update(r,f) = { r with camState = f r.camState }
                }
            let visible =
                { new Lens<PRo3DModels.Bookmark, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.visible
                    override x.Set(r,v) = { r with visible = v }
                    override x.Update(r,f) = { r with visible = f r.visible }
                }
            let text =
                { new Lens<PRo3DModels.Bookmark, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.text
                    override x.Set(r,v) = { r with text = v }
                    override x.Update(r,f) = { r with text = f r.text }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MRenderingParameters private(__initial : PRo3DModels.RenderingParameters) =
        let mutable __current = __initial
        let _fillMode = ResetMod(__initial.fillMode)
        let _cullMode = ResetMod(__initial.cullMode)
        
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Update(__model : PRo3DModels.RenderingParameters) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _fillMode.Update(__model.fillMode)
                _cullMode.Update(__model.cullMode)
        
        static member Create(initial) = MRenderingParameters(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MRenderingParameters =
        let inline fillMode (m : MRenderingParameters) = m.fillMode
        let inline cullMode (m : MRenderingParameters) = m.cullMode
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RenderingParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let fillMode =
                { new Lens<PRo3DModels.RenderingParameters, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let cullMode =
                { new Lens<PRo3DModels.RenderingParameters, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MNavigationParameters private(__initial : PRo3DModels.NavigationParameters) =
        let mutable __current = __initial
        let _navigationMode = ResetMod(__initial.navigationMode)
        
        member x.navigationMode = _navigationMode :> IMod<_>
        
        member x.Update(__model : PRo3DModels.NavigationParameters) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _navigationMode.Update(__model.navigationMode)
        
        static member Create(initial) = MNavigationParameters(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MNavigationParameters =
        let inline navigationMode (m : MNavigationParameters) = m.navigationMode
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NavigationParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let navigationMode =
                { new Lens<PRo3DModels.NavigationParameters, PRo3DModels.NavigationMode>() with
                    override x.Get(r) = r.navigationMode
                    override x.Set(r,v) = { r with navigationMode = v }
                    override x.Update(r,f) = { r with navigationMode = f r.navigationMode }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MBookmarkAppModel private(__initial : PRo3DModels.BookmarkAppModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod(__initial.draw)
        let _hoverPosition = ResetMod(__initial.hoverPosition)
        let _boxHovered = ResetMod(__initial.boxHovered)
        let _bookmarks = ResetMapList(__initial.bookmarks, (fun _ -> MBookmark.Create), fun (m,i) -> m.Update(i))
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.bookmarks = _bookmarks :> alist<_>
        
        member x.Update(__model : PRo3DModels.BookmarkAppModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
                _draw.Update(__model.draw)
                _hoverPosition.Update(__model.hoverPosition)
                _boxHovered.Update(__model.boxHovered)
                _bookmarks.Update(__model.bookmarks)
        
        static member Create(initial) = MBookmarkAppModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MBookmarkAppModel =
        let inline camera (m : MBookmarkAppModel) = m.camera
        let inline rendering (m : MBookmarkAppModel) = m.rendering
        let inline draw (m : MBookmarkAppModel) = m.draw
        let inline hoverPosition (m : MBookmarkAppModel) = m.hoverPosition
        let inline boxHovered (m : MBookmarkAppModel) = m.boxHovered
        let inline bookmarks (m : MBookmarkAppModel) = m.bookmarks
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module BookmarkAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.BookmarkAppModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.BookmarkAppModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let draw =
                { new Lens<PRo3DModels.BookmarkAppModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<PRo3DModels.BookmarkAppModel, Microsoft.FSharp.Core.Option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let boxHovered =
                { new Lens<PRo3DModels.BookmarkAppModel, Microsoft.FSharp.Core.Option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
            let bookmarks =
                { new Lens<PRo3DModels.BookmarkAppModel, Aardvark.Base.plist<PRo3DModels.Bookmark>>() with
                    override x.Get(r) = r.bookmarks
                    override x.Set(r,v) = { r with bookmarks = v }
                    override x.Update(r,f) = { r with bookmarks = f r.bookmarks }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MVisibleBox private(__initial : PRo3DModels.VisibleBox) =
        let mutable __current = __initial
        let _geometry = ResetMod(__initial.geometry)
        let _color = ResetMod(__initial.color)
        let _id = ResetMod(__initial.id)
        
        member x.geometry = _geometry :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.id = _id :> IMod<_>
        
        member x.Update(__model : PRo3DModels.VisibleBox) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _geometry.Update(__model.geometry)
                _color.Update(__model.color)
                _id.Update(__model.id)
        
        static member Create(initial) = MVisibleBox(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MVisibleBox =
        let inline geometry (m : MVisibleBox) = m.geometry
        let inline color (m : MVisibleBox) = m.color
        let inline id (m : MVisibleBox) = m.id
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VisibleBox =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let geometry =
                { new Lens<PRo3DModels.VisibleBox, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let color =
                { new Lens<PRo3DModels.VisibleBox, Aardvark.Base.C4b>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let id =
                { new Lens<PRo3DModels.VisibleBox, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MAnnotation private(__initial : PRo3DModels.Annotation) =
        let mutable __current = __initial
        let _geometry = ResetMod(__initial.geometry)
        let _projection = ResetMod(__initial.projection)
        let _semantic = ResetMod(__initial.semantic)
        let _points = ResetMod(__initial.points)
        let _segments = ResetMod(__initial.segments)
        let _color = ResetMod(__initial.color)
        let _thickness = Aardvark.UI.Mutable.MNumericInput.Create(__initial.thickness)
        let _visible = ResetMod(__initial.visible)
        let _text = ResetMod(__initial.text)
        
        member x.geometry = _geometry :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.semantic = _semantic :> IMod<_>
        member x.points = _points :> IMod<_>
        member x.segments = _segments :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.thickness = _thickness
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Update(__model : PRo3DModels.Annotation) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _geometry.Update(__model.geometry)
                _projection.Update(__model.projection)
                _semantic.Update(__model.semantic)
                _points.Update(__model.points)
                _segments.Update(__model.segments)
                _color.Update(__model.color)
                _thickness.Update(__model.thickness)
                _visible.Update(__model.visible)
                _text.Update(__model.text)
        
        static member Create(initial) = MAnnotation(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MAnnotation =
        let inline geometry (m : MAnnotation) = m.geometry
        let inline projection (m : MAnnotation) = m.projection
        let inline semantic (m : MAnnotation) = m.semantic
        let inline points (m : MAnnotation) = m.points
        let inline segments (m : MAnnotation) = m.segments
        let inline color (m : MAnnotation) = m.color
        let inline thickness (m : MAnnotation) = m.thickness
        let inline visible (m : MAnnotation) = m.visible
        let inline text (m : MAnnotation) = m.text
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Annotation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let geometry =
                { new Lens<PRo3DModels.Annotation, PRo3DModels.Geometry>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let projection =
                { new Lens<PRo3DModels.Annotation, PRo3DModels.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let semantic =
                { new Lens<PRo3DModels.Annotation, PRo3DModels.Semantic>() with
                    override x.Get(r) = r.semantic
                    override x.Set(r,v) = { r with semantic = v }
                    override x.Update(r,f) = { r with semantic = f r.semantic }
                }
            let points =
                { new Lens<PRo3DModels.Annotation, PRo3DModels.Points>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
            let segments =
                { new Lens<PRo3DModels.Annotation, Microsoft.FSharp.Collections.list<PRo3DModels.Segment>>() with
                    override x.Get(r) = r.segments
                    override x.Set(r,v) = { r with segments = v }
                    override x.Update(r,f) = { r with segments = f r.segments }
                }
            let color =
                { new Lens<PRo3DModels.Annotation, Aardvark.Base.C4b>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let thickness =
                { new Lens<PRo3DModels.Annotation, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.thickness
                    override x.Set(r,v) = { r with thickness = v }
                    override x.Update(r,f) = { r with thickness = f r.thickness }
                }
            let visible =
                { new Lens<PRo3DModels.Annotation, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.visible
                    override x.Set(r,v) = { r with visible = v }
                    override x.Update(r,f) = { r with visible = f r.visible }
                }
            let text =
                { new Lens<PRo3DModels.Annotation, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.text
                    override x.Set(r,v) = { r with text = v }
                    override x.Update(r,f) = { r with text = f r.text }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MComposedViewerModel private(__initial : PRo3DModels.ComposedViewerModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _singleAnnotation = MAnnotation.Create(__initial.singleAnnotation)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _boxHovered = ResetMod(__initial.boxHovered)
        
        member x.camera = _camera
        member x.singleAnnotation = _singleAnnotation
        member x.rendering = _rendering
        member x.boxHovered = _boxHovered :> IMod<_>
        
        member x.Update(__model : PRo3DModels.ComposedViewerModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _singleAnnotation.Update(__model.singleAnnotation)
                _rendering.Update(__model.rendering)
                _boxHovered.Update(__model.boxHovered)
        
        static member Create(initial) = MComposedViewerModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MComposedViewerModel =
        let inline camera (m : MComposedViewerModel) = m.camera
        let inline singleAnnotation (m : MComposedViewerModel) = m.singleAnnotation
        let inline rendering (m : MComposedViewerModel) = m.rendering
        let inline boxHovered (m : MComposedViewerModel) = m.boxHovered
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ComposedViewerModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.ComposedViewerModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let singleAnnotation =
                { new Lens<PRo3DModels.ComposedViewerModel, PRo3DModels.Annotation>() with
                    override x.Get(r) = r.singleAnnotation
                    override x.Set(r,v) = { r with singleAnnotation = v }
                    override x.Update(r,f) = { r with singleAnnotation = f r.singleAnnotation }
                }
            let rendering =
                { new Lens<PRo3DModels.ComposedViewerModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let boxHovered =
                { new Lens<PRo3DModels.ComposedViewerModel, Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MBoxSelectionDemoModel private(__initial : PRo3DModels.BoxSelectionDemoModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _boxes = ResetMapList(__initial.boxes, (fun _ -> MVisibleBox.Create), fun (m,i) -> m.Update(i))
        let _boxesSet = ResetMapSet((fun v -> v.id :> obj), __initial.boxesSet, MVisibleBox.Create, fun (m,i) -> m.Update(i))
        let _boxesMap = ResetMapMap(__initial.boxesMap, (fun k v -> MVisibleBox.Create(v)), (fun (m,i) -> m.Update(i)))
        let _boxHovered = ResetMod(__initial.boxHovered)
        let _selectedBoxes = ResetSet(__initial.selectedBoxes)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.boxes = _boxes :> alist<_>
        member x.boxesSet = _boxesSet :> aset<_>
        member x.boxesMap = _boxesMap :> amap<_,_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.selectedBoxes = _selectedBoxes :> aset<_>
        
        member x.Update(__model : PRo3DModels.BoxSelectionDemoModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
                _boxes.Update(__model.boxes)
                _boxesSet.Update(__model.boxesSet)
                _boxesMap.Update(__model.boxesMap)
                _boxHovered.Update(__model.boxHovered)
                _selectedBoxes.Update(__model.selectedBoxes)
        
        static member Create(initial) = MBoxSelectionDemoModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MBoxSelectionDemoModel =
        let inline camera (m : MBoxSelectionDemoModel) = m.camera
        let inline rendering (m : MBoxSelectionDemoModel) = m.rendering
        let inline boxes (m : MBoxSelectionDemoModel) = m.boxes
        let inline boxesSet (m : MBoxSelectionDemoModel) = m.boxesSet
        let inline boxesMap (m : MBoxSelectionDemoModel) = m.boxesMap
        let inline boxHovered (m : MBoxSelectionDemoModel) = m.boxHovered
        let inline selectedBoxes (m : MBoxSelectionDemoModel) = m.selectedBoxes
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module BoxSelectionDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let boxes =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.Base.plist<PRo3DModels.VisibleBox>>() with
                    override x.Get(r) = r.boxes
                    override x.Set(r,v) = { r with boxes = v }
                    override x.Update(r,f) = { r with boxes = f r.boxes }
                }
            let boxesSet =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.Base.hset<PRo3DModels.VisibleBox>>() with
                    override x.Get(r) = r.boxesSet
                    override x.Set(r,v) = { r with boxesSet = v }
                    override x.Update(r,f) = { r with boxesSet = f r.boxesSet }
                }
            let boxesMap =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.Base.hmap<Microsoft.FSharp.Core.string, PRo3DModels.VisibleBox>>() with
                    override x.Get(r) = r.boxesMap
                    override x.Set(r,v) = { r with boxesMap = v }
                    override x.Update(r,f) = { r with boxesMap = f r.boxesMap }
                }
            let boxHovered =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
            let selectedBoxes =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.Base.hset<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.selectedBoxes
                    override x.Set(r,v) = { r with selectedBoxes = v }
                    override x.Update(r,f) = { r with selectedBoxes = f r.selectedBoxes }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MSimpleDrawingAppModel private(__initial : PRo3DModels.SimpleDrawingAppModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod(__initial.draw)
        let _hoverPosition = ResetMod(__initial.hoverPosition)
        let _points = ResetMod(__initial.points)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.points = _points :> IMod<_>
        
        member x.Update(__model : PRo3DModels.SimpleDrawingAppModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
                _draw.Update(__model.draw)
                _hoverPosition.Update(__model.hoverPosition)
                _points.Update(__model.points)
        
        static member Create(initial) = MSimpleDrawingAppModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MSimpleDrawingAppModel =
        let inline camera (m : MSimpleDrawingAppModel) = m.camera
        let inline rendering (m : MSimpleDrawingAppModel) = m.rendering
        let inline draw (m : MSimpleDrawingAppModel) = m.draw
        let inline hoverPosition (m : MSimpleDrawingAppModel) = m.hoverPosition
        let inline points (m : MSimpleDrawingAppModel) = m.points
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SimpleDrawingAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let draw =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let points =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, Microsoft.FSharp.Collections.list<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MDrawingAppModel private(__initial : PRo3DModels.DrawingAppModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod(__initial.draw)
        let _hoverPosition = ResetMod(__initial.hoverPosition)
        let _working = ResetMod(__initial.working)
        let _projection = ResetMod(__initial.projection)
        let _geometry = ResetMod(__initial.geometry)
        let _semantic = ResetMod(__initial.semantic)
        let _annotations = ResetMod(__initial.annotations)
        let _exportPath = ResetMod(__initial.exportPath)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.working = _working :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.geometry = _geometry :> IMod<_>
        member x.semantic = _semantic :> IMod<_>
        member x.annotations = _annotations :> IMod<_>
        member x.exportPath = _exportPath :> IMod<_>
        
        member x.Update(__model : PRo3DModels.DrawingAppModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
                _draw.Update(__model.draw)
                _hoverPosition.Update(__model.hoverPosition)
                _working.Update(__model.working)
                _projection.Update(__model.projection)
                _geometry.Update(__model.geometry)
                _semantic.Update(__model.semantic)
                _annotations.Update(__model.annotations)
                _exportPath.Update(__model.exportPath)
        
        static member Create(initial) = MDrawingAppModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MDrawingAppModel =
        let inline camera (m : MDrawingAppModel) = m.camera
        let inline rendering (m : MDrawingAppModel) = m.rendering
        let inline draw (m : MDrawingAppModel) = m.draw
        let inline hoverPosition (m : MDrawingAppModel) = m.hoverPosition
        let inline working (m : MDrawingAppModel) = m.working
        let inline projection (m : MDrawingAppModel) = m.projection
        let inline geometry (m : MDrawingAppModel) = m.geometry
        let inline semantic (m : MDrawingAppModel) = m.semantic
        let inline annotations (m : MDrawingAppModel) = m.annotations
        let inline exportPath (m : MDrawingAppModel) = m.exportPath
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DrawingAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.DrawingAppModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.DrawingAppModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let draw =
                { new Lens<PRo3DModels.DrawingAppModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<PRo3DModels.DrawingAppModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let working =
                { new Lens<PRo3DModels.DrawingAppModel, Microsoft.FSharp.Core.option<PRo3DModels.Annotation>>() with
                    override x.Get(r) = r.working
                    override x.Set(r,v) = { r with working = v }
                    override x.Update(r,f) = { r with working = f r.working }
                }
            let projection =
                { new Lens<PRo3DModels.DrawingAppModel, PRo3DModels.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let geometry =
                { new Lens<PRo3DModels.DrawingAppModel, PRo3DModels.Geometry>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let semantic =
                { new Lens<PRo3DModels.DrawingAppModel, PRo3DModels.Semantic>() with
                    override x.Get(r) = r.semantic
                    override x.Set(r,v) = { r with semantic = v }
                    override x.Update(r,f) = { r with semantic = f r.semantic }
                }
            let annotations =
                { new Lens<PRo3DModels.DrawingAppModel, Microsoft.FSharp.Collections.list<PRo3DModels.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
            let exportPath =
                { new Lens<PRo3DModels.DrawingAppModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.exportPath
                    override x.Set(r,v) = { r with exportPath = v }
                    override x.Update(r,f) = { r with exportPath = f r.exportPath }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MOrbitCameraDemoModel private(__initial : PRo3DModels.OrbitCameraDemoModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        
        member x.camera = _camera
        member x.rendering = _rendering
        
        member x.Update(__model : PRo3DModels.OrbitCameraDemoModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
        
        static member Create(initial) = MOrbitCameraDemoModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MOrbitCameraDemoModel =
        let inline camera (m : MOrbitCameraDemoModel) = m.camera
        let inline rendering (m : MOrbitCameraDemoModel) = m.rendering
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module OrbitCameraDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.OrbitCameraDemoModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.OrbitCameraDemoModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MNavigationModeDemoModel private(__initial : PRo3DModels.NavigationModeDemoModel) =
        let mutable __current = __initial
        let _camera = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _navigation = MNavigationParameters.Create(__initial.navigation)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.navigation = _navigation
        
        member x.Update(__model : PRo3DModels.NavigationModeDemoModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _camera.Update(__model.camera)
                _rendering.Update(__model.rendering)
                _navigation.Update(__model.navigation)
        
        static member Create(initial) = MNavigationModeDemoModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MNavigationModeDemoModel =
        let inline camera (m : MNavigationModeDemoModel) = m.camera
        let inline rendering (m : MNavigationModeDemoModel) = m.rendering
        let inline navigation (m : MNavigationModeDemoModel) = m.navigation
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NavigationModeDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.NavigationModeDemoModel, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.NavigationModeDemoModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let navigation =
                { new Lens<PRo3DModels.NavigationModeDemoModel, PRo3DModels.NavigationParameters>() with
                    override x.Get(r) = r.navigation
                    override x.Set(r,v) = { r with navigation = v }
                    override x.Update(r,f) = { r with navigation = f r.navigation }
                }
