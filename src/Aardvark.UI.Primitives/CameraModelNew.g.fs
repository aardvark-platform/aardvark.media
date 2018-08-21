namespace Aardvark.UI.Primitives.CameraControllers.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives.CameraControllers.Model

[<AutoOpen>]
module Mutable =

    
    
    type MCameraControllerState(__initial : Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState>
        let _view = ResetMod.Create(__initial.view)
        let _dragStart = ResetMod.Create(__initial.dragStart)
        let _movePos = ResetMod.Create(__initial.movePos)
        let _look = ResetMod.Create(__initial.look)
        let _zoom = ResetMod.Create(__initial.zoom)
        let _pan = ResetMod.Create(__initial.pan)
        let _pendingMove = ResetMod.Create(__initial.pendingMove)
        let _forward = MOption.Create(__initial.forward)
        let _backward = MOption.Create(__initial.backward)
        let _left = MOption.Create(__initial.left)
        let _right = MOption.Create(__initial.right)
        let _moveVec = ResetMod.Create(__initial.moveVec)
        let _moveSpeed = ResetMod.Create(__initial.moveSpeed)
        let _orbitCenter = MOption.Create(__initial.orbitCenter)
        let _lastTime = MOption.Create(__initial.lastTime)
        let _isWheel = ResetMod.Create(__initial.isWheel)
        let _sensitivity = ResetMod.Create(__initial.sensitivity)
        let _scrollSensitivity = ResetMod.Create(__initial.scrollSensitivity)
        let _scrolling = ResetMod.Create(__initial.scrolling)
        let _zoomFactor = ResetMod.Create(__initial.zoomFactor)
        let _panFactor = ResetMod.Create(__initial.panFactor)
        let _rotationFactor = ResetMod.Create(__initial.rotationFactor)
        let _stash = ResetMod.Create(__initial.stash)
        
        member x.view = _view :> IMod<_>
        member x.dragStart = _dragStart :> IMod<_>
        member x.movePos = _movePos :> IMod<_>
        member x.look = _look :> IMod<_>
        member x.zoom = _zoom :> IMod<_>
        member x.pan = _pan :> IMod<_>
        member x.pendingMove = _pendingMove :> IMod<_>
        member x.forward = _forward :> IMod<_>
        member x.backward = _backward :> IMod<_>
        member x.left = _left :> IMod<_>
        member x.right = _right :> IMod<_>
        member x.moveVec = _moveVec :> IMod<_>
        member x.moveSpeed = _moveSpeed :> IMod<_>
        member x.orbitCenter = _orbitCenter :> IMod<_>
        member x.lastTime = _lastTime :> IMod<_>
        member x.isWheel = _isWheel :> IMod<_>
        member x.sensitivity = _sensitivity :> IMod<_>
        member x.scrollSensitivity = _scrollSensitivity :> IMod<_>
        member x.scrolling = _scrolling :> IMod<_>
        member x.zoomFactor = _zoomFactor :> IMod<_>
        member x.panFactor = _panFactor :> IMod<_>
        member x.rotationFactor = _rotationFactor :> IMod<_>
        member x.stash = _stash :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_view,v.view)
                ResetMod.Update(_dragStart,v.dragStart)
                ResetMod.Update(_movePos,v.movePos)
                ResetMod.Update(_look,v.look)
                ResetMod.Update(_zoom,v.zoom)
                ResetMod.Update(_pan,v.pan)
                ResetMod.Update(_pendingMove,v.pendingMove)
                MOption.Update(_forward, v.forward)
                MOption.Update(_backward, v.backward)
                MOption.Update(_left, v.left)
                MOption.Update(_right, v.right)
                ResetMod.Update(_moveVec,v.moveVec)
                ResetMod.Update(_moveSpeed,v.moveSpeed)
                MOption.Update(_orbitCenter, v.orbitCenter)
                MOption.Update(_lastTime, v.lastTime)
                ResetMod.Update(_isWheel,v.isWheel)
                ResetMod.Update(_sensitivity,v.sensitivity)
                ResetMod.Update(_scrollSensitivity,v.scrollSensitivity)
                ResetMod.Update(_scrolling,v.scrolling)
                ResetMod.Update(_zoomFactor,v.zoomFactor)
                ResetMod.Update(_panFactor,v.panFactor)
                ResetMod.Update(_rotationFactor,v.rotationFactor)
                _stash.Update(v.stash)
                
        
        static member Create(__initial : Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState) : MCameraControllerState = MCameraControllerState(__initial)
        static member Update(m : MCameraControllerState, v : Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CameraControllerState =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let view =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Aardvark.Base.CameraView>() with
                    override x.Get(r) = r.view
                    override x.Set(r,v) = { r with view = v }
                    override x.Update(r,f) = { r with view = f r.view }
                }
            let dragStart =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Aardvark.Base.V2i>() with
                    override x.Get(r) = r.dragStart
                    override x.Set(r,v) = { r with dragStart = v }
                    override x.Update(r,f) = { r with dragStart = f r.dragStart }
                }
            let movePos =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Aardvark.Base.V2i>() with
                    override x.Get(r) = r.movePos
                    override x.Set(r,v) = { r with movePos = v }
                    override x.Update(r,f) = { r with movePos = f r.movePos }
                }
            let look =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.look
                    override x.Set(r,v) = { r with look = v }
                    override x.Update(r,f) = { r with look = f r.look }
                }
            let zoom =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.zoom
                    override x.Set(r,v) = { r with zoom = v }
                    override x.Update(r,f) = { r with zoom = f r.zoom }
                }
            let pan =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.pan
                    override x.Set(r,v) = { r with pan = v }
                    override x.Update(r,f) = { r with pan = f r.pan }
                }
            let pendingMove =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Aardvark.Base.V3d>() with
                    override x.Get(r) = r.pendingMove
                    override x.Set(r,v) = { r with pendingMove = v }
                    override x.Update(r,f) = { r with pendingMove = f r.pendingMove }
                }
            let forward =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<System.TimeSpan>>() with
                    override x.Get(r) = r.forward
                    override x.Set(r,v) = { r with forward = v }
                    override x.Update(r,f) = { r with forward = f r.forward }
                }
            let backward =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<System.TimeSpan>>() with
                    override x.Get(r) = r.backward
                    override x.Set(r,v) = { r with backward = v }
                    override x.Update(r,f) = { r with backward = f r.backward }
                }
            let left =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<System.TimeSpan>>() with
                    override x.Get(r) = r.left
                    override x.Set(r,v) = { r with left = v }
                    override x.Update(r,f) = { r with left = f r.left }
                }
            let right =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<System.TimeSpan>>() with
                    override x.Get(r) = r.right
                    override x.Set(r,v) = { r with right = v }
                    override x.Update(r,f) = { r with right = f r.right }
                }
            let moveVec =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Aardvark.Base.V3i>() with
                    override x.Get(r) = r.moveVec
                    override x.Set(r,v) = { r with moveVec = v }
                    override x.Update(r,f) = { r with moveVec = f r.moveVec }
                }
            let moveSpeed =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.moveSpeed
                    override x.Set(r,v) = { r with moveSpeed = v }
                    override x.Update(r,f) = { r with moveSpeed = f r.moveSpeed }
                }
            let orbitCenter =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.orbitCenter
                    override x.Set(r,v) = { r with orbitCenter = v }
                    override x.Update(r,f) = { r with orbitCenter = f r.orbitCenter }
                }
            let lastTime =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<System.Double>>() with
                    override x.Get(r) = r.lastTime
                    override x.Set(r,v) = { r with lastTime = v }
                    override x.Update(r,f) = { r with lastTime = f r.lastTime }
                }
            let isWheel =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.isWheel
                    override x.Set(r,v) = { r with isWheel = v }
                    override x.Update(r,f) = { r with isWheel = f r.isWheel }
                }
            let sensitivity =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.sensitivity
                    override x.Set(r,v) = { r with sensitivity = v }
                    override x.Update(r,f) = { r with sensitivity = f r.sensitivity }
                }
            let scrollSensitivity =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.scrollSensitivity
                    override x.Set(r,v) = { r with scrollSensitivity = v }
                    override x.Update(r,f) = { r with scrollSensitivity = f r.scrollSensitivity }
                }
            let scrolling =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.scrolling
                    override x.Set(r,v) = { r with scrolling = v }
                    override x.Update(r,f) = { r with scrolling = f r.scrolling }
                }
            let zoomFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.zoomFactor
                    override x.Set(r,v) = { r with zoomFactor = v }
                    override x.Update(r,f) = { r with zoomFactor = f r.zoomFactor }
                }
            let panFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.panFactor
                    override x.Set(r,v) = { r with panFactor = v }
                    override x.Update(r,f) = { r with panFactor = f r.panFactor }
                }
            let rotationFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.rotationFactor
                    override x.Set(r,v) = { r with rotationFactor = v }
                    override x.Update(r,f) = { r with rotationFactor = f r.rotationFactor }
                }
            let stash =
                { new Lens<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.UI.Primitives.CameraControllers.Model.CameraControllerState>>() with
                    override x.Get(r) = r.stash
                    override x.Set(r,v) = { r with stash = v }
                    override x.Update(r,f) = { r with stash = f r.stash }
                }
