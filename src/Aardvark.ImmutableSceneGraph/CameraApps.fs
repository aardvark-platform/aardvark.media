namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Scratch.DomainTypes

open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish
open Primitives

module OrbitCameraApp = 

    open Aardvark.Base
    open Aardvark.Base.Rendering

    open Scratch.DomainTypes2
    open CameraTest
    open Primitives
    open Aardvark.ImmutableSceneGraph.Scene

    open Input

    type Action =                 
        | MouseDelta of V2d
        | PickPoint  of V3d
        | Animate    of DateTime
        | TimeStep   of float    

    let center' (c) = 
        match c with
            | Some s -> s
            | None -> V3d.Zero       
            
    let view (m : MModel) =        
        [Sphere (Sphere3d(V3d.OOO, 1.0)) 
            |> Scene.render [ on (Mouse.down' MouseButtons.Left) PickPoint ]
         Sphere (Sphere3d(V3d.OOO, 0.02)) 
            |> Scene.render [] 
            |> colored' (Mod.constant C4b.Red)
            |> Scene.transform' (m.mcenter |> Mod.map (fun a -> Trafo3d.Translation (center' a)))
        ]
        |> Scene.group            
        //|> Scene.viewTrafo (m.mcamera |> Mod.map CameraView.viewTrafo)
        //|> Scene.projTrafo (m.mfrustum |> Mod.map Frustum.projTrafo)

    let viewCenter (m : MModel) =
         Sphere (Sphere3d(V3d.OOO, 0.02)) 
                |> Scene.render [] 
                |> colored' (Mod.constant C4b.Red)
                |> Scene.transform' (m.mcenter |> Mod.map (fun a -> Trafo3d.Translation (center' a)))
    
    let orientationFctr = 1.0
    let panningFctr = 1.0
    let zoomingFctr = 8.0

    let clampedLocation (p:V3d) (s:V3d) (c:V3d) =
        let newLoc = p + s;
        let dist = (c - newLoc).Length
        let stepDist = (newLoc-p).Length
        
        if stepDist < dist then newLoc else p

    let (|Orient|Panning|Zoom|NoOp|) (look, pan, zoom, center) =
        match look,pan,zoom,center with
            | Some _, None,   None,   Some c -> Orient  c
            | None,   Some _, None,   Some c -> Panning c
            | None,   None,   Some _, Some c -> Zoom    c
            | _,_,_,_                        -> NoOp

    let update pickEx e (m : Model) msg = 
        match msg with            
            | MouseDelta d -> 
                match (m.lookingAround, m.panning, m.zooming, m.center) with
                    | Orient c ->
                        let delta = Constant.PiTimesTwo * d * orientationFctr
                        let t = M44d.Rotation (m.camera.Right, -delta.Y) * M44d.Rotation (m.camera.Sky, -delta.X)

                        let newLocation = t.TransformDir (m.camera.Location)
                        let tempcam = m.camera.WithLocation newLocation
                        let newForward = c - newLocation |> Vec.normalize
                        let tempcam = tempcam.WithForward newForward
                               
                        { m with camera = CameraView.lookAt tempcam.Location c tempcam.Up}         
                    | Panning c ->
                        let step = (m.camera.Down * float d.Y + m.camera.Right * float d.X) * panningFctr
                        { m with camera = m.camera.WithLocation (m.camera.Location + step ); center = Some (c + step)}
                    | Zoom c ->
                        let step = (m.camera.Forward * float +d.Y) * -zoomingFctr
                        let newLoc = clampedLocation m.camera.Location step c
                        { m with camera = m.camera.WithLocation newLoc}
                    | NoOp -> m 
            | TimeStep dt -> 
                let dir = m.forward.X * m.camera.Right + m.forward.Y * m.camera.Forward
                let speed = dt * 0.01                
                { m with camera = m.camera.WithLocation(m.camera.Location + dir * speed )}
            | PickPoint c when m.picking.IsSome && pickEx -> 
                let newForward = c - m.camera.Location |> Vec.normalize
                let tempCam = m.camera.WithForward newForward
                { m with center = Some c; camera = CameraView.lookAt tempCam.Location c tempCam.Up }
            | _ -> m

    let subscriptions (time : IMod<DateTime>) (m : Model) =
        Many [                            
                
            Input.moveDelta MouseDelta                
            //Sub.time(TimeSpan.FromMilliseconds 30.0) ( fun a -> TimeStep 30.0)
            //Sub.ofMod time (fun _ ms -> [TimeStep ms])        
        ]
    
    let initial = { 
        camera = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
        //frustum = Frustum.perspective 60.0 0.01 10.0 (1024.0/768.0); 
        _id = null
        lookingAround = None
        panning = None
        zooming = None
        picking = None
        forward = V2d.OO
        forwardSpeed = 0.0
        center = Some V3d.Zero
        navigationMode = NavigationMode.FreeFly
        }

    let ofPickMsg _ m = []

    let app time : App<Model,MModel,Action,ISg<Action>> =
        {
            initial = initial
            update = update true
            view = view
            ofPickMsg = ofPickMsg 
            subscriptions = subscriptions time
        }

