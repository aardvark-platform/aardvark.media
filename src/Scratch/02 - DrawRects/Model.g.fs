namespace DrawRects

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DrawRects

[<AutoOpen>]
module Mutable =

    
    
    type MRect(__initial : DrawRects.Rect) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawRects.Rect> = Aardvark.Base.Incremental.EqModRef<DrawRects.Rect>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawRects.Rect>
        let _box = ResetMod.Create(__initial.box)
        let _color = ResetMod.Create(__initial.color)
        let _id = ResetMod.Create(__initial.id)
        
        member x.box = _box :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.id = _id :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawRects.Rect) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_box,v.box)
                ResetMod.Update(_color,v.color)
                ResetMod.Update(_id,v.id)
                
        
        static member Create(__initial : DrawRects.Rect) : MRect = MRect(__initial)
        static member Update(m : MRect, v : DrawRects.Rect) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawRects.Rect> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Rect =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let box =
                { new Lens<DrawRects.Rect, Aardvark.Base.Box2d>() with
                    override x.Get(r) = r.box
                    override x.Set(r,v) = { r with box = v }
                    override x.Update(r,f) = { r with box = f r.box }
                }
            let color =
                { new Lens<DrawRects.Rect, DrawRects.Color>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let id =
                { new Lens<DrawRects.Rect, System.Int32>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
    
    
    type MModel(__initial : DrawRects.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawRects.Model> = Aardvark.Base.Incremental.EqModRef<DrawRects.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawRects.Model>
        let _rects = MMap.Create(__initial.rects, (fun v -> MRect.Create(v)), (fun (m,v) -> MRect.Update(m, v)), (fun v -> v))
        
        member x.rects = _rects :> amap<_,_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawRects.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MMap.Update(_rects, v.rects)
                
        
        static member Create(__initial : DrawRects.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : DrawRects.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawRects.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let rects =
                { new Lens<DrawRects.Model, Aardvark.Base.hmap<System.Int32,DrawRects.Rect>>() with
                    override x.Get(r) = r.rects
                    override x.Set(r,v) = { r with rects = v }
                    override x.Update(r,f) = { r with rects = f r.rects }
                }
    
    
    type MClientState(__initial : DrawRects.ClientState) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawRects.ClientState> = Aardvark.Base.Incremental.EqModRef<DrawRects.ClientState>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawRects.ClientState>
        let _viewport = ResetMod.Create(__initial.viewport)
        let _selectedRect = MOption.Create(__initial.selectedRect)
        let _workingRect = MOption.Create(__initial.workingRect)
        let _dragEndPoint = MOption.Create(__initial.dragEndPoint)
        let _downOnRect = ResetMod.Create(__initial.downOnRect)
        let _dragRect = MOption.Create(__initial.dragRect)
        let _mouseDown = MOption.Create(__initial.mouseDown)
        let _mouseDrag = MOption.Create(__initial.mouseDrag)
        let _currentInteraction = ResetMod.Create(__initial.currentInteraction)
        
        member x.viewport = _viewport :> IMod<_>
        member x.selectedRect = _selectedRect :> IMod<_>
        member x.workingRect = _workingRect :> IMod<_>
        member x.dragEndPoint = _dragEndPoint :> IMod<_>
        member x.downOnRect = _downOnRect :> IMod<_>
        member x.dragRect = _dragRect :> IMod<_>
        member x.mouseDown = _mouseDown :> IMod<_>
        member x.mouseDrag = _mouseDrag :> IMod<_>
        member x.currentInteraction = _currentInteraction :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawRects.ClientState) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_viewport,v.viewport)
                MOption.Update(_selectedRect, v.selectedRect)
                MOption.Update(_workingRect, v.workingRect)
                MOption.Update(_dragEndPoint, v.dragEndPoint)
                ResetMod.Update(_downOnRect,v.downOnRect)
                MOption.Update(_dragRect, v.dragRect)
                MOption.Update(_mouseDown, v.mouseDown)
                MOption.Update(_mouseDrag, v.mouseDrag)
                ResetMod.Update(_currentInteraction,v.currentInteraction)
                
        
        static member Create(__initial : DrawRects.ClientState) : MClientState = MClientState(__initial)
        static member Update(m : MClientState, v : DrawRects.ClientState) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawRects.ClientState> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ClientState =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let viewport =
                { new Lens<DrawRects.ClientState, Aardvark.Base.Box2d>() with
                    override x.Get(r) = r.viewport
                    override x.Set(r,v) = { r with viewport = v }
                    override x.Update(r,f) = { r with viewport = f r.viewport }
                }
            let selectedRect =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<System.Int32>>() with
                    override x.Get(r) = r.selectedRect
                    override x.Set(r,v) = { r with selectedRect = v }
                    override x.Update(r,f) = { r with selectedRect = f r.selectedRect }
                }
            let workingRect =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<DrawRects.OpenRect>>() with
                    override x.Get(r) = r.workingRect
                    override x.Set(r,v) = { r with workingRect = v }
                    override x.Update(r,f) = { r with workingRect = f r.workingRect }
                }
            let dragEndPoint =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<DrawRects.DragEndpoint>>() with
                    override x.Get(r) = r.dragEndPoint
                    override x.Set(r,v) = { r with dragEndPoint = v }
                    override x.Update(r,f) = { r with dragEndPoint = f r.dragEndPoint }
                }
            let downOnRect =
                { new Lens<DrawRects.ClientState, System.Boolean>() with
                    override x.Get(r) = r.downOnRect
                    override x.Set(r,v) = { r with downOnRect = v }
                    override x.Update(r,f) = { r with downOnRect = f r.downOnRect }
                }
            let dragRect =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<Aardvark.Base.V2d>>() with
                    override x.Get(r) = r.dragRect
                    override x.Set(r,v) = { r with dragRect = v }
                    override x.Update(r,f) = { r with dragRect = f r.dragRect }
                }
            let mouseDown =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<Aardvark.Base.V2d>>() with
                    override x.Get(r) = r.mouseDown
                    override x.Set(r,v) = { r with mouseDown = v }
                    override x.Update(r,f) = { r with mouseDown = f r.mouseDown }
                }
            let mouseDrag =
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<Aardvark.Base.V2d>>() with
                    override x.Get(r) = r.mouseDrag
                    override x.Set(r,v) = { r with mouseDrag = v }
                    override x.Update(r,f) = { r with mouseDrag = f r.mouseDrag }
                }
            let currentInteraction =
                { new Lens<DrawRects.ClientState, DrawRects.Interaction>() with
                    override x.Get(r) = r.currentInteraction
                    override x.Set(r,v) = { r with currentInteraction = v }
                    override x.Update(r,f) = { r with currentInteraction = f r.currentInteraction }
                }
