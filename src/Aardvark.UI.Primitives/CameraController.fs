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

open System
open System.Runtime.InteropServices
open System.Security
open Aardvark.Base

module MultimediaTimer =

    open System.Threading

    module private Windows = 
        type MultimediaTimerCallbackDel  = delegate of uint32 * uint32 * nativeint * uint32 * uint32 -> unit

        [<DllImport("winmm.dll", SetLastError = true); SuppressUnmanagedCodeSecurity>]
        extern uint32 timeSetEvent(uint32 msDelay, uint32 msResolution, nativeint callback, uint32& userCtx, uint32 eventType)
            
        [<DllImport("winmm.dll", SetLastError = true); SuppressUnmanagedCodeSecurity>]
        extern void timeKillEvent(uint32 timer)

        let start (interval : int) (callback : unit -> unit) =
            let del = MultimediaTimerCallbackDel(fun _ _ _ _ _ -> callback())
            let ptr = Marshal.PinDelegate del
            let mutable user = 0u
            let id = timeSetEvent(uint32 interval, 1u, ptr.Pointer, &user, 1u)
            if id = 0u then
                let err = Marshal.GetLastWin32Error()
                ptr.Dispose()
                failwithf "[Timer] could not start timer: %A" err
            else
                { new IDisposable with
                    member x.Dispose() =
                        timeKillEvent(id)
                        ptr.Dispose() 
                }
            
    module private Linux =
        
        [<DllImport("libc"); SuppressUnmanagedCodeSecurity>]
        extern void usleep(int usec)

        let start (interval : int) (callback : unit -> unit) =
            let run () =
                while true do
                    usleep(interval * 1000)
                    callback()
            let thread = new Thread(ThreadStart(run), IsBackground = true)
            thread.Start()

            { new IDisposable with
                member x.Dispose() = thread.Abort()
            }

    type Trigger(ms : int) =
        let ticksPerMillisecond = int64 TimeSpan.TicksPerMillisecond 
        let pulse = obj()

        let callback() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )
                
        let timer = 
            match Environment.OSVersion  with
                | Windows -> Windows.start ms callback
                | _ -> Linux.start ms callback
        
        member x.Wait() =
            lock pulse (fun () ->
                Monitor.Wait pulse |> ignore
            )  

        member x.Signal() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )  
            
        member x.Dispose() =
            timer.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type Timer(ms : int) =
        let s = Event<TimeSpan>()
        let ticksPerMillisecond = int64 TimeSpan.TicksPerMillisecond 
        let pulse = obj()
        let sw = System.Diagnostics.Stopwatch()
        do sw.Start()

        let callback() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
                s.Trigger(sw.Elapsed)
            )
                

        let timer = 
            match Environment.OSVersion  with
                | Windows -> Windows.start ms callback
                | _ -> Linux.start ms callback
        
        member x.Event = s.Publish

        member x.Wait() =
            lock pulse (fun () ->
                Monitor.Wait pulse |> ignore
            )  

        member x.Signal() =
            lock pulse (fun () ->
                Monitor.PulseAll pulse
            )  
            
        member x.Dispose() =
            timer.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


