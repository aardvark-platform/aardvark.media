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
        let _p00 = ResetMod.Create(__initial.p00)
        let _p10 = ResetMod.Create(__initial.p10)
        let _p11 = ResetMod.Create(__initial.p11)
        let _p01 = ResetMod.Create(__initial.p01)
        let _color = ResetMod.Create(__initial.color)
        let _id = ResetMod.Create(__initial.id)
        
        member x.p00 = _p00 :> IMod<_>
        member x.p10 = _p10 :> IMod<_>
        member x.p11 = _p11 :> IMod<_>
        member x.p01 = _p01 :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.id = _id :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawRects.Rect) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_p00,v.p00)
                ResetMod.Update(_p10,v.p10)
                ResetMod.Update(_p11,v.p11)
                ResetMod.Update(_p01,v.p01)
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
            let p00 =
                { new Lens<DrawRects.Rect, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.p00
                    override x.Set(r,v) = { r with p00 = v }
                    override x.Update(r,f) = { r with p00 = f r.p00 }
                }
            let p10 =
                { new Lens<DrawRects.Rect, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.p10
                    override x.Set(r,v) = { r with p10 = v }
                    override x.Update(r,f) = { r with p10 = f r.p10 }
                }
            let p11 =
                { new Lens<DrawRects.Rect, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.p11
                    override x.Set(r,v) = { r with p11 = v }
                    override x.Update(r,f) = { r with p11 = f r.p11 }
                }
            let p01 =
                { new Lens<DrawRects.Rect, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.p01
                    override x.Set(r,v) = { r with p01 = v }
                    override x.Update(r,f) = { r with p01 = f r.p01 }
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
        let _currentInteraction = ResetMod.Create(__initial.currentInteraction)
        
        member x.viewport = _viewport :> IMod<_>
        member x.selectedRect = _selectedRect :> IMod<_>
        member x.workingRect = _workingRect :> IMod<_>
        member x.currentInteraction = _currentInteraction :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawRects.ClientState) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_viewport,v.viewport)
                MOption.Update(_selectedRect, v.selectedRect)
                MOption.Update(_workingRect, v.workingRect)
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
                { new Lens<DrawRects.ClientState, Microsoft.FSharp.Core.Option<Aardvark.Base.Box2d>>() with
                    override x.Get(r) = r.workingRect
                    override x.Set(r,v) = { r with workingRect = v }
                    override x.Update(r,f) = { r with workingRect = f r.workingRect }
                }
            let currentInteraction =
                { new Lens<DrawRects.ClientState, DrawRects.Interaction>() with
                    override x.Get(r) = r.currentInteraction
                    override x.Set(r,v) = { r with currentInteraction = v }
                    override x.Update(r,f) = { r with currentInteraction = f r.currentInteraction }
                }
