namespace PRo3DModels

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open PRo3DModels

[<AutoOpen>]
module Mutable =

    
    
    type MBookmark(__initial : PRo3DModels.Bookmark) =
        inherit obj()
        let mutable __current = __initial
        let _id = ResetMod.Create(__initial.id)
        let _point = ResetMod.Create(__initial.point)
        let _color = ResetMod.Create(__initial.color)
        let _camState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camState)
        let _visible = ResetMod.Create(__initial.visible)
        let _text = ResetMod.Create(__initial.text)
        
        member x.id = _id :> IMod<_>
        member x.point = _point :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.camState = _camState
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Update(v : PRo3DModels.Bookmark) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_id,v.id)
                ResetMod.Update(_point,v.point)
                ResetMod.Update(_color,v.color)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camState, v.camState)
                ResetMod.Update(_visible,v.visible)
                ResetMod.Update(_text,v.text)
                
        
        static member Create(__initial : PRo3DModels.Bookmark) : MBookmark = MBookmark(__initial)
        static member Update(m : MBookmark, v : PRo3DModels.Bookmark) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.Bookmark> with
            member x.Update v = x.Update v
    
    
    
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
                { new Lens<PRo3DModels.Bookmark, Aardvark.UI.Primitives.CameraControllerState>() with
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
    
    
    type MRenderingParameters(__initial : PRo3DModels.RenderingParameters) =
        inherit obj()
        let mutable __current = __initial
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Update(v : PRo3DModels.RenderingParameters) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_cullMode,v.cullMode)
                
        
        static member Create(__initial : PRo3DModels.RenderingParameters) : MRenderingParameters = MRenderingParameters(__initial)
        static member Update(m : MRenderingParameters, v : PRo3DModels.RenderingParameters) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.RenderingParameters> with
            member x.Update v = x.Update v
    
    
    
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
    
    
    type MNavigationParameters(__initial : PRo3DModels.NavigationParameters) =
        inherit obj()
        let mutable __current = __initial
        let _navigationMode = ResetMod.Create(__initial.navigationMode)
        
        member x.navigationMode = _navigationMode :> IMod<_>
        
        member x.Update(v : PRo3DModels.NavigationParameters) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_navigationMode,v.navigationMode)
                
        
        static member Create(__initial : PRo3DModels.NavigationParameters) : MNavigationParameters = MNavigationParameters(__initial)
        static member Update(m : MNavigationParameters, v : PRo3DModels.NavigationParameters) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.NavigationParameters> with
            member x.Update v = x.Update v
    
    
    
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
    
    
    type MBookmarkAppModel(__initial : PRo3DModels.BookmarkAppModel) =
        inherit obj()
        let mutable __current = __initial
        let _bookmarkCamera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.bookmarkCamera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _boxHovered = MOption.Create(__initial.boxHovered)
        let _bookmarks = MList.Create(__initial.bookmarks, (fun v -> MBookmark.Create(v)), (fun (m,v) -> MBookmark.Update(m, v)), (fun v -> v))
        
        member x.bookmarkCamera = _bookmarkCamera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.bookmarks = _bookmarks :> alist<_>
        
        member x.Update(v : PRo3DModels.BookmarkAppModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_bookmarkCamera, v.bookmarkCamera)
                MRenderingParameters.Update(_rendering, v.rendering)
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                MOption.Update(_boxHovered, v.boxHovered)
                MList.Update(_bookmarks, v.bookmarks)
                
        
        static member Create(__initial : PRo3DModels.BookmarkAppModel) : MBookmarkAppModel = MBookmarkAppModel(__initial)
        static member Update(m : MBookmarkAppModel, v : PRo3DModels.BookmarkAppModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.BookmarkAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module BookmarkAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let bookmarkCamera =
                { new Lens<PRo3DModels.BookmarkAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.bookmarkCamera
                    override x.Set(r,v) = { r with bookmarkCamera = v }
                    override x.Update(r,f) = { r with bookmarkCamera = f r.bookmarkCamera }
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
    
    
    type MVisibleBox(__initial : PRo3DModels.VisibleBox) =
        inherit obj()
        let mutable __current = __initial
        let _geometry = ResetMod.Create(__initial.geometry)
        let _color = ResetMod.Create(__initial.color)
        let _id = ResetMod.Create(__initial.id)
        
        member x.geometry = _geometry :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.id = _id :> IMod<_>
        
        member x.Update(v : PRo3DModels.VisibleBox) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_color,v.color)
                _id.Update(v.id)
                
        
        static member Create(__initial : PRo3DModels.VisibleBox) : MVisibleBox = MVisibleBox(__initial)
        static member Update(m : MVisibleBox, v : PRo3DModels.VisibleBox) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.VisibleBox> with
            member x.Update v = x.Update v
    
    
    
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
    
    
    type MAnnotation(__initial : PRo3DModels.Annotation) =
        inherit obj()
        let mutable __current = __initial
        let _geometry = ResetMod.Create(__initial.geometry)
        let _projection = ResetMod.Create(__initial.projection)
        let _semantic = ResetMod.Create(__initial.semantic)
        let _points = MList.Create(__initial.points)
        let _segments = MList.Create(__initial.segments, (fun v -> MList.Create(v)), (fun (m,v) -> MList.Update(m, v)), (fun v -> v :> alist<_>))
        let _color = ResetMod.Create(__initial.color)
        let _thickness = Aardvark.UI.Mutable.MNumericInput.Create(__initial.thickness)
        let _visible = ResetMod.Create(__initial.visible)
        let _text = ResetMod.Create(__initial.text)
        
        member x.geometry = _geometry :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.semantic = _semantic :> IMod<_>
        member x.points = _points :> alist<_>
        member x.segments = _segments :> alist<_>
        member x.color = _color :> IMod<_>
        member x.thickness = _thickness
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Update(v : PRo3DModels.Annotation) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_projection,v.projection)
                ResetMod.Update(_semantic,v.semantic)
                MList.Update(_points, v.points)
                MList.Update(_segments, v.segments)
                ResetMod.Update(_color,v.color)
                Aardvark.UI.Mutable.MNumericInput.Update(_thickness, v.thickness)
                ResetMod.Update(_visible,v.visible)
                ResetMod.Update(_text,v.text)
                
        
        static member Create(__initial : PRo3DModels.Annotation) : MAnnotation = MAnnotation(__initial)
        static member Update(m : MAnnotation, v : PRo3DModels.Annotation) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.Annotation> with
            member x.Update v = x.Update v
    
    
    
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
                { new Lens<PRo3DModels.Annotation, Aardvark.Base.plist<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
            let segments =
                { new Lens<PRo3DModels.Annotation, Aardvark.Base.plist<Aardvark.Base.plist<Aardvark.Base.V3d>>>() with
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
    
    
    type MMeasurementsImporterAppModel(__initial : PRo3DModels.MeasurementsImporterAppModel) =
        inherit obj()
        let mutable __current = __initial
        let _measurementsCamera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.measurementsCamera)
        let _measurementsRendering = MRenderingParameters.Create(__initial.measurementsRendering)
        let _measurementsHoverPosition = MOption.Create(__initial.measurementsHoverPosition)
        let _scenePath = ResetMod.Create(__initial.scenePath)
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        
        member x.measurementsCamera = _measurementsCamera
        member x.measurementsRendering = _measurementsRendering
        member x.measurementsHoverPosition = _measurementsHoverPosition :> IMod<_>
        member x.scenePath = _scenePath :> IMod<_>
        member x.annotations = _annotations :> alist<_>
        
        member x.Update(v : PRo3DModels.MeasurementsImporterAppModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_measurementsCamera, v.measurementsCamera)
                MRenderingParameters.Update(_measurementsRendering, v.measurementsRendering)
                MOption.Update(_measurementsHoverPosition, v.measurementsHoverPosition)
                ResetMod.Update(_scenePath,v.scenePath)
                MList.Update(_annotations, v.annotations)
                
        
        static member Create(__initial : PRo3DModels.MeasurementsImporterAppModel) : MMeasurementsImporterAppModel = MMeasurementsImporterAppModel(__initial)
        static member Update(m : MMeasurementsImporterAppModel, v : PRo3DModels.MeasurementsImporterAppModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.MeasurementsImporterAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MeasurementsImporterAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let measurementsCamera =
                { new Lens<PRo3DModels.MeasurementsImporterAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.measurementsCamera
                    override x.Set(r,v) = { r with measurementsCamera = v }
                    override x.Update(r,f) = { r with measurementsCamera = f r.measurementsCamera }
                }
            let measurementsRendering =
                { new Lens<PRo3DModels.MeasurementsImporterAppModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.measurementsRendering
                    override x.Set(r,v) = { r with measurementsRendering = v }
                    override x.Update(r,f) = { r with measurementsRendering = f r.measurementsRendering }
                }
            let measurementsHoverPosition =
                { new Lens<PRo3DModels.MeasurementsImporterAppModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.measurementsHoverPosition
                    override x.Set(r,v) = { r with measurementsHoverPosition = v }
                    override x.Update(r,f) = { r with measurementsHoverPosition = f r.measurementsHoverPosition }
                }
            let scenePath =
                { new Lens<PRo3DModels.MeasurementsImporterAppModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.scenePath
                    override x.Set(r,v) = { r with scenePath = v }
                    override x.Update(r,f) = { r with scenePath = f r.scenePath }
                }
            let annotations =
                { new Lens<PRo3DModels.MeasurementsImporterAppModel, Aardvark.Base.plist<PRo3DModels.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
    
    
    type MComposedViewerModel(__initial : PRo3DModels.ComposedViewerModel) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _singleAnnotation = MAnnotation.Create(__initial.singleAnnotation)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _boxHovered = MOption.Create(__initial.boxHovered)
        
        member x.camera = _camera
        member x.singleAnnotation = _singleAnnotation
        member x.rendering = _rendering
        member x.boxHovered = _boxHovered :> IMod<_>
        
        member x.Update(v : PRo3DModels.ComposedViewerModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MAnnotation.Update(_singleAnnotation, v.singleAnnotation)
                MRenderingParameters.Update(_rendering, v.rendering)
                MOption.Update(_boxHovered, v.boxHovered)
                
        
        static member Create(__initial : PRo3DModels.ComposedViewerModel) : MComposedViewerModel = MComposedViewerModel(__initial)
        static member Update(m : MComposedViewerModel, v : PRo3DModels.ComposedViewerModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.ComposedViewerModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ComposedViewerModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.ComposedViewerModel, Aardvark.UI.Primitives.CameraControllerState>() with
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
    
    
    type MBoxSelectionDemoModel(__initial : PRo3DModels.BoxSelectionDemoModel) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _boxes = MList.Create(__initial.boxes, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxesSet = MSet.Create((fun (v : PRo3DModels.VisibleBox) -> v.id :> obj), __initial.boxesSet, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxesMap = MMap.Create(__initial.boxesMap, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxHovered = MOption.Create(__initial.boxHovered)
        let _selectedBoxes = MSet.Create(__initial.selectedBoxes)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.boxes = _boxes :> alist<_>
        member x.boxesSet = _boxesSet :> aset<_>
        member x.boxesMap = _boxesMap :> amap<_,_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.selectedBoxes = _selectedBoxes :> aset<_>
        
        member x.Update(v : PRo3DModels.BoxSelectionDemoModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                MList.Update(_boxes, v.boxes)
                MSet.Update(_boxesSet, v.boxesSet)
                MMap.Update(_boxesMap, v.boxesMap)
                MOption.Update(_boxHovered, v.boxHovered)
                MSet.Update(_selectedBoxes, v.selectedBoxes)
                
        
        static member Create(__initial : PRo3DModels.BoxSelectionDemoModel) : MBoxSelectionDemoModel = MBoxSelectionDemoModel(__initial)
        static member Update(m : MBoxSelectionDemoModel, v : PRo3DModels.BoxSelectionDemoModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.BoxSelectionDemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module BoxSelectionDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
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
                { new Lens<PRo3DModels.BoxSelectionDemoModel, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,PRo3DModels.VisibleBox>>() with
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
    
    
    type MSimpleDrawingAppModel(__initial : PRo3DModels.SimpleDrawingAppModel) =
        inherit obj()
        let mutable __current = __initial
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
        
        member x.Update(v : PRo3DModels.SimpleDrawingAppModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                ResetMod.Update(_points,v.points)
                
        
        static member Create(__initial : PRo3DModels.SimpleDrawingAppModel) : MSimpleDrawingAppModel = MSimpleDrawingAppModel(__initial)
        static member Update(m : MSimpleDrawingAppModel, v : PRo3DModels.SimpleDrawingAppModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.SimpleDrawingAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SimpleDrawingAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.SimpleDrawingAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
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
    
    
    type MDrawingModel(__initial : PRo3DModels.DrawingModel) =
        inherit obj()
        let mutable __current = __initial
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _working = MOption.Create(__initial.working, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _projection = ResetMod.Create(__initial.projection)
        let _geometry = ResetMod.Create(__initial.geometry)
        let _semantic = ResetMod.Create(__initial.semantic)
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        let _exportPath = ResetMod.Create(__initial.exportPath)
        
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.working = _working :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.geometry = _geometry :> IMod<_>
        member x.semantic = _semantic :> IMod<_>
        member x.annotations = _annotations :> alist<_>
        member x.exportPath = _exportPath :> IMod<_>
        
        member x.Update(v : PRo3DModels.DrawingModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                MOption.Update(_working, v.working)
                ResetMod.Update(_projection,v.projection)
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_semantic,v.semantic)
                MList.Update(_annotations, v.annotations)
                ResetMod.Update(_exportPath,v.exportPath)
                
        
        static member Create(__initial : PRo3DModels.DrawingModel) : MDrawingModel = MDrawingModel(__initial)
        static member Update(m : MDrawingModel, v : PRo3DModels.DrawingModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.DrawingModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DrawingModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let draw =
                { new Lens<PRo3DModels.DrawingModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<PRo3DModels.DrawingModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let working =
                { new Lens<PRo3DModels.DrawingModel, Microsoft.FSharp.Core.Option<PRo3DModels.Annotation>>() with
                    override x.Get(r) = r.working
                    override x.Set(r,v) = { r with working = v }
                    override x.Update(r,f) = { r with working = f r.working }
                }
            let projection =
                { new Lens<PRo3DModels.DrawingModel, PRo3DModels.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let geometry =
                { new Lens<PRo3DModels.DrawingModel, PRo3DModels.Geometry>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let semantic =
                { new Lens<PRo3DModels.DrawingModel, PRo3DModels.Semantic>() with
                    override x.Get(r) = r.semantic
                    override x.Set(r,v) = { r with semantic = v }
                    override x.Update(r,f) = { r with semantic = f r.semantic }
                }
            let annotations =
                { new Lens<PRo3DModels.DrawingModel, Aardvark.Base.plist<PRo3DModels.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
            let exportPath =
                { new Lens<PRo3DModels.DrawingModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.exportPath
                    override x.Set(r,v) = { r with exportPath = v }
                    override x.Update(r,f) = { r with exportPath = f r.exportPath }
                }
    
    
    type MAnnotationAppModel(__initial : PRo3DModels.AnnotationAppModel) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _drawing = MDrawingModel.Create(__initial.drawing)
        let _history = ResetMod.Create(__initial.history)
        let _future = ResetMod.Create(__initial.future)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.drawing = _drawing
        member x.history = _history :> IMod<_>
        member x.future = _future :> IMod<_>
        
        member x.Update(v : PRo3DModels.AnnotationAppModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                MDrawingModel.Update(_drawing, v.drawing)
                _history.Update(v.history)
                _future.Update(v.future)
                
        
        static member Create(__initial : PRo3DModels.AnnotationAppModel) : MAnnotationAppModel = MAnnotationAppModel(__initial)
        static member Update(m : MAnnotationAppModel, v : PRo3DModels.AnnotationAppModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.AnnotationAppModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module AnnotationAppModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.AnnotationAppModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<PRo3DModels.AnnotationAppModel, PRo3DModels.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let drawing =
                { new Lens<PRo3DModels.AnnotationAppModel, PRo3DModels.DrawingModel>() with
                    override x.Get(r) = r.drawing
                    override x.Set(r,v) = { r with drawing = v }
                    override x.Update(r,f) = { r with drawing = f r.drawing }
                }
            let history =
                { new Lens<PRo3DModels.AnnotationAppModel, Microsoft.FSharp.Core.Option<PRo3DModels.AnnotationAppModel>>() with
                    override x.Get(r) = r.history
                    override x.Set(r,v) = { r with history = v }
                    override x.Update(r,f) = { r with history = f r.history }
                }
            let future =
                { new Lens<PRo3DModels.AnnotationAppModel, Microsoft.FSharp.Core.Option<PRo3DModels.AnnotationAppModel>>() with
                    override x.Get(r) = r.future
                    override x.Set(r,v) = { r with future = v }
                    override x.Update(r,f) = { r with future = f r.future }
                }
    
    
    type MOrbitCameraDemoModel(__initial : PRo3DModels.OrbitCameraDemoModel) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        
        member x.camera = _camera
        member x.rendering = _rendering
        
        member x.Update(v : PRo3DModels.OrbitCameraDemoModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                
        
        static member Create(__initial : PRo3DModels.OrbitCameraDemoModel) : MOrbitCameraDemoModel = MOrbitCameraDemoModel(__initial)
        static member Update(m : MOrbitCameraDemoModel, v : PRo3DModels.OrbitCameraDemoModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.OrbitCameraDemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module OrbitCameraDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.OrbitCameraDemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
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
    
    
    type MNavigationModeDemoModel(__initial : PRo3DModels.NavigationModeDemoModel) =
        inherit obj()
        let mutable __current = __initial
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = MRenderingParameters.Create(__initial.rendering)
        let _navigation = MNavigationParameters.Create(__initial.navigation)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.navigation = _navigation
        
        member x.Update(v : PRo3DModels.NavigationModeDemoModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                MRenderingParameters.Update(_rendering, v.rendering)
                MNavigationParameters.Update(_navigation, v.navigation)
                
        
        static member Create(__initial : PRo3DModels.NavigationModeDemoModel) : MNavigationModeDemoModel = MNavigationModeDemoModel(__initial)
        static member Update(m : MNavigationModeDemoModel, v : PRo3DModels.NavigationModeDemoModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<PRo3DModels.NavigationModeDemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NavigationModeDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<PRo3DModels.NavigationModeDemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
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
