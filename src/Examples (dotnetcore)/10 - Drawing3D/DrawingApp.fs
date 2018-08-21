module App

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.SceneGraph

open Aardvark.UI.Primitives
open Aardvark.UI

open DrawingModel
open RenderingParametersModel

type Action =
    | CameraMessage    of ArcBallController.Message
    | RenderingAction  of RenderingParametersModel.Action
    | Move     of V3d
    | AddPoint of V3d
    | KeyDown  of key : Keys
    | KeyUp    of key : Keys
    | Exit      

let update (model : SimpleDrawingModel) (act : Action) =
    match act, model.draw with
        | CameraMessage m, false -> 
            { model with camera = ArcBallController.update model.camera m }
        | RenderingAction a, _ ->
            { model with rendering = RenderingParameters.update model.rendering a }
        | KeyDown Keys.LeftCtrl, _ -> { model with draw = true }
        | KeyUp Keys.LeftCtrl, _ -> { model with draw = false; hoverPosition = None }
        | Move p, true -> { model with hoverPosition = Some (Trafo3d.Translation p) }
        | AddPoint p, true -> { model with points = model.points |> List.append [p] }
        | Exit, _ -> { model with hoverPosition = None }
        | _ -> model
            
            
let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

let computeScale (view : IMod<CameraView>)(p:V3d)(size:float) =        
    view 
    |> Mod.map (fun v -> 
        let distV = p - v.Location
        let distF = V3d.Dot(v.Forward, distV)
        distF * size / 800.0
      )

let mkISg color size trafo =         
    Sg.sphere 5 color size 
     |> Sg.shader {
         do! DefaultSurfaces.trafo
         do! DefaultSurfaces.vertexColor
         do! DefaultSurfaces.simpleLighting
     }
     |> Sg.noEvents
     |> Sg.trafo(trafo) 
        
let canvas =  
    let b = new Box3d( V3d(-2.0,-0.5,-2.0), V3d(2.0,0.5,2.0) )                                               
    Sg.box (Mod.constant Primitives.colorsBlue.[0]) (Mod.constant b)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.requirePicking
        |> Sg.noEvents 
            |> Sg.withEvents [
                Sg.onMouseMove (fun p -> Move p)
                Sg.onClick(fun p -> AddPoint p)
                Sg.onLeave (fun _ -> Exit)
            ]    

let edgeLines (close : bool)  (points : IMod<list<V3d>>) =        
    points 
     |> Mod.map (fun k -> 
        match k with
            | h::_ -> 
                let start = if close then k @ [h] else k
                start 
                 |> List.pairwise 
                 |> List.map (fun (a,b) -> new Line3d(a,b)) 
                 |> Array.ofList                                                        
            | _ -> [||]
        )

let frustum =
    Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

let scene3D (model : MSimpleDrawingModel) =
    let cam =
        model.camera.view 

    let lines = 
        edgeLines false model.points 
            |> Sg.lines (Mod.constant Primitives.colorsBlue.[2])
            |> Sg.noEvents
            |> Sg.uniform "LineWidth" (Mod.constant 5) 
            |> Sg.effect [
                toEffect DefaultSurfaces.trafo
                toEffect DefaultSurfaces.vertexColor
                toEffect DefaultSurfaces.thickLine
                ]
            |> Sg.pass (RenderPass.after "lines" RenderPassOrder.Arbitrary RenderPass.main)
            |> Sg.depthTest (Mod.constant DepthTestMode.None)                                         

    let spheres =
        model.points 
            |> Mod.map (fun ps -> 
                ps |> List.map (fun p -> 
                        mkISg (Mod.constant Primitives.colorsBlue.[3])
                              (computeScale model.camera.view p 5.0)
                              (Mod.constant (Trafo3d.Translation(p)))) 
                        |> Sg.ofList)                                
            |> Sg.dynamic                            
                                              
    let trafo = 
        model.hoverPosition 
            |> Mod.map (function o -> match o with 
                                        | Some t-> t
                                        | None -> Trafo3d.Scale(V3d.Zero))

    let brush = mkISg (Mod.constant C4b.Red) (Mod.constant 0.05) trafo
                                                            
    [canvas; brush; spheres; lines]
        |> Sg.ofList
        |> Sg.fillMode model.rendering.fillMode
        |> Sg.cullMode model.rendering.cullMode   

let view (model : MSimpleDrawingModel) =            
    require (Html.semui) (
        div [clazz "ui"; style "background: #1B1C1E"] [
            ArcBallController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                        onKeyDown KeyDown; onKeyUp KeyUp; attribute "style" "width:65%; height: 100%; float: left;"; attribute "data-samples" "8"
                    ]
                )
                (scene3D model)

            div [style "width:35%; height: 100%; float:right; background: #1B1C1E;"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingParameters.view model.rendering |> UI.map RenderingAction 
                    ]
                ]
            ]
    )

let initial =
    {
        camera        = { ArcBallController.initial with view = CameraView.lookAt (6.0 * V3d.OIO) V3d.Zero V3d.OOI}
        rendering     = RenderingParameters.initial
        hoverPosition = None
        draw = false
        points = []
    }

let app =
    {
        unpersist = Unpersist.instance
        threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }

let start () = App.start app
