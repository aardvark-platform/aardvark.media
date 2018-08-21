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

type CameraControllerMessage =     
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Wheel of V2d
        | Move of V2i
        | StepTime
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Blur

[<Obsolete("CameraController module is deprecated. Use much smoother FreeFlyController instead.")>]
module CameraController =
    open Aardvark.Base.Incremental.Operators    
    
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
            targetZoom = 0.0
            freeFlyConfig = FreeFlyConfig.initial
        }

    let initial' (dist:float) =
        { initial with view = CameraView.lookAt (dist * V3d.III) V3d.Zero V3d.OOI }

    let sw = System.Diagnostics.Stopwatch()
    do sw.Start()

    let withTime (model : CameraControllerState) =
        { model with lastTime = Some sw.Elapsed.TotalSeconds; view = model.view.WithLocation(model.view.Location) }

    let exp x = Math.Pow(Math.E, x)
    
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
            | Wheel delta ->
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


    let threads (state : CameraControllerState) =
        let pool = ThreadPool.empty

       
        let rec time() =
            proclist {
                do! Proc.Sleep 10
                //let! _ = Async.AwaitEvent m.Event
                yield StepTime
                yield! time()
            }

        if state.moveVec <> V3i.Zero || state.scrolling then
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