module Integrator = 

    let inline private dbl (one) = one + one

    let inline rungeKutta (f : ^t -> ^a -> ^da) (y0 : ^a) (h : ^t) : ^a =
        let twa : ^t = dbl LanguagePrimitives.GenericOne
        let half : ^t = LanguagePrimitives.GenericOne / twa
        let hHalf = h * half

        let k1 = h * f LanguagePrimitives.GenericZero y0
        let k2 = h * f hHalf (y0 + k1 * half)
        let k3 = h * f hHalf (y0 + k2 * half)
        let k4 = h * f h (y0 + k3)
        let sixth = LanguagePrimitives.GenericOne / (dbl twa + twa)
        y0 + (k1 + twa*k2 + twa*k3 + k4) * sixth

    let inline euler (f : ^t -> ^a -> ^da) (y0 : ^a) (h : ^t) : ^a=
        y0 + h * f LanguagePrimitives.GenericZero y0

    let rec integrate (maxDt : float) (f : 'm -> float -> 'm) (m0 : 'm) (dt : float) =
        if dt <= maxDt then
            f m0 dt
        else
            integrate maxDt f (f m0 maxDt) (dt - maxDt) 


module CameraController =
    open Aardvark.Base.Incremental.Operators    
    
    type CameraMotion = { dPos : V3d; dRot : V3d; dMoveSpeed : float; dPanSpeed : float; dPan : V2d; dDolly : float } with
        static member Zero = { dPos = V3d.Zero; dRot = V3d.Zero; dMoveSpeed = 0.0; dPanSpeed = 0.0; dPan = V2d.Zero; dDolly = 0.0 }

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
                dPanSpeed = l.dPanSpeed + r.dPanSpeed
                dPan = l.dPan + r.dPan
                dDolly = l.dDolly + r.dDolly
            }

        static member (*) (motion : CameraMotion, f : float) =
            { dPos = motion.dPos * f; dRot = motion.dRot * f; dMoveSpeed = motion.dMoveSpeed * f; dPanSpeed = motion.dPanSpeed * f; dPan = motion.dPan * f; dDolly = motion.dDolly * f}

        static member (*) (f : float, motion : CameraMotion) = motion * f
            

        static member Move(dPos : V3d) = { dPos = dPos; dRot = V3d.Zero; dMoveSpeed = 0.0; dPanSpeed = 0.0; dPan = V2d.Zero; dDolly = 0.0 }
        static member Rotate(dRot : V3d) = { dPos = V3d.Zero; dRot = dRot; dMoveSpeed = 0.0; dPanSpeed = 0.0; dPan = V2d.Zero; dDolly = 0.0 }


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
                panSpeed = state.panSpeed - motion.dPanSpeed
                targetPan = state.targetPan - motion.dPan
                targetDolly = state.targetDolly - motion.dDolly
            }






    type Message = CameraControllerMessage

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

            targetPhiTheta = V2d.Zero
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
    let exp x =
        Math.Pow(Math.E, x)
    
    let update (model : CameraControllerState) (message : Message) =
        match message with
            | Blur ->
                { model with 
                    lastTime = None
                    moveVec = V3i.Zero
                    dragStart = V2i.Zero
                    look = false; zoom = false; pan = false                    
                    forward = false; backward = false; left = false; right = false
                }
            | StepTime ->
              let now = sw.Elapsed.TotalSeconds

              

              let model = 
                match model.lastTime with
                  | Some last ->
                    let cam = model.view                
                    let dt = now - last

                    let moveVec = V3d model.moveVec

                    let dir = 
                        cam.Forward * moveVec.Z +
                        cam.Right * moveVec.X +
                        cam.Sky *moveVec.Y

                    if model.moveVec = V3i.Zero && not model.scrolling then
                        printfn "useless time %A" now

                    let moveSpeed = model.moveSpeed * pow 0.002 dt

                    let scroll = if model.scrolling then cam.Forward * moveSpeed * dt else V3d.OOO

                    let cam = cam.WithLocation(model.view.Location + dir * (exp model.sensitivity) * dt + scroll)
                    if abs model.moveSpeed > 1E-2 then
                            { model with
                                moveSpeed = moveSpeed
                                view = cam
                            }
                    else 
                        { model with scrolling = false; moveSpeed = 0.0; view = cam }
                  | None -> 
                      model

              //let model = if model.isWheel then { model with moveVec = V3i.Zero; isWheel = false} else model                  


              { model with lastTime = Some now; }

            | KeyDown Keys.W ->
                if not model.forward then
                    dummyChange { model with forward = true; moveVec = model.moveVec + V3i.OOI  }
                else
                    model

            | KeyUp Keys.W ->
                if model.forward then
                    dummyChange { model with forward = false; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyDown Keys.S ->
                if not model.backward then
                    withTime { model with backward = true; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyUp Keys.S ->
                if model.backward then
                    withTime { model with backward = false; moveVec = model.moveVec + V3i.OOI  }
                else
                    model
            | Wheel delta ->
                //withTime { model with isWheel = true; moveVec = model.moveVec + V3i.OOI * int delta.Y * 10 }
                let delta = model.scrollSensitivity * (delta.Y * 10.0)
                withTime { model with 
                                moveSpeed = model.moveSpeed + delta 
                                scrolling = true
                }
            | KeyDown Keys.A ->
                if not model.left then
                    withTime { model with left = true; moveVec = model.moveVec - V3i.IOO  }
                else
                    model

            | KeyUp Keys.A ->
                if model.left then
                    withTime { model with left = false; moveVec = model.moveVec + V3i.IOO  }
                else
                    model

            | KeyDown Keys.D ->
                if not model.right then
                    withTime { model with right = true; moveVec = model.moveVec + V3i.IOO}
                else
                    model

            | KeyUp Keys.D ->
                if model.right then
                    withTime { model with right = false; moveVec = model.moveVec - V3i.IOO }
                else
                    model

            | KeyDown _ | KeyUp _ ->
                model

            | Down(button,pos) ->
                let model = { model with dragStart = pos }
                match button with
                    | MouseButtons.Left -> { model with look = true }
                    | MouseButtons.Middle -> { model with pan = true }
                    | MouseButtons.Right -> { model with zoom = true }
                    | _ -> model

            | Up button ->
                match button with
                    | MouseButtons.Left -> { model with look = false }
                    | MouseButtons.Middle -> { model with pan = false }
                    | MouseButtons.Right -> { model with zoom = false }
                    | _ -> model            
            | Move pos  ->
                let cam = model.view
                let delta = pos - model.dragStart

                let cam =
                    if model.look then
                        let trafo =
                            M44d.Rotation(cam.Right, float delta.Y * -model.rotationFactor) *
                            M44d.Rotation(cam.Sky,   float delta.X * -model.rotationFactor)

                        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                        cam.WithForward newForward
                    else
                        cam

                let cam =
                    if model.zoom then
                        let step = -model.zoomFactor * (cam.Forward * float delta.Y) * (exp model.sensitivity)
                        cam.WithLocation(cam.Location + step)
                    else
                        cam

                let cam =
                    if model.pan then
                        let step = model.panFactor * (cam.Down * float delta.Y + cam.Right * float delta.X) * (exp model.sensitivity)
                        cam.WithLocation(cam.Location + step)
                    else
                        cam

                { model with view = cam; dragStart = pos }

    //let updateLookAround (model : CameraControllerState) =
    //    let cam = model.view
    //    let pos = model.movePos
    //    let delta = pos - model.dragStart

    //    let cam =
    //        if model.look then
    //            let trafo =
    //                M44d.Rotation(cam.Right, float delta.Y * -model.rotationFactor) *
    //                M44d.Rotation(cam.Sky,   float delta.X * -model.rotationFactor)

    //            let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
    //            cam.WithForward newForward
    //        else
    //            cam

    //    let cam =
    //        if model.zoom then
    //            let step = -model.zoomFactor * (cam.Forward * float delta.Y) * (exp model.sensitivity)
    //            cam.WithLocation(cam.Location + step)
    //        else
    //            cam

    //    let cam =
    //        if model.pan then
    //            let step = model.panFactor * (cam.Down * float delta.Y + cam.Right * float delta.X) * (exp model.sensitivity)
    //            cam.WithLocation(cam.Location + step)
    //        else
    //            cam

    //    { model with view = cam; dragStart = pos }

    let subSteps (maxDt : float) (dt : float) =
        if dt <= maxDt then [dt]
        else 
            let cnt = int (dt / maxDt)
            (dt % maxDt) :: (List.init cnt (fun _ -> maxDt))

    //let rec integrate (maxDt : float) (dt : float) (m : 'm) (acc : 'm -> float -> 'm) =
    //    Integrator.integrate

    let updateSmooth (model : CameraControllerState) (message : Message) =
        match message with
            | Blur ->
                { model with 
                    lastTime = None
                    moveVec = V3i.Zero
                    dragStart = V2i.Zero
                    look = false; zoom = false; pan = false                    
                    forward = false; backward = false; left = false; right = false
                }
            | StepTime ->
                let now = sw.Elapsed.TotalSeconds
              
                let clampAbs (maxAbs : float) (v : float) =
                    if abs v > maxAbs then
                        float (sign v) * maxAbs
                    else
                        v

                let move (state : CameraControllerState) =
                    if state.moveVec <> V3i.Zero then
                        {CameraMotion.Zero with
                            dPos = V3d state.moveVec * exp state.sensitivity
                        }
                    else
                        CameraMotion.Zero

                let pan (state : CameraControllerState) =
                    if state.targetPan.Length > 0.05 then
                        let tt = (0.01 + abs state.targetPan.X * exp (state.sensitivity * 3.0)) * float (sign state.targetPan.X)
                        let tu = (0.01 + abs state.targetPan.Y * exp (state.sensitivity * 3.0)) * float (sign state.targetPan.Y)
                        {CameraMotion.Zero with
                            dPan = V2d(tt,tu)
                        }
                    else
                        CameraMotion.Zero
                
                let dolly (state : CameraControllerState) =
                    if abs state.targetDolly > 0.05 then
                        let dd = (0.05 + abs state.targetDolly * exp (state.sensitivity * 3.25)) * float (sign state.targetDolly)
                        {CameraMotion.Zero with
                            dDolly = dd
                        }
                    else
                        CameraMotion.Zero

                let look (state : CameraControllerState) =
                    if state.targetPhiTheta <> V2d.Zero then
                    
                        let rr = (0.1 + abs state.targetPhiTheta.Y * 30.0) * float (sign (state.targetPhiTheta.Y))
                        let ru = (0.1 + abs state.targetPhiTheta.X * 30.0) * float (sign (state.targetPhiTheta.X))

                        {CameraMotion.Zero with
                            dRot = V3d(rr, ru, 0.0)
                        }
                    else
                        CameraMotion.Zero

                let model = 
                    match model.lastTime with
                        | Some last ->
                        let dt = now - last
                        let now = ()

                        let step = Integrator.rungeKutta (fun t s -> move s + look s + pan s + dolly s)

                        Integrator.integrate 0.0166666 step model dt

                        //integrate 0.0166 dt model (fun model dt ->
                        //    let cam = model.view
                        //    let dir = 
                        //        cam.Forward * float model.moveVec.Z +
                        //        cam.Right * float model.moveVec.X +
                        //        cam.Sky * float model.moveVec.Y

                        //    let cam = 
                        //        if model.moveVec = V3i.Zero then
                        //            //printfn "useless time %A" now
                        //            cam
                        //        else
                        //            cam.WithLocation(model.view.Location + dir * (exp model.sensitivity) * dt)

                        //    if model.targetPhiTheta <> V2d.Zero then
                        

                        //        let rr = clampAbs ((0.1 + abs model.targetPhiTheta.Y * 30.0) * dt) (model.targetPhiTheta.Y)
                        //        let ru = clampAbs ((0.1 + abs model.targetPhiTheta.X * 30.0) * dt) (model.targetPhiTheta.X)

                        //        let trafo =
                        //            M44d.Rotation(cam.Right, rr) *
                        //            M44d.Rotation(cam.Sky,   ru)

                        //        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                        //        let cam = cam.WithForward newForward

                        //        { model with view = cam; targetPhiTheta = model.targetPhiTheta - V2d(ru, rr) }
                        //    else
                        //        { model with view = cam }
                        //)

                        | None -> 
                            model
                     
                if model.moveVec = V3i.Zero && model.targetPhiTheta = V2d.Zero && (model.targetPan.Length <= 0.05) && (abs model.targetDolly <= 0.05) then
                    stopAnimation model
                else
                    { model with lastTime = Some now; }

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
                        targetDolly = model.targetDolly + (float delta.Y)// * 0.0175
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
                    
                //let trafo =
                //    M44d.Rotation(cam.Right, float delta.Y * -model.rotationFactor) *
                //    M44d.Rotation(cam.Sky,   float delta.X * -model.rotationFactor)

                let look model = 
                    if model.look then
                        let deltaAngle = V2d(float delta.X * -model.rotationFactor, float delta.Y * -model.rotationFactor)
                    
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
                                targetPan = model.targetPan + (V2d(delta.X,-delta.Y)) * 0.05
                                dragStart = pos
                            }
                    else 
                        model

                let dolly model =
                    if model.dolly then
                        startAnimation 
                            { model with
                                targetDolly = model.targetDolly + (float -delta.Y) * 0.0175
                                dragStart = pos
                            }
                    else 
                        model
                    
                { model with dragStart = pos }
                    |> look
                    |> pan
                    |> dolly


    let update' = flip update

    let extractAttributes (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>)  =
        AttributeMap.ofListCond [
            always (onBlur (fun _ -> f Blur))
            always (onMouseDown (fun b p -> f (Down(b,p))))
            onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseUp (fun b p -> f (Up b)))
            always (onKeyDown (KeyDown >> f))
            always (onKeyUp (KeyUp >> f))
            always (onWheel(fun x -> f (Wheel x)))
            onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseMove (Move >> f))
        ] |> AttributeMap.toAMap



    let controlledControlWithClientValues (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (config : RenderControlConfig) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes =
            AttributeMap.ofListCond [
                always (onBlur (fun _ -> f Blur))
                always (onMouseDown (fun b p -> f (Down(b,p))))
                onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseUp (fun b p -> f (Up b)))
                always (onKeyDown (KeyDown >> f))
                always (onKeyUp (KeyUp >> f))           
                always (onWheel(fun x -> f (Wheel x)))
                onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseMove (Move >> f))
            ]

        let attributes = AttributeMap.union att attributes


        let cam = Mod.map2 Camera.create state.view frustum 
        Incremental.renderControlWithClientValues' cam attributes config sg

    let controlledControl (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        controlledControlWithClientValues state f frustum att (RenderControlConfig.standard true) (constF sg)
    
    let controlledControl' (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        controlledControlWithClientValues state f frustum att (RenderControlConfig.standard true) (constF sg)

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

                let attributes =
                    AttributeMap.ofListCond [
                        always (onBlur (fun _ -> f Blur))
                        always (onMouseDown (fun b p -> f (Down(b,p))))
                        onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseUp (fun b p -> f (Up b)))
                        always (onKeyDown (KeyDown >> f))
                        always (onKeyUp (KeyUp >> f))
                        always (onWheel(fun x -> f (Wheel x)))
                        onlyWhen (state.look %|| state.pan %|| state.dolly) (onMouseMove (Move >> f))
                    ]

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

