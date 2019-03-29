namespace Orbit

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Orbit

module OrbitController = 
    let private sw = System.Diagnostics.Stopwatch.StartNew()

    let initial = OrbitState.create V3d.Zero Constant.PiQuarter Constant.PiQuarter 4.0

    let rec update (model : OrbitState) (msg : OrbitMessage) =
        match msg with

        | SetTargetPhi tphi ->
            OrbitState.withView { model with targetPhi = tphi }
        
        | SetTargetTheta ttheta ->
            OrbitState.withView { model with targetPhi = clamp model.thetaRange.Min model.thetaRange.Max ttheta }
        
        | SetTargetRadius tr ->
            OrbitState.withView { model with targetRadius = clamp model.radiusRange.Min model.radiusRange.Max tr }

        | SetTargetCenter tc ->
            OrbitState.withView { model with targetCenter = tc }
        

        | MouseDown p ->
            { model with dragStart = Some p; lastRender = None }

        | MouseUp p ->
            { model with dragStart = None; lastRender = None }

        | Wheel delta ->
            OrbitState.withView { model with targetRadius = clamp model.radiusRange.Min model.radiusRange.Max (model.targetRadius + delta.Y * model.zoomSensitivity) }

        | MouseMove p ->
            match model.dragStart with
            | Some(start) ->
                let delta = p - start
                let dphi = float delta.X * -0.01 * model.moveSensitivity
                let dtheta = float delta.Y * 0.01 * model.moveSensitivity
            
           
                OrbitState.withView 
                    { model with
                        dragStart = Some p 
                        targetPhi = model.targetPhi + dphi
                        targetTheta = clamp model.thetaRange.Min model.thetaRange.Max (model.targetTheta + dtheta)
                    }
            | None ->
                model
        | Rendered ->
            let dphi = model.targetPhi - model.phi
            let dtheta = model.targetTheta - model.theta
            let dradius = model.targetRadius - model.radius
            let dcenter = model.targetCenter - model.center

            let now = sw.MicroTime
            let dt =
                match model.lastRender with
                | Some last -> (now - last)
                | None -> MicroTime.Zero
            
            let delta = model.speed * dt.TotalSeconds / 0.05
            let part = if dt.TotalSeconds > 0.0 then clamp 0.0 1.0 delta else 0.0
            let model = { model with lastRender = Some now }

            let model = 
                if abs dphi > 0.0 then
                    if Fun.IsTiny(dphi, 1E-4) then
                        Log.warn "phi done"
                        OrbitState.withView { model with phi = model.targetPhi }
                    else
                        OrbitState.withView { model with phi = model.phi + part * dphi }
                else
                    model

            let model = 
                if abs dtheta > 0.0 then
                    if Fun.IsTiny(dtheta, 1E-4) then
                        Log.warn "theta done"
                        OrbitState.withView { model with theta = model.targetTheta }
                    else
                        OrbitState.withView { model with theta  = model.theta + part * dtheta }
                else
                    model

            let model = 
                if abs dradius > 0.0 then
                    if Fun.IsTiny(dradius, 1E-4) then
                        Log.warn "radius done"
                        OrbitState.withView { model with radius = model.targetRadius }
                    else
                        OrbitState.withView { model with radius  = model.radius + part * dradius }
                else
                    model

            let model = 
                if dcenter.Length > 0.0 then
                    if V3d.ApproxEqual(V3d.Zero, dcenter, 1E-4) then
                        Log.warn "center done"
                        OrbitState.withView { model with center = model.targetCenter }
                    else
                        OrbitState.withView { model with center  = model.center + part * dcenter }
                else
                    model

            model

    let threads (model : OrbitState) = 
        ThreadPool.empty


    let private attributes (model : MOrbitState) (f : OrbitMessage -> 'msg) =
        let down = model.dragStart |> Mod.map Option.isSome
        AttributeMap.ofListCond [
            always <| onCapturedPointerDown None (fun k b p -> MouseDown p |> f)
            always <| onCapturedPointerUp None (fun k b p -> MouseUp p |> f)
            always <| onEvent "onRendered" [] (fun _ -> Rendered |> f)
            always <| onWheel (fun delta -> Wheel delta |> f)
            onlyWhen down <| onCapturedPointerMove None (fun k p -> MouseMove p |> f)
        ]
        

    let controlledControl (model : MOrbitState) (f : OrbitMessage -> 'msg) (frustum : IMod<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        let cam = Mod.map2 Camera.create model.view  frustum
        let controllerAtts = attributes model f
        DomNode.RenderControl(AttributeMap.union att controllerAtts, cam, sg, RenderControlConfig.standard, None)

    let withControls (state : MOrbitState) (f : OrbitMessage -> 'msg) (frustum : IMod<Frustum>) (node : DomNode<'msg>) =
        let cam = Mod.map2 Camera.create state.view frustum 
        match node with
            | :? SceneNode<'msg> as node ->
                let getState(c : Aardvark.Service.ClientInfo) =
                    let cam = cam.GetValue(c.token)
                    let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

                    {
                        Aardvark.Service.ClientState.viewTrafo = CameraView.viewTrafo cam.cameraView
                        Aardvark.Service.ClientState.projTrafo = Frustum.projTrafo cam.frustum
                    }

                let attributes = attributes state f

                DomNode.Scene(AttributeMap.union node.Attributes attributes, node.Scene, getState).WithAttributesFrom node
            | _ ->
                failwith "[UI] cannot add camera controllers to non-scene node"
                

    let simpleView (model : MOrbitState) =
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        let mainAtts =
            AttributeMap.ofList [
                style "width: 100%; height: 100%"
            ]

        controlledControl model id (Mod.constant frustum) mainAtts (
            Sg.box (Mod.constant C4b.Green) (Mod.constant Box3d.Unit)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
        )
        
    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = initial
            update = update 
            view = simpleView
        }