module CameraUtilities =

    open Scratch.DomainTypes2
    open CameraTest
    open Input
    
    type MouseAction =
        | DragStart       of PixelPosition
        | DragStop        of PixelPosition
        | PanStart        of PixelPosition
        | PanStop         of PixelPosition
        | ZoomStart       of PixelPosition
        | ZoomStop        of PixelPosition

    let mouseSubscriptions (m : Model) =
        [
            Input.toggleMouse Mouse.left   DragStart  DragStop
            Input.toggleMouse Mouse.middle PanStart   PanStop
            Input.toggleMouse Mouse.right  ZoomStart  ZoomStop
        ]

    let update (v : Model) (action : MouseAction) =
        match action with
            | DragStart p -> { v with lookingAround = Some p }
            | DragStop _  -> { v with lookingAround = None }
            | PanStart p  -> { v with panning = Some p }
            | PanStop _   -> { v with panning = None }
            | ZoomStart p -> { v with zooming = Some p }
            | ZoomStop _  -> { v with zooming = None }

module FreeFlyCameraApp = 

    open Aardvark.Base
    open Aardvark.Base.Rendering
    open Scratch.DomainTypes2
    open CameraTest
    open Primitives
    open Aardvark.ImmutableSceneGraph.Scene
    open Input

    type Action = 
        | MouseDelta of V2d        
        | AddMove    of V2d
        | RemoveMove of V2d
        | Animate    of DateTime
        | PickPoint  of V3d        
        | TimeStep   of float
        | MouseAction of CameraUtilities.MouseAction

    let point = Mod.init V3d.Zero

    let view (m : MModel) =
        [Sphere (Sphere3d(V3d.OOO, 1.0))
            |> Scene.render []
         Sphere (Sphere3d(V3d.OOO, 0.05)) 
            |> Scene.render []             
            |> colored' (Mod.constant C4b.Red)
            |> Scene.transform' (point |> Mod.map (fun a -> Trafo3d.Translation (a)));]      
        |> Scene.group
        |> Scene.viewTrafo (m.mcamera |> Mod.map CameraView.viewTrafo)
        //|> Scene.projTrafo (m.mfrustum |> Mod.map Frustum.projTrafo)

    let forward = V2d.OI
    let backward = -V2d.OI
    let left = -V2d.IO
    let right = V2d.IO
    let clampDir (v : V2d) = V2d(clamp -1.0 1.0 v.X, clamp -1.0 1.0 v.Y)
    let orientationFctr = 1.0
    let panningFctr = 1.0
    let zoomingFctr = 1.0

    let update e (m : Model) msg = 
        match msg with            
            | MouseDelta d -> 
                match (m.lookingAround, m.panning, m.zooming) with
                    | Some _, None, None -> //orient
                        let delta = Constant.PiTimesTwo * d * orientationFctr
                        let t = M44d.Rotation (m.camera.Right, -delta.Y) * M44d.Rotation (m.camera.Sky, -delta.X)
                        let forward = t.TransformDir m.camera.Forward |> Vec.normalize
                        { m with camera = m.camera.WithForward forward }
                    | None, Some _, None -> //pan
                        let step = (m.camera.Down * float d.Y + m.camera.Right * float d.X) * panningFctr
                        { m with camera = m.camera.WithLocation (m.camera.Location + step )}
                    | None, None, Some _ -> //zoom
                        let step = (m.camera.Forward * float -d.Y) * zoomingFctr                        
                        { m with camera = m.camera.WithLocation (m.camera.Location + step)}
                    | _,_,_ -> m
            | AddMove d    -> { m with forward = clampDir <| m.forward + d }
            | RemoveMove d -> { m with forward = clampDir <| m.forward - d }
            | TimeStep dt -> 
                let dir = m.forward.X * m.camera.Right + m.forward.Y * m.camera.Forward
                let speed = dt * 0.01
                { m with camera = m.camera.WithLocation(m.camera.Location + dir * speed )}
            | MouseAction a -> CameraUtilities.update m a
            | _ -> m          

    let ofPickMsg _ m = []

    let subscriptions (time : IMod<DateTime>) (m : Model) =
        Many [
            
            Input.toggleKey Keys.W (fun _ -> AddMove forward)   (fun _ -> RemoveMove forward)
            Input.toggleKey Keys.S (fun _ -> AddMove backward)  (fun _ -> RemoveMove backward)
            Input.toggleKey Keys.A (fun _ -> AddMove left)      (fun _ -> RemoveMove left)
            Input.toggleKey Keys.D (fun _ -> AddMove right)     (fun _ -> RemoveMove right)                                   
            
            Input.moveDelta MouseDelta     

            //Input.key Direction.Down Keys.W (fun b a -> TimeStep 20.0)

            //Sub.time(TimeSpan.FromMilliseconds 25.0) ( fun a -> TimeStep 25.0)
            //Sub.ofMod time (fun t ms -> [TimeStep ms])
            Sub.Many <| CameraUtilities.mouseSubscriptions m |> Sub.map MouseAction
        ]


    let initial = { 
        camera = CameraView.lookAt (V3d.III * 3.0) V3d.OOO V3d.OOI
        ///frustum = Frustum.perspective 60.0 0.01 10.0 (1024.0/768.0); 
        _id = null
        lookingAround = None
        panning = None
        zooming = None
        picking = None
        forward = V2d.OO
        forwardSpeed = 0.0 
        center = Some V3d.Zero
        navigationMode = NavigationMode.FreeFly
        }

    let groundIt (m : Model) =
        { m with 
            panning = None
            zooming = None
            picking = None
            forward = V2d.OO
            lookingAround = None
            forwardSpeed = 0.0 }

    let app time : App<Model,MModel,Action,ISg<Action>> =
        {
            initial = initial
            update = update
            view = view
            ofPickMsg = ofPickMsg 
            subscriptions = subscriptions time
        }


