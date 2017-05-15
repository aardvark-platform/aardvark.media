namespace Aardvark.UI.Primitives

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MCameraControllerState private(__initial : Aardvark.UI.Primitives.CameraControllerState) =
        let mutable __current = __initial
        let _view = ResetMod(__initial.view)
        let _dragStart = ResetMod(__initial.dragStart)
        let _look = ResetMod(__initial.look)
        let _zoom = ResetMod(__initial.zoom)
        let _pan = ResetMod(__initial.pan)
        let _forward = ResetMod(__initial.forward)
        let _backward = ResetMod(__initial.backward)
        let _left = ResetMod(__initial.left)
        let _right = ResetMod(__initial.right)
        let _moveVec = ResetMod(__initial.moveVec)
        let _orbitCenter = ResetMod(__initial.orbitCenter)
        let _lastTime = ResetMod(__initial.lastTime)
        let _stash = ResetMod(__initial.stash)
        
        member x.view = _view :> IMod<_>
        member x.dragStart = _dragStart :> IMod<_>
        member x.look = _look :> IMod<_>
        member x.zoom = _zoom :> IMod<_>
        member x.pan = _pan :> IMod<_>
        member x.forward = _forward :> IMod<_>
        member x.backward = _backward :> IMod<_>
        member x.left = _left :> IMod<_>
        member x.right = _right :> IMod<_>
        member x.moveVec = _moveVec :> IMod<_>
        member x.orbitCenter = _orbitCenter :> IMod<_>
        member x.lastTime = _lastTime :> IMod<_>
        member x.stash = _stash :> IMod<_>
        
        member x.Update(__model : Aardvark.UI.Primitives.CameraControllerState) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _view.Update(__model.view)
                _dragStart.Update(__model.dragStart)
                _look.Update(__model.look)
                _zoom.Update(__model.zoom)
                _pan.Update(__model.pan)
                _forward.Update(__model.forward)
                _backward.Update(__model.backward)
                _left.Update(__model.left)
                _right.Update(__model.right)
                _moveVec.Update(__model.moveVec)
                _orbitCenter.Update(__model.orbitCenter)
                _lastTime.Update(__model.lastTime)
                _stash.Update(__model.stash)
        
        static member Create(initial) = MCameraControllerState(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MCameraControllerState =
        let inline view (m : MCameraControllerState) = m.view
        let inline dragStart (m : MCameraControllerState) = m.dragStart
        let inline look (m : MCameraControllerState) = m.look
        let inline zoom (m : MCameraControllerState) = m.zoom
        let inline pan (m : MCameraControllerState) = m.pan
        let inline forward (m : MCameraControllerState) = m.forward
        let inline backward (m : MCameraControllerState) = m.backward
        let inline left (m : MCameraControllerState) = m.left
        let inline right (m : MCameraControllerState) = m.right
        let inline moveVec (m : MCameraControllerState) = m.moveVec
        let inline orbitCenter (m : MCameraControllerState) = m.orbitCenter
        let inline lastTime (m : MCameraControllerState) = m.lastTime
        let inline stash (m : MCameraControllerState) = m.stash
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CameraControllerState =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let view =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.CameraView>() with
                    override x.Get(r) = r.view
                    override x.Set(r,v) = { r with view = v }
                    override x.Update(r,f) = { r with view = f r.view }
                }
            let dragStart =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.V2i>() with
                    override x.Get(r) = r.dragStart
                    override x.Set(r,v) = { r with dragStart = v }
                    override x.Update(r,f) = { r with dragStart = f r.dragStart }
                }
            let look =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.look
                    override x.Set(r,v) = { r with look = v }
                    override x.Update(r,f) = { r with look = f r.look }
                }
            let zoom =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.zoom
                    override x.Set(r,v) = { r with zoom = v }
                    override x.Update(r,f) = { r with zoom = f r.zoom }
                }
            let pan =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.pan
                    override x.Set(r,v) = { r with pan = v }
                    override x.Update(r,f) = { r with pan = f r.pan }
                }
            let forward =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.forward
                    override x.Set(r,v) = { r with forward = v }
                    override x.Update(r,f) = { r with forward = f r.forward }
                }
            let backward =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.backward
                    override x.Set(r,v) = { r with backward = v }
                    override x.Update(r,f) = { r with backward = f r.backward }
                }
            let left =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.left
                    override x.Set(r,v) = { r with left = v }
                    override x.Update(r,f) = { r with left = f r.left }
                }
            let right =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.right
                    override x.Set(r,v) = { r with right = v }
                    override x.Update(r,f) = { r with right = f r.right }
                }
            let moveVec =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.V3i>() with
                    override x.Get(r) = r.moveVec
                    override x.Set(r,v) = { r with moveVec = v }
                    override x.Update(r,f) = { r with moveVec = f r.moveVec }
                }
            let orbitCenter =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.orbitCenter
                    override x.Set(r,v) = { r with orbitCenter = v }
                    override x.Update(r,f) = { r with orbitCenter = f r.orbitCenter }
                }
            let lastTime =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<Microsoft.FSharp.Core.float>>() with
                    override x.Get(r) = r.lastTime
                    override x.Set(r,v) = { r with lastTime = v }
                    override x.Update(r,f) = { r with lastTime = f r.lastTime }
                }
            let stash =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.UI.Primitives.CameraControllerState>>() with
                    override x.Get(r) = r.stash
                    override x.Set(r,v) = { r with stash = v }
                    override x.Update(r,f) = { r with stash = f r.stash }
                }