module ArcBallController =
    open Aardvark.Base.Incremental.Operators

    type Message = 
        | Down      of button : MouseButtons * pos : V2i
        | Up        of button : MouseButtons
        | Move      of V2i
        | StepTime
        | KeyDown   of key : Keys
        | KeyUp     of key : Keys
        | Wheel     of V2d
        | Blur
        | Pick      of V3d

    let initial =
        {
            view        = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
            dragStart   = V2i.Zero
            movePos     = V2i.Zero
            look        = false
            zoom        = false
            pan         = false
            dolly       = false
            forward     = false; backward = false; left = false; right = false; isWheel = false

            moveVec         = V3i.Zero
            lastTime        = None
            orbitCenter     = Some V3d.Zero
            stash           = None
            sensitivity     = 1.0
            zoomFactor      = 0.01
            panFactor       = 0.01
            rotationFactor  = 0.01

            moveSpeed = 0.0
            scrollSensitivity = 1.0
            scrolling = false
            targetPhiTheta = V2d.Zero
            animating = false
            targetPan = V2d.Zero 
            targetDolly = 0.0
            panSpeed = 0.0
        }

    let sw = Diagnostics.Stopwatch()
    do sw.Start()

    let withTime (model : CameraControllerState) =
        { model with lastTime = Some sw.Elapsed.TotalSeconds }
        

    let exp x =
        let v = Math.Pow(Math.E, x)        
        v
   
    let update (model : CameraControllerState) (message : Message) =
        match message with
            | Blur ->
                { initial with view = model.view; lastTime = None; orbitCenter = model.orbitCenter }
            | Pick p -> 
                let cam = model.view
                let newForward = p - cam.Location |> Vec.normalize
                let tempCam = cam.WithForward newForward
                                
                { model with orbitCenter = Some p; view = CameraView.lookAt cam.Location p cam.Up }
            | StepTime ->
              let now = sw.Elapsed.TotalSeconds
              let cam = model.view

              let cam, center = 
                match model.lastTime with
                  | Some last ->
                      let dt = now - last

                      let dir = 
                          cam.Forward * float model.moveVec.Z +
                          cam.Right * float model.moveVec.X +
                          cam.Sky * float model.moveVec.Y

                      if model.moveVec = V3i.Zero then
                          printfn "useless time %A" now

                      let step = dir * (exp model.sensitivity) * dt
                      let center = if model.isWheel then model.orbitCenter.Value else model.orbitCenter.Value + step
                      
                      cam.WithLocation(cam.Location + step), Some center

                  | None -> 
                      cam, model.orbitCenter

              let model = if model.isWheel then { model with moveVec = V3i.Zero; isWheel = false} else model                

              { model with lastTime = Some now; view = cam; orbitCenter = center }

            | KeyDown Keys.W ->                
                if not model.forward then
                    withTime { model with forward = true; moveVec = model.moveVec + V3i.OOI  }
                else
                    model

            | KeyUp Keys.W ->
                if model.forward then
                    withTime { model with forward = false; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyDown Keys.S ->
                if not model.backward then
                    withTime { model with backward = true; moveVec = model.moveVec - V3i.OOI  }
                else
                    model

            | KeyUp Keys.S ->
                if model.backward then
                    withTime { model with backward = false; moveVec = model.moveVec + V3i.OOI  }
                else
                    model

            | KeyDown Keys.A ->
                if not model.left then
                    withTime { model with left = true; moveVec = model.moveVec - V3i.IOO  }
                else
                    model

            | KeyUp Keys.A ->
                if model.left then
                    withTime { model with left = false; moveVec = model.moveVec + V3i.IOO }
                else
                    model

            | KeyDown Keys.D ->
                if not model.right then
                    withTime { model with right = true; moveVec = model.moveVec + V3i.IOO  }
                else
                    model

            | KeyUp Keys.D ->
                if model.right then
                    withTime { model with right = false; moveVec = model.moveVec - V3i.IOO}
                else
                    model

            | Wheel delta ->
                withTime { model with isWheel = true; moveVec = model.moveVec + V3i.OOI * int delta.Y * 10 }

            | KeyDown _ | KeyUp _ ->
                model


            | Down(button,pos) ->
                let model = { model with dragStart = pos }
                match button with
                    | MouseButtons.Left -> { model with look = true }
                    | MouseButtons.Middle -> { model with pan = true }
                    | MouseButtons.Right -> { model with zoom = true }
                    | _ -> model

            | Up button ->
                match button with
                    | MouseButtons.Left -> { model with look = false }
                    | MouseButtons.Middle -> { model with pan = false }
                    | MouseButtons.Right -> { model with zoom = false }
                    | _ -> model

            | Move pos  ->
                
                let cam = model.view
                let delta = pos - model.dragStart

                //orientation
                let cam =
                    if model.look && model.orbitCenter.IsSome then
                        let trafo = 
                            M44d.Translation (model.orbitCenter.Value) *
                            M44d.Rotation (cam.Right, float delta.Y * -0.01 ) * 
                            M44d.Rotation (cam.Up, float delta.X * -0.01 ) *
                            M44d.Translation (-model.orbitCenter.Value)
                     
                        let newLocation = trafo.TransformPos (cam.Location)

                        let newUp = trafo.TransformDir (cam.Up)
                        let newRight = trafo.TransformDir (cam.Right)

                        //let tempcam = cam.WithLocation newLocation
                        
                        // make cam with up vector

                        //tempcam.WithForward newForward
                        let newForward = model.orbitCenter.Value - newLocation |> Vec.normalize
                        CameraView(cam.Sky, newLocation, newForward, newUp, newRight)
                    else
                        cam

                // zoom and pan
                let cam =
                    if model.zoom then
                        let step = -model.zoomFactor * (exp model.sensitivity) * (cam.Forward * float delta.Y)
                        cam.WithLocation(cam.Location + step)
                    else
                        cam

                let cam, center =
                    if model.pan && model.orbitCenter.IsSome then
                        let step = model.panFactor * (exp model.sensitivity) * (cam.Down * float delta.Y + cam.Right * float delta.X)
                        let center = model.orbitCenter.Value + step
                        cam.WithLocation(cam.Location + step), Some center
                    else
                        cam, model.orbitCenter            

                { model with view = cam; dragStart = pos; orbitCenter = center }


    let extractAttributes (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>)  =
        AttributeMap.ofListCond [
            always (onBlur (fun _ -> f Blur))
            always (onMouseDown (fun b p -> f (Down(b,p))))
            always (onMouseUp (fun b p -> f (Up b)))
            always (onKeyDown (KeyDown >> f))
            always (onKeyUp (KeyUp >> f))
            always (onWheel(fun x -> f (Wheel x)))
            onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
        ] |> AttributeMap.toAMap

    let controlledControlWithClientValues (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes =
            AttributeMap.ofListCond [
                always (onBlur (fun _ -> f Blur))
                always (onMouseDown (fun b p -> f (Down(b,p))))
                always (onMouseUp (fun b p -> f (Up b)))
                always (onKeyDown (KeyDown >> f))
                always (onKeyUp (KeyUp >> f))
                always (onWheel(fun x -> f (Wheel x)))
                onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
//                onlyWhen (state.moveVec |> Mod.map (fun v -> v <> V3i.Zero)) (onRendered (fun s c t -> f StepTime))
            ]

        let attributes = AttributeMap.union att attributes

        let cam = Mod.map2 Camera.create state.view frustum 
        Incremental.renderControlWithClientValues cam attributes sg

    let controlledControl (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        controlledControlWithClientValues state f frustum att (constF sg)

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

    let threads (state : CameraControllerState) =
        let pool = ThreadPool.empty
       
        let rec time() =
            proclist {
                do! Proc.Sleep 10
                yield StepTime
                yield! time()
            }

        if state.moveVec <> V3i.Zero then
            ThreadPool.add "timer" (time()) pool

        else
            pool


    let start () =
        App.start {
            unpersist = Unpersist.instance
            view = view
            threads = threads
            update = update
            initial = initial
        }