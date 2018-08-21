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
            targetZoom = 0.0

            freeFlyConfig = FreeFlyConfig.initial
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

    let attributes (state : MCameraControllerState) (f : Message -> 'msg) =
        AttributeMap.ofListCond [
            always (onBlur (fun _ -> f Blur))
            always (onMouseDown (fun b p -> f (Down(b,p))))
            always (onMouseUp (fun b p -> f (Up b)))
            always (onKeyDown (KeyDown >> f))
            always (onKeyUp (KeyUp >> f))
            always (onWheel(fun x -> f (Wheel x)))
            onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
        ]

    let extractAttributes (state : MCameraControllerState) (f : Message -> 'msg)  =
        attributes state f |> AttributeMap.toAMap

    let controlledControlWithClientValues (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes = AttributeMap.union att (attributes state f)
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