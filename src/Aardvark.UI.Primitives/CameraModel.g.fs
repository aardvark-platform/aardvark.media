namespace Aardvark.UI.Primitives

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives

[<AutoOpen>]
module Mutable =

    
    
    type MFreeFlyConfig(__initial : Aardvark.UI.Primitives.FreeFlyConfig) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.FreeFlyConfig> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Primitives.FreeFlyConfig>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.FreeFlyConfig>
        let _lookAtMouseSensitivity = ResetMod.Create(__initial.lookAtMouseSensitivity)
        let _lookAtConstant = ResetMod.Create(__initial.lookAtConstant)
        let _lookAtDamping = ResetMod.Create(__initial.lookAtDamping)
        let _panMouseSensitivity = ResetMod.Create(__initial.panMouseSensitivity)
        let _panConstant = ResetMod.Create(__initial.panConstant)
        let _panDamping = ResetMod.Create(__initial.panDamping)
        let _dollyMouseSensitivity = ResetMod.Create(__initial.dollyMouseSensitivity)
        let _dollyConstant = ResetMod.Create(__initial.dollyConstant)
        let _dollyDamping = ResetMod.Create(__initial.dollyDamping)
        let _zoomMouseWheelSensitivity = ResetMod.Create(__initial.zoomMouseWheelSensitivity)
        let _zoomConstant = ResetMod.Create(__initial.zoomConstant)
        let _zoomDamping = ResetMod.Create(__initial.zoomDamping)
        let _moveSensitivity = ResetMod.Create(__initial.moveSensitivity)
        
        member x.lookAtMouseSensitivity = _lookAtMouseSensitivity :> IMod<_>
        member x.lookAtConstant = _lookAtConstant :> IMod<_>
        member x.lookAtDamping = _lookAtDamping :> IMod<_>
        member x.panMouseSensitivity = _panMouseSensitivity :> IMod<_>
        member x.panConstant = _panConstant :> IMod<_>
        member x.panDamping = _panDamping :> IMod<_>
        member x.dollyMouseSensitivity = _dollyMouseSensitivity :> IMod<_>
        member x.dollyConstant = _dollyConstant :> IMod<_>
        member x.dollyDamping = _dollyDamping :> IMod<_>
        member x.zoomMouseWheelSensitivity = _zoomMouseWheelSensitivity :> IMod<_>
        member x.zoomConstant = _zoomConstant :> IMod<_>
        member x.zoomDamping = _zoomDamping :> IMod<_>
        member x.moveSensitivity = _moveSensitivity :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Primitives.FreeFlyConfig) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_lookAtMouseSensitivity,v.lookAtMouseSensitivity)
                ResetMod.Update(_lookAtConstant,v.lookAtConstant)
                ResetMod.Update(_lookAtDamping,v.lookAtDamping)
                ResetMod.Update(_panMouseSensitivity,v.panMouseSensitivity)
                ResetMod.Update(_panConstant,v.panConstant)
                ResetMod.Update(_panDamping,v.panDamping)
                ResetMod.Update(_dollyMouseSensitivity,v.dollyMouseSensitivity)
                ResetMod.Update(_dollyConstant,v.dollyConstant)
                ResetMod.Update(_dollyDamping,v.dollyDamping)
                ResetMod.Update(_zoomMouseWheelSensitivity,v.zoomMouseWheelSensitivity)
                ResetMod.Update(_zoomConstant,v.zoomConstant)
                ResetMod.Update(_zoomDamping,v.zoomDamping)
                ResetMod.Update(_moveSensitivity,v.moveSensitivity)
                
        
        static member Create(__initial : Aardvark.UI.Primitives.FreeFlyConfig) : MFreeFlyConfig = MFreeFlyConfig(__initial)
        static member Update(m : MFreeFlyConfig, v : Aardvark.UI.Primitives.FreeFlyConfig) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Primitives.FreeFlyConfig> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module FreeFlyConfig =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let lookAtMouseSensitivity =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.lookAtMouseSensitivity
                    override x.Set(r,v) = { r with lookAtMouseSensitivity = v }
                    override x.Update(r,f) = { r with lookAtMouseSensitivity = f r.lookAtMouseSensitivity }
                }
            let lookAtConstant =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.lookAtConstant
                    override x.Set(r,v) = { r with lookAtConstant = v }
                    override x.Update(r,f) = { r with lookAtConstant = f r.lookAtConstant }
                }
            let lookAtDamping =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.lookAtDamping
                    override x.Set(r,v) = { r with lookAtDamping = v }
                    override x.Update(r,f) = { r with lookAtDamping = f r.lookAtDamping }
                }
            let panMouseSensitivity =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.panMouseSensitivity
                    override x.Set(r,v) = { r with panMouseSensitivity = v }
                    override x.Update(r,f) = { r with panMouseSensitivity = f r.panMouseSensitivity }
                }
            let panConstant =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.panConstant
                    override x.Set(r,v) = { r with panConstant = v }
                    override x.Update(r,f) = { r with panConstant = f r.panConstant }
                }
            let panDamping =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.panDamping
                    override x.Set(r,v) = { r with panDamping = v }
                    override x.Update(r,f) = { r with panDamping = f r.panDamping }
                }
            let dollyMouseSensitivity =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.dollyMouseSensitivity
                    override x.Set(r,v) = { r with dollyMouseSensitivity = v }
                    override x.Update(r,f) = { r with dollyMouseSensitivity = f r.dollyMouseSensitivity }
                }
            let dollyConstant =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.dollyConstant
                    override x.Set(r,v) = { r with dollyConstant = v }
                    override x.Update(r,f) = { r with dollyConstant = f r.dollyConstant }
                }
            let dollyDamping =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.dollyDamping
                    override x.Set(r,v) = { r with dollyDamping = v }
                    override x.Update(r,f) = { r with dollyDamping = f r.dollyDamping }
                }
            let zoomMouseWheelSensitivity =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.zoomMouseWheelSensitivity
                    override x.Set(r,v) = { r with zoomMouseWheelSensitivity = v }
                    override x.Update(r,f) = { r with zoomMouseWheelSensitivity = f r.zoomMouseWheelSensitivity }
                }
            let zoomConstant =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.zoomConstant
                    override x.Set(r,v) = { r with zoomConstant = v }
                    override x.Update(r,f) = { r with zoomConstant = f r.zoomConstant }
                }
            let zoomDamping =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.zoomDamping
                    override x.Set(r,v) = { r with zoomDamping = v }
                    override x.Update(r,f) = { r with zoomDamping = f r.zoomDamping }
                }
            let moveSensitivity =
                { new Lens<Aardvark.UI.Primitives.FreeFlyConfig, System.Double>() with
                    override x.Get(r) = r.moveSensitivity
                    override x.Set(r,v) = { r with moveSensitivity = v }
                    override x.Update(r,f) = { r with moveSensitivity = f r.moveSensitivity }
                }
    
    
    type MCameraControllerState(__initial : Aardvark.UI.Primitives.CameraControllerState) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.CameraControllerState> = Aardvark.Base.Incremental.EqModRef<Aardvark.UI.Primitives.CameraControllerState>(__initial) :> Aardvark.Base.Incremental.IModRef<Aardvark.UI.Primitives.CameraControllerState>
        let _view = ResetMod.Create(__initial.view)
        let _dragStart = ResetMod.Create(__initial.dragStart)
        let _movePos = ResetMod.Create(__initial.movePos)
        let _look = ResetMod.Create(__initial.look)
        let _zoom = ResetMod.Create(__initial.zoom)
        let _pan = ResetMod.Create(__initial.pan)
        let _dolly = ResetMod.Create(__initial.dolly)
        let _isWheel = ResetMod.Create(__initial.isWheel)
        let _scrolling = ResetMod.Create(__initial.scrolling)
        let _forward = ResetMod.Create(__initial.forward)
        let _backward = ResetMod.Create(__initial.backward)
        let _left = ResetMod.Create(__initial.left)
        let _right = ResetMod.Create(__initial.right)
        let _moveVec = ResetMod.Create(__initial.moveVec)
        let _moveSpeed = ResetMod.Create(__initial.moveSpeed)
        let _panSpeed = ResetMod.Create(__initial.panSpeed)
        let _orbitCenter = MOption.Create(__initial.orbitCenter)
        let _lastTime = MOption.Create(__initial.lastTime)
        let _animating = ResetMod.Create(__initial.animating)
        let _sensitivity = ResetMod.Create(__initial.sensitivity)
        let _scrollSensitivity = ResetMod.Create(__initial.scrollSensitivity)
        let _zoomFactor = ResetMod.Create(__initial.zoomFactor)
        let _panFactor = ResetMod.Create(__initial.panFactor)
        let _rotationFactor = ResetMod.Create(__initial.rotationFactor)
        let _targetPhiTheta = ResetMod.Create(__initial.targetPhiTheta)
        let _targetPan = ResetMod.Create(__initial.targetPan)
        let _targetDolly = ResetMod.Create(__initial.targetDolly)
        let _targetZoom = ResetMod.Create(__initial.targetZoom)
        let _freeFlyConfig = MFreeFlyConfig.Create(__initial.freeFlyConfig)
        let _stash = ResetMod.Create(__initial.stash)
        
        member x.view = _view :> IMod<_>
        member x.dragStart = _dragStart :> IMod<_>
        member x.movePos = _movePos :> IMod<_>
        member x.look = _look :> IMod<_>
        member x.zoom = _zoom :> IMod<_>
        member x.pan = _pan :> IMod<_>
        member x.dolly = _dolly :> IMod<_>
        member x.isWheel = _isWheel :> IMod<_>
        member x.scrolling = _scrolling :> IMod<_>
        member x.forward = _forward :> IMod<_>
        member x.backward = _backward :> IMod<_>
        member x.left = _left :> IMod<_>
        member x.right = _right :> IMod<_>
        member x.moveVec = _moveVec :> IMod<_>
        member x.moveSpeed = _moveSpeed :> IMod<_>
        member x.panSpeed = _panSpeed :> IMod<_>
        member x.orbitCenter = _orbitCenter :> IMod<_>
        member x.lastTime = _lastTime :> IMod<_>
        member x.animating = _animating :> IMod<_>
        member x.sensitivity = _sensitivity :> IMod<_>
        member x.scrollSensitivity = _scrollSensitivity :> IMod<_>
        member x.zoomFactor = _zoomFactor :> IMod<_>
        member x.panFactor = _panFactor :> IMod<_>
        member x.rotationFactor = _rotationFactor :> IMod<_>
        member x.targetPhiTheta = _targetPhiTheta :> IMod<_>
        member x.targetPan = _targetPan :> IMod<_>
        member x.targetDolly = _targetDolly :> IMod<_>
        member x.targetZoom = _targetZoom :> IMod<_>
        member x.freeFlyConfig = _freeFlyConfig
        member x.stash = _stash :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Aardvark.UI.Primitives.CameraControllerState) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_view,v.view)
                ResetMod.Update(_dragStart,v.dragStart)
                ResetMod.Update(_movePos,v.movePos)
                ResetMod.Update(_look,v.look)
                ResetMod.Update(_zoom,v.zoom)
                ResetMod.Update(_pan,v.pan)
                ResetMod.Update(_dolly,v.dolly)
                ResetMod.Update(_isWheel,v.isWheel)
                ResetMod.Update(_scrolling,v.scrolling)
                ResetMod.Update(_forward,v.forward)
                ResetMod.Update(_backward,v.backward)
                ResetMod.Update(_left,v.left)
                ResetMod.Update(_right,v.right)
                ResetMod.Update(_moveVec,v.moveVec)
                ResetMod.Update(_moveSpeed,v.moveSpeed)
                ResetMod.Update(_panSpeed,v.panSpeed)
                MOption.Update(_orbitCenter, v.orbitCenter)
                MOption.Update(_lastTime, v.lastTime)
                ResetMod.Update(_animating,v.animating)
                ResetMod.Update(_sensitivity,v.sensitivity)
                ResetMod.Update(_scrollSensitivity,v.scrollSensitivity)
                ResetMod.Update(_zoomFactor,v.zoomFactor)
                ResetMod.Update(_panFactor,v.panFactor)
                ResetMod.Update(_rotationFactor,v.rotationFactor)
                ResetMod.Update(_targetPhiTheta,v.targetPhiTheta)
                ResetMod.Update(_targetPan,v.targetPan)
                ResetMod.Update(_targetDolly,v.targetDolly)
                ResetMod.Update(_targetZoom,v.targetZoom)
                MFreeFlyConfig.Update(_freeFlyConfig, v.freeFlyConfig)
                _stash.Update(v.stash)
                
        
        static member Create(__initial : Aardvark.UI.Primitives.CameraControllerState) : MCameraControllerState = MCameraControllerState(__initial)
        static member Update(m : MCameraControllerState, v : Aardvark.UI.Primitives.CameraControllerState) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Aardvark.UI.Primitives.CameraControllerState> with
            member x.Update v = x.Update v
    
    
    
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
            let movePos =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.V2i>() with
                    override x.Get(r) = r.movePos
                    override x.Set(r,v) = { r with movePos = v }
                    override x.Update(r,f) = { r with movePos = f r.movePos }
                }
            let look =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.look
                    override x.Set(r,v) = { r with look = v }
                    override x.Update(r,f) = { r with look = f r.look }
                }
            let zoom =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.zoom
                    override x.Set(r,v) = { r with zoom = v }
                    override x.Update(r,f) = { r with zoom = f r.zoom }
                }
            let pan =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.pan
                    override x.Set(r,v) = { r with pan = v }
                    override x.Update(r,f) = { r with pan = f r.pan }
                }
            let dolly =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.dolly
                    override x.Set(r,v) = { r with dolly = v }
                    override x.Update(r,f) = { r with dolly = f r.dolly }
                }
            let isWheel =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.isWheel
                    override x.Set(r,v) = { r with isWheel = v }
                    override x.Update(r,f) = { r with isWheel = f r.isWheel }
                }
            let scrolling =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.scrolling
                    override x.Set(r,v) = { r with scrolling = v }
                    override x.Update(r,f) = { r with scrolling = f r.scrolling }
                }
            let forward =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.forward
                    override x.Set(r,v) = { r with forward = v }
                    override x.Update(r,f) = { r with forward = f r.forward }
                }
            let backward =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.backward
                    override x.Set(r,v) = { r with backward = v }
                    override x.Update(r,f) = { r with backward = f r.backward }
                }
            let left =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.left
                    override x.Set(r,v) = { r with left = v }
                    override x.Update(r,f) = { r with left = f r.left }
                }
            let right =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
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
            let moveSpeed =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.moveSpeed
                    override x.Set(r,v) = { r with moveSpeed = v }
                    override x.Update(r,f) = { r with moveSpeed = f r.moveSpeed }
                }
            let panSpeed =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.panSpeed
                    override x.Set(r,v) = { r with panSpeed = v }
                    override x.Update(r,f) = { r with panSpeed = f r.panSpeed }
                }
            let orbitCenter =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.orbitCenter
                    override x.Set(r,v) = { r with orbitCenter = v }
                    override x.Update(r,f) = { r with orbitCenter = f r.orbitCenter }
                }
            let lastTime =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<System.Double>>() with
                    override x.Get(r) = r.lastTime
                    override x.Set(r,v) = { r with lastTime = v }
                    override x.Update(r,f) = { r with lastTime = f r.lastTime }
                }
            let animating =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Boolean>() with
                    override x.Get(r) = r.animating
                    override x.Set(r,v) = { r with animating = v }
                    override x.Update(r,f) = { r with animating = f r.animating }
                }
            let sensitivity =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.sensitivity
                    override x.Set(r,v) = { r with sensitivity = v }
                    override x.Update(r,f) = { r with sensitivity = f r.sensitivity }
                }
            let scrollSensitivity =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.scrollSensitivity
                    override x.Set(r,v) = { r with scrollSensitivity = v }
                    override x.Update(r,f) = { r with scrollSensitivity = f r.scrollSensitivity }
                }
            let zoomFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.zoomFactor
                    override x.Set(r,v) = { r with zoomFactor = v }
                    override x.Update(r,f) = { r with zoomFactor = f r.zoomFactor }
                }
            let panFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.panFactor
                    override x.Set(r,v) = { r with panFactor = v }
                    override x.Update(r,f) = { r with panFactor = f r.panFactor }
                }
            let rotationFactor =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.rotationFactor
                    override x.Set(r,v) = { r with rotationFactor = v }
                    override x.Update(r,f) = { r with rotationFactor = f r.rotationFactor }
                }
            let targetPhiTheta =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.targetPhiTheta
                    override x.Set(r,v) = { r with targetPhiTheta = v }
                    override x.Update(r,f) = { r with targetPhiTheta = f r.targetPhiTheta }
                }
            let targetPan =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.targetPan
                    override x.Set(r,v) = { r with targetPan = v }
                    override x.Update(r,f) = { r with targetPan = f r.targetPan }
                }
            let targetDolly =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.targetDolly
                    override x.Set(r,v) = { r with targetDolly = v }
                    override x.Update(r,f) = { r with targetDolly = f r.targetDolly }
                }
            let targetZoom =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, System.Double>() with
                    override x.Get(r) = r.targetZoom
                    override x.Set(r,v) = { r with targetZoom = v }
                    override x.Update(r,f) = { r with targetZoom = f r.targetZoom }
                }
            let freeFlyConfig =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Aardvark.UI.Primitives.FreeFlyConfig>() with
                    override x.Get(r) = r.freeFlyConfig
                    override x.Set(r,v) = { r with freeFlyConfig = v }
                    override x.Update(r,f) = { r with freeFlyConfig = f r.freeFlyConfig }
                }
            let stash =
                { new Lens<Aardvark.UI.Primitives.CameraControllerState, Microsoft.FSharp.Core.Option<Aardvark.UI.Primitives.CameraControllerState>>() with
                    override x.Get(r) = r.stash
                    override x.Set(r,v) = { r with stash = v }
                    override x.Update(r,f) = { r with stash = f r.stash }
                }
