namespace Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Service

open Aardvark.UI.Primitives



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OrbitState =

    let withView (s : OrbitState) =
        let basis = M44d.FromBasis(s.right, Vec.cross s.sky s.right, s.sky, V3d.Zero)
        let onSphere = V2d(s.phi, s.theta).CartesianFromSpherical()  * s._radius
        let position = basis.TransformPos(onSphere) + s.center
        let view = CameraView.lookAt position s.center s.sky
        let withPan = view.WithLocation(view.Location + s.pan.X * view.Right + s.pan.Y * view.Up )

        { s with _view = withPan }

    let create' (right : V3d) (sky : V3d) (center : V3d) (phi : float) (theta : float) (r : float) =
        let thetaRange = Range1d(-Constant.PiHalf + 0.0001, Constant.PiHalf - 0.0001)
        let radiusRange = Range1d(0.1, 40.0)

        let r = clamp radiusRange.Min radiusRange.Max r
        let theta = clamp thetaRange.Min thetaRange.Max theta

        withView {
            sky     = sky
            right   = right
            center  = center
            phi     = phi
            theta   = theta
            _radius  = r

            targetPhi = phi
            targetTheta = theta
            targetRadius = r
            targetCenter = center

            pan = V2d.Zero
            targetPan = V2d.Zero

            dragStart = None
            panning   = false
            lastRender = None
            _view = Unchecked.defaultof<_>

            radiusRange = radiusRange
            thetaRange = thetaRange
            moveSensitivity = 1.0
            zoomSensitivity = 1.0
            speed = 1.0

            config = { isPan = fun b -> b = MouseButtons.Middle }
        }

    let create (center : V3d) (phi : float) (theta : float) (r : float) =
        create' V3d.OIO V3d.OOI center phi theta r



type OrbitMessage =
    | MouseDown of button : MouseButtons * pos : V2i
    | MouseUp of button : MouseButtons * pos : V2i
    | MouseMove of V2i
    | Wheel of V2d


    | Rendered
    | SetTargetCenter of V3d
    | SetTargetPhi of float
    | SetTargetTheta of float
    | SetTargetRadius of float



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
            OrbitState.withView { model with targetCenter = tc; targetPan = V2d.Zero }
        

        | MouseDown(button, p) ->
            { model with dragStart = Some p; lastRender = None; panning = model.config.isPan button }

        | MouseUp(button, p) ->
            let model = 
                if model.panning then 
                    { model with  panning = false}
                else 
                    model
            { model with dragStart = None; lastRender = None }

        | Wheel delta ->
            OrbitState.withView { model with targetRadius = clamp model.radiusRange.Min model.radiusRange.Max (model.targetRadius - delta.Y * model.zoomSensitivity) }

        | MouseMove p ->
            match model.dragStart with
            | Some(start) ->
                let delta = p - start
                if model.panning then 
                    let dx = float delta.X * -0.01 * model.moveSensitivity
                    let dy = float delta.Y * 0.01 * model.moveSensitivity
                    OrbitState.withView  
                        { model with 
                            targetPan = V2d(dx,dy) + model.targetPan 
                            dragStart = Some p
                        } 
                else
                    let dphi = float delta.X * -0.01 * model.moveSensitivity
                    let dtheta = float delta.Y * 0.01 * model.moveSensitivity
                    // rotating
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
            let dradius = model.targetRadius - model._radius
            let dcenter = model.targetCenter - model.center
            let dpan = model.targetPan - model.pan

            let now = sw.MicroTime
            let dt =
                match model.lastRender with
                | Some last -> (now - last)
                | None -> MicroTime.Zero
            
            let delta = model.speed * dt.TotalSeconds / 0.05
            let part = if dt.TotalSeconds > 0.0 then clamp 0.0 1.0 delta else 0.0
            let model = { model with lastRender = Some now }

            let model = 
                if abs dpan.Length > 0.0 then
                    if Fun.IsTiny(dpan.Length, 1E-4) then
                        OrbitState.withView { model with pan = model.targetPan }
                    else
                        OrbitState.withView { model with pan = model.pan + part * dpan }
                else
                    model

            let model = 
                if abs dphi > 0.0 then
                    if Fun.IsTiny(dphi, 1E-4) then
                        OrbitState.withView { model with phi = model.targetPhi }
                    else
                        OrbitState.withView { model with phi = model.phi + part * dphi }
                else
                    model

            let model = 
                if abs dtheta > 0.0 then
                    if Fun.IsTiny(dtheta, 1E-4) then
                        OrbitState.withView { model with theta = model.targetTheta }
                    else
                        OrbitState.withView { model with theta  = model.theta + part * dtheta }
                else
                    model

            let model = 
                if abs dradius > 0.0 then
                    if Fun.IsTiny(dradius, 1E-4) then
                        OrbitState.withView { model with _radius = model.targetRadius }
                    else
                        OrbitState.withView { model with _radius  = model._radius + part * dradius }
                else
                    model

            let model = 
                if dcenter.Length > 0.0 then
                    if Fun.ApproximateEquals(V3d.Zero, dcenter, 1E-4) then
                        OrbitState.withView { model with center = model.targetCenter }
                    else
                        OrbitState.withView { model with center  = model.center + part * dcenter }
                else
                    model

            model

    let threads (model : OrbitState) = 
        ThreadPool.empty


    let attributes (model : AdaptiveOrbitState) (f : OrbitMessage -> 'msg) =
        let down = model.dragStart |> AVal.map Option.isSome
        AttributeMap.ofListCond [
            always <| onCapturedPointerDown None (fun k b p -> MouseDown(b, p) |> f)
            always <| onCapturedPointerUp None (fun k b p -> MouseUp(b, p) |> f)
            always <| onEvent "onRendered" [] (fun _ -> Rendered |> f)
            always <| onWheel (fun delta -> Wheel delta |> f)
            onlyWhen down <| onCapturedPointerMove None (fun k p -> MouseMove p |> f)
        ]

    let extractAttributes (model : AdaptiveOrbitState) (f : OrbitMessage -> 'msg) =
        attributes model f |> AttributeMap.toAMap
        
    let controlledControlWithClientValues (state : AdaptiveOrbitState) (f : OrbitMessage -> 'msg) (frustum : aval<Frustum>) (att : AttributeMap<'msg>) (config : RenderControlConfig) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        let attributes = AttributeMap.union att (attributes state f)
        let cam = AVal.map2 Camera.create state._view frustum
        Incremental.renderControlWithClientValues' cam attributes config sg

    let controlledControl (model : AdaptiveOrbitState) (f : OrbitMessage -> 'msg) (frustum : aval<Frustum>) (att : AttributeMap<'msg>) (sg : ISg<'msg>) =
        let cam = AVal.map2 Camera.create model._view  frustum
        let controllerAtts = attributes model f
        DomNode.RenderControl(AttributeMap.union att controllerAtts, cam, sg, RenderControlConfig.standard, None)

    let withControls (state : AdaptiveOrbitState) (f : OrbitMessage -> 'msg) (frustum : aval<Frustum>) (node : DomNode<'msg>) =
        let cam = AVal.map2 Camera.create state._view frustum 
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
                

    let simpleView (model : AdaptiveOrbitState) =
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        let mainAtts =
            AttributeMap.ofList [
                style "width: 100%; height: 100%"
            ]

        controlledControl model id (AVal.constant frustum) mainAtts (
            Sg.box (AVal.constant C4b.Green) (AVal.constant Box3d.Unit)
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
