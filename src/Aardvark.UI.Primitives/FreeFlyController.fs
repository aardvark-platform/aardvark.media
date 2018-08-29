namespace Aardvark.UI.Primitives

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Service

open Aardvark.UI.Primitives


module FreeFlyController =
    open Aardvark.Base.Incremental.Operators    
    
    type CameraMotion = { dPos : V3d; dRot : V3d; dMoveSpeed : float; dZoom : float; dPan : V2d; dDolly : float } with
        static member Zero = { dPos = V3d.Zero; dRot = V3d.Zero; dMoveSpeed = 0.0; dZoom = 0.0; dPan = V2d.Zero; dDolly = 0.0 }

        static member (+) (cam : CameraView, motion : CameraMotion) =
            let cam = 
                cam.WithLocation(
                    cam.Location +
                    motion.dPos.X * cam.Right +
                    motion.dPos.Y * cam.Up +
                    motion.dPos.Z * cam.Forward
                )

            let cam = 
                cam.WithLocation(
                    cam.Location +
                    motion.dPan.X * cam.Right  +
                    motion.dPan.Y * cam.Up 
                )
            
            let cam =
                cam.WithLocation(
                    cam.Location +
                    motion.dDolly * cam.Forward
                )

            let cam =
                cam.WithLocation(
                    cam.Location +
                    motion.dZoom * cam.Forward
                )
            
            let cam =
                let trafo =
                    M44d.Rotation(cam.Right, motion.dRot.X) *
                    M44d.Rotation(cam.Sky, motion.dRot.Y) *
                    M44d.Rotation(cam.Forward, motion.dRot.Z)
                    
                let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                cam.WithForward newForward

            cam

        static member (+) (l : CameraMotion, r : CameraMotion) =
            // TODO: correct?
            let trafo =
                M44d.Rotation(V3d.IOO, l.dRot.X) *
                M44d.Rotation(V3d.OIO, l.dRot.Y) *
                M44d.Rotation(V3d.OOI, l.dRot.Z)
                    
            {
                dPos = l.dPos + trafo.TransformDir r.dPos
                dRot = l.dRot + r.dRot
                dMoveSpeed = l.dMoveSpeed + r.dMoveSpeed
                dZoom = l.dZoom + r.dZoom
                dPan = l.dPan + r.dPan
                dDolly = l.dDolly + r.dDolly
            }

        static member (*) (motion : CameraMotion, f : float) =
            { dPos = motion.dPos * f; dRot = motion.dRot * f; dMoveSpeed = motion.dMoveSpeed * f; dZoom = motion.dZoom * f; dPan = motion.dPan * f; dDolly = motion.dDolly * f}

        static member (*) (f : float, motion : CameraMotion) = motion * f
            

        static member Move(dPos : V3d) = { dPos = dPos; dRot = V3d.Zero; dMoveSpeed = 0.0; dZoom = 0.0; dPan = V2d.Zero; dDolly = 0.0 }
        static member Rotate(dRot : V3d) = { dPos = V3d.Zero; dRot = dRot; dMoveSpeed = 0.0; dZoom = 0.0; dPan = V2d.Zero; dDolly = 0.0 }


        static member (+) (state : CameraControllerState, motion : CameraMotion) =
            let clamping (max : float) (v : float) =
                if max < 0.0 then
                    if v < max then max
                    else v
                else
                    if v > max then max
                    else v
                
            let motion = 
                { motion with 
                    dRot = V3d(clamping state.targetPhiTheta.Y motion.dRot.X, clamping state.targetPhiTheta.X motion.dRot.Y , motion.dRot.Z)
                }

            { state with 
                view = state.view + motion
                moveSpeed = state.moveSpeed + motion.dMoveSpeed 
                targetPhiTheta = state.targetPhiTheta - V2d(motion.dRot.Y, motion.dRot.X)
                targetZoom = state.targetZoom - motion.dZoom
                targetPan = state.targetPan - motion.dPan
                targetDolly = state.targetDolly - motion.dDolly
            }

    type Message =     
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Wheel of V2d
        | Move of V2i
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Blur
        | Interpolate
        | Rendered

    let initial =
        {
            view = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
                                    
            orbitCenter = None
            stash = None
            sensitivity = 1.0
            panFactor  = 0.01
            zoomFactor = 0.01
            rotationFactor = 0.01            
            dolly = false

            lastTime = None
            moveVec = V3i.Zero
            dragStart = V2i.Zero
            movePos = V2i.Zero
            look = false; zoom = false; pan = false                    
            forward = false; backward = false; left = false; right = false
            isWheel = false;
            moveSpeed = 0.0
            scrollSensitivity = 0.8
            scrolling = false

            freeFlyConfig = FreeFlyConfig.initial

            targetPhiTheta = V2d.Zero
            targetZoom = 0.0
            targetDolly = 0.0
            animating = false
            targetPan = V2d.Zero
            panSpeed = 0.0
        }

    let initial' (dist:float) =
        { initial with view = CameraView.lookAt (dist * V3d.III) V3d.Zero V3d.OOI }

    let sw = System.Diagnostics.Stopwatch()
    do sw.Start()

    let withTime (model : CameraControllerState) =
        { model with lastTime = Some sw.Elapsed.TotalSeconds; view = model.view.WithLocation(model.view.Location) }

    let dummyChange (model : CameraControllerState) =
        { model with view = model.view.WithLocation(model.view.Location) }

    let startAnimation (model : CameraControllerState) =
        if not model.animating then
            { model with 
                view = model.view.WithLocation model.view.Location
                animating = true
                lastTime = Some sw.Elapsed.TotalSeconds
            }
        else
            model

    let stopAnimation (model : CameraControllerState) =
        if model.animating then
            { model with 
                animating = false
                lastTime = None
            }
        else
            model
    
    let exp x = Math.Pow(Math.E, x)
    
    let update (model : CameraControllerState) (message : Message) =
        match message with
            //| Rendered -> 
            //    if model.animating then dummyChange model else model
            | Blur ->
                { model with 
                    lastTime = None
                    moveVec = V3i.Zero
                    dragStart = V2i.Zero
                    look = false; zoom = false; pan = false                    
                    forward = false; backward = false; left = false; right = false
                }
            | Interpolate | Rendered ->
                let now = sw.Elapsed.TotalSeconds
              
                let clampAbs (maxAbs : float) (v : float) =
                    if abs v > maxAbs then
                        float (sign v) * maxAbs
                    else
                        v

                let move (state : CameraControllerState) =
                    if state.moveVec <> V3i.Zero then
                        { CameraMotion.Zero with
                            dPos = V3d state.moveVec * exp state.freeFlyConfig.moveSensitivity
                        }
                    else
                        CameraMotion.Zero

                let pan (state : CameraControllerState) =
                    if state.targetPan.Length > 0.05 then
                        let tt = (state.freeFlyConfig.panConstant + abs state.targetPan.X * exp (state.freeFlyConfig.panDamping )) * float (sign state.targetPan.X)
                        let tu = (state.freeFlyConfig.panConstant + abs state.targetPan.Y * exp (state.freeFlyConfig.panDamping )) * float (sign state.targetPan.Y)
                        { CameraMotion.Zero with
                            dPan = V2d(tt,tu)
                        }
                    else
                        CameraMotion.Zero
                
                let dolly (state : CameraControllerState) =
                    if abs state.targetDolly > 0.05 then
                        let dd = (state.freeFlyConfig.dollyConstant + abs state.targetDolly * exp (state.freeFlyConfig.dollyDamping )) * float (sign state.targetDolly)
                        { CameraMotion.Zero with
                            dDolly = dd
                        }
                    else
                        CameraMotion.Zero

                let zoom (state : CameraControllerState) =
                    if abs state.targetZoom > 0.05 then
                        let dd = (state.freeFlyConfig.zoomConstant + abs state.targetZoom * exp (state.freeFlyConfig.zoomDamping )) * float (sign state.targetZoom)
                        { CameraMotion.Zero with
                            dZoom = dd
                        }
                    else
                        CameraMotion.Zero

                let look (state : CameraControllerState) =
                    if state.targetPhiTheta <> V2d.Zero then
                    
                        let rr = (state.freeFlyConfig.lookAtConstant + abs state.targetPhiTheta.Y * state.freeFlyConfig.lookAtDamping) * float (sign (state.targetPhiTheta.Y))
                        let ru = (state.freeFlyConfig.lookAtConstant + abs state.targetPhiTheta.X * state.freeFlyConfig.lookAtDamping) * float (sign (state.targetPhiTheta.X))

                        { CameraMotion.Zero with
                            dRot = V3d(rr, ru, 0.0)
                        }
                    else
                        CameraMotion.Zero

                let model = 
                    match model.lastTime with
                        | Some last ->
                            let dt = now - last
                            let step = Integrator.rungeKutta (fun t s -> move s + look s + pan s + dolly s + zoom s)

                            Integrator.integrate 0.0166666 step model dt

                        | None -> 
                            model
                     
                if model.moveVec = V3i.Zero && model.targetPhiTheta = V2d.Zero && (model.targetPan.Length <= 0.05) && (abs model.targetDolly <= 0.05) && (abs model.targetZoom <= 0.05) then
                    stopAnimation model
                else
                    let model = 
                        { model with lastTime = Some now; }
                    if model.animating then 
                        { model with lastTime = Some now; } |> dummyChange 
                    else model 

            | KeyDown Keys.W ->
                if not model.forward then
                    startAnimation { model with forward = true; moveVec = model.moveVec + V3i.OOI  }
                else
                    model

            | KeyUp Keys.W ->
                if model.forward then
                    startAnimation { model with forward = false; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyDown Keys.S ->
                if not model.backward then
                    startAnimation { model with backward = true; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyUp Keys.S ->
                if model.backward then
                    startAnimation { model with backward = false; moveVec = model.moveVec + V3i.OOI  }
                else
                    model

            | Wheel delta ->
                startAnimation 
                    { model with
                        targetZoom = model.targetZoom + (float delta.Y) * model.freeFlyConfig.zoomMouseWheelSensitivity
                    }

            | KeyDown Keys.A ->
                if not model.left then
                    startAnimation { model with left = true; moveVec = model.moveVec - V3i.IOO  }
                else
                    model

            | KeyUp Keys.A ->
                if model.left then
                    startAnimation { model with left = false; moveVec = model.moveVec + V3i.IOO  }
                else
                    model

            | KeyDown Keys.D ->
                if not model.right then
                    startAnimation { model with right = true; moveVec = model.moveVec + V3i.IOO}
                else
                    model

            | KeyUp Keys.D ->
                if model.right then
                    startAnimation { model with right = false; moveVec = model.moveVec - V3i.IOO }
                else
                    model

            | KeyDown _ | KeyUp _ ->
                model

            | Down(button,pos) ->
                let model = withTime { model with dragStart = pos }
                match button with
                    | MouseButtons.Left -> { model with look = true }
                    | MouseButtons.Middle -> { model with pan = true }
                    | MouseButtons.Right -> { model with dolly = true }
                    | _ -> model

            | Up button ->
                match button with
                    | MouseButtons.Left -> { model with look = false }
                    | MouseButtons.Middle -> { model with pan = false }
                    | MouseButtons.Right -> { model with dolly = false }
                    | _ -> model   
                    
            | Move pos  ->
                let delta = pos - model.dragStart

                let look model = 
                    if model.look then
                        let deltaAngle = V2d(float delta.X * -model.freeFlyConfig.lookAtMouseSensitivity, float delta.Y * -model.freeFlyConfig.lookAtMouseSensitivity)
                    
                        startAnimation 
                            { model with 
                                dragStart = pos
                                targetPhiTheta = model.targetPhiTheta + deltaAngle 
                            }
                    else model

                let pan model =
                    if model.pan then
                        startAnimation 
                            { model with
                                targetPan = model.targetPan + (V2d(delta.X,-delta.Y)) * model.freeFlyConfig.panMouseSensitivity
                                dragStart = pos
                            }
                    else 
                        model

                let dolly model =
                    if model.dolly then
                        startAnimation 
                            { model with
                                targetDolly = model.targetDolly + (float -delta.Y) * model.freeFlyConfig.dollyMouseSensitivity
                                dragStart = pos
                            }
                    else 
                        model
                    
                { model with dragStart = pos }
                    |> look
                    |> pan
                    |> dolly


    let update' = flip update



    let attributes (state : MCameraControllerState) (f : Message -> 'msg) = 
         AttributeMap.ofListCond [
            always (onBlur (fun _ -> f Blur))
            always (onMouseDown (fun b p -> f (Down(b,p))))
            onlyWhen (state.look %|| state.pan %|| state.dolly %|| state.zoom) (onMouseUp (fun b p -> f (Up b)))
            always (onKeyDown (KeyDown >> f))
            always (onKeyUp (KeyUp >> f))           
            always (onWheel(fun x -> f (Wheel x)))
            onlyWhen (state.look %|| state.pan %|| state.dolly %|| state.zoom) (onMouseMove (Move >> f))
            //always (onEvent "preRender" [] (fun _ -> f Interpolate))
            always <| onEvent "onRendered" [] (fun _ -> f Rendered)
        ]

    let extractAttributes (state : MCameraControllerState) (f : Message -> 'msg) =
        attributes state f |> AttributeMap.toAMap

    let controlledControlWithClientValues (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (config : RenderControlConfig) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes = AttributeMap.union att (attributes state f)
        let cam = Mod.map2 Camera.create state.view frustum 
        Incremental.renderControlWithClientValues' cam attributes config sg

    let controlledControl (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        controlledControlWithClientValues state f frustum att RenderControlConfig.standard (constF sg)

    let withControls (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (node : DomNode<'msg>) =
        let cam = Mod.map2 Camera.create state.view frustum 
        match node with
            | :? SceneNode<'msg> as node ->
                let getState(c : Aardvark.Service.ClientInfo) =
                    let cam = cam.GetValue(c.token)
                    let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

                    {
                        viewTrafo = CameraView.viewTrafo cam.cameraView
                        projTrafo = Frustum.projTrafo cam.frustum
                    }

                let attributes = attributes state f

                DomNode.Scene(AttributeMap.union node.Attributes attributes, node.Scene, getState).WithAttributesFrom node
            | _ ->
                failwith "[UI] cannot add camera controllers to non-scene node"
                
 

    let view (state : MCameraControllerState) =
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        div [attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [
  
            controlledControl state id 
                (Mod.constant frustum)
                (AttributeMap.empty)                
                (
                    Sg.box' C4b.Green (Box3d(-V3d.III, V3d.III))
                        |> Sg.noEvents
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                        }
                )
        ]


    let threads (state : CameraControllerState) = ThreadPool.empty



    let start () =
        App.start {
            unpersist = Unpersist.instance
            view = view
            threads = threads
            update = update
            initial = initial
        }

