namespace Aardvark.UI.Primitives.CameraControllers

open System

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.Service
open Aardvark.UI.Primitives.CameraControllers.Model



module FreeFlyController =
    open Aardvark.Base.Incremental.Operators    

    type Message = 
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Wheel of V2d
        | Move of V2i
        | StepTime
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Blur

    let initial =
        {
            view = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
                                    
            orbitCenter = None
            stash = None
            sensitivity = 1.0
            panFactor  = 0.01
            zoomFactor = 0.01
            rotationFactor = 0.01            

            pendingMove = V3d.Zero

            lastTime = None
            moveVec = V3i.Zero
            dragStart = V2i.Zero
            movePos = V2i.Zero
            look = false; zoom = false; pan = false                    
            forward = None; backward = None; left = None; right = None
            isWheel = false;
            moveSpeed = 0.0
            scrollSensitivity = 0.8
            scrolling = false
        }

    let initial' (dist:float) =
        { initial with view = CameraView.lookAt (dist * V3d.III) V3d.Zero V3d.OOI }

    let sw = System.Diagnostics.Stopwatch()
    do sw.Start()

    let withTime (model : CameraControllerState) =
        { model with lastTime = Some sw.Elapsed.TotalSeconds; view = model.view.WithLocation(model.view.Location) }

    let exp x =
        Math.Pow(Math.E, x)
    
    let updateLookAround (model : CameraControllerState) =
        let cam = model.view
        let pos = model.movePos
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

    let computeMove (dir : V3d) (dt : TimeSpan) (model : CameraControllerState) =
        { model with pendingMove = dir * dt.TotalSeconds * model.moveSpeed }

    let update (model : CameraControllerState) (message : Message) =
        match message with
            | Blur ->
                { model with 
                    lastTime = None
                    moveVec = V3i.Zero
                    dragStart = V2i.Zero
                    look = false; zoom = false; pan = false                    
                    forward = None; backward = None; left = None; right = None
                }
            | StepTime ->
              let now = sw.Elapsed.TotalSeconds
              let cam = model.view                

              let cam = 
                match model.lastTime with
                  | Some last ->
                    let dt = now - last

                    let dir = 
                        cam.Forward * model.pendingMove.Z +
                        cam.Right * model.pendingMove.X +
                        cam.Sky *  model.pendingMove.Y

                    if model.moveVec = V3i.Zero then
                        //printfn "useless time %A" now
                        cam
                    else
                        cam.WithLocation(model.view.Location + dir * (exp model.sensitivity) * dt)
                  | None -> 
                      cam

              let model = if model.isWheel then { model with moveVec = V3i.Zero; isWheel = false} else model        
              
              let model = updateLookAround { model with view = cam }

              { model with lastTime = Some now; pendingMove = V3d.Zero }

            | KeyDown Keys.W ->
                match model.forward with
                    | None -> { model with forward = Some sw.Elapsed; }
                    | Some _ -> model

            | KeyUp Keys.W ->
                match model.forward with
                    | None -> model
                    | Some dt -> { model with forward = None } |> computeMove V3d.OOI dt

            | KeyDown Keys.S ->
                match model.backward with
                    | None -> { model with backward = Some sw.Elapsed }
                    | Some _ -> model

            | KeyUp Keys.S ->
                match model.backward with
                    | None -> model
                    | Some dt -> { model with backward = None } |> computeMove -V3d.OOI dt

            | KeyDown Keys.A ->
                match model.left with
                    | None -> { model with left = Some sw.Elapsed; }
                    | Some dt -> model

            | KeyUp Keys.A ->
                match model.left with
                    | Some dt -> { model with left = None; } |> computeMove -V3d.IOO dt
                    | None -> model

            | KeyDown Keys.D ->
                match model.right with
                    | None -> { model with right = Some sw.Elapsed; }
                    | Some dt -> model

            | KeyUp Keys.D ->
                match model.right with
                    | Some dt -> { model with right = None; } |> computeMove V3d.IOO dt
                    | None -> model

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
                { model with movePos = pos; } |> updateLookAround

            | Wheel delta ->
                withTime { model with isWheel = true; moveVec = model.moveVec + V3i.OOI * int delta.Y * 10 }


    let update' = flip update

    let extractAttributes (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>)  =
        AttributeMap.ofListCond [
            always (onBlur (fun _ -> f Blur))
            always (onMouseDown (fun b p -> f (Down(b,p))))
            onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseUp (fun b p -> f (Up b)))
            always (onKeyDown (KeyDown >> f))
            always (onKeyUp (KeyUp >> f))
            always (onWheel(fun x -> f (Wheel x)))
            onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
        ] |> AttributeMap.toAMap



    let controlledControlWithClientValues (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (config : RenderControlConfig) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes =
            AttributeMap.ofListCond [
                always (onBlur (fun _ -> f Blur))
                always (onMouseDown (fun b p -> f (Down(b,p))))
                onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseUp (fun b p -> f (Up b)))
                always (onKeyDown (KeyDown >> f))
                always (onKeyUp (KeyUp >> f))           
                always (onWheel(fun x -> f (Wheel x)))
                onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
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
                        onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseUp (fun b p -> f (Up b)))
                        always (onKeyDown (KeyDown >> f))
                        always (onKeyUp (KeyUp >> f))
                        always (onWheel(fun x -> f (Wheel x)))
                        onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
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
