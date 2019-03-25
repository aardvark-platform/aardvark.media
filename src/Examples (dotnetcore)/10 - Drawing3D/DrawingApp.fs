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

module Shaders = 
    open FShade
    open Aardvark.Base.Rendering.Effects

    type ThickLineVertex = {
        [<Position>]                pos     : V4d
        [<Color>]                   c       : V4d
        [<Semantic("LineCoord")>]   lc      : V2d
        [<Semantic("Width")>]       w       : float
        [<SourceVertexIndex>]       i : int
    }

    [<GLSLIntrinsic("mix({0}, {1}, {2})")>]
    let Lerp (a : V4d) (b : V4d) (s : float) : V4d = failwith ""

    [<ReflectedDefinition>]
    let clipLine (plane : V4d) (p0 : ref<V4d>) (p1 : ref<V4d>) =
        let h0 = Vec.dot plane !p0
        let h1 = Vec.dot plane !p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0 && h1 > 0.0 then
            false
        elif h0 < 0.0 && h1 > 0.0 then
            let t = h0 / (h0 - h1)
            p1 := !p0 + t * (!p1 - !p0)
            true
        elif h1 < 0.0 && h0 > 0.0 then
            let t = h0 / (h0 - h1)
            p0 := !p0 + t * (!p1 - !p0)
            true
        else
            true

    [<ReflectedDefinition>]
    let clipLinePure (plane : V4d) (p0 : V4d) (p1 : V4d) =
        let h0 = Vec.dot plane p0
        let h1 = Vec.dot plane p1

        // h = h0 + (h1 - h0)*t
        // 0 = h0 + (h1 - h0)*t
        // (h0 - h1)*t = h0
        // t = h0 / (h0 - h1)
        if h0 > 0.0 && h1 > 0.0 then
            (false, p0, p1)
        elif h0 < 0.0 && h1 > 0.0 then
            let t = h0 / (h0 - h1)
            let p11 = p0 + t * (p1 - p0)
            (true, p0, p11)
        elif h1 < 0.0 && h0 > 0.0 then
            let t = h0 / (h0 - h1)
            let p01 = p0 + t * (p1 - p0)
            
            (true, p01, p1)
        else
            (true, p0, p1)

    let internal thickLine (line : Line<ThickLineVertex>) =
        triangle {
            let t = uniform.LineWidth
            let sizeF = V3d(float uniform.ViewportSize.X, float uniform.ViewportSize.Y, 1.0)

            let mutable pp0 = line.P0.pos
            let mutable pp1 = line.P1.pos

            let w = 1.0
            
            //let (a0, pp0, pp1) = clipLinePure (V4d( 1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a1, pp0, pp1) = clipLinePure (V4d(-1.0,  0.0,  0.0, -w)) pp0 pp1
            //let (a2, pp0, pp1) = clipLinePure (V4d( 0.0,  1.0,  0.0, -w)) pp0 pp1
            //let (a3, pp0, pp1) = clipLinePure (V4d( 0.0, -1.0,  0.0, -w)) pp0 pp1
            //let (a4, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0,  1.0, -1.0)) pp0 pp1
            //let (a5, pp0, pp1) = clipLinePure (V4d( 0.0,  0.0, -1.0, -1.0)) pp0 pp1
            
            let add = 2.0 * V2d(t,t) / sizeF.XY

            // x = w

            // p' = p / p.w
            // p' € [-1,1]
            // p' € [-1-add.X,1+add.X]


            // p.x - (1+add.X)*p.w = 0



            let a0 = clipLine (V4d( 1.0,  0.0,  0.0, -(1.0 + add.X))) &&pp0 &&pp1
            let a1 = clipLine (V4d(-1.0,  0.0,  0.0, -(1.0 + add.X))) &&pp0 &&pp1
            let a2 = clipLine (V4d( 0.0,  1.0,  0.0, -(1.0 + add.Y))) &&pp0 &&pp1
            let a3 = clipLine (V4d( 0.0, -1.0,  0.0, -(1.0 + add.Y))) &&pp0 &&pp1
            let a4 = clipLine (V4d( 0.0,  0.0,  1.0, -1.0)) &&pp0 &&pp1
            let a5 = clipLine (V4d( 0.0,  0.0, -1.0, -1.0)) &&pp0 &&pp1

            if a0 && a1 && a2 && a3 && a4 && a5 then
                let p0 = pp0.XYZ / pp0.W
                let p1 = pp1.XYZ / pp1.W

                let fwp = (p1.XYZ - p0.XYZ) * sizeF

                let fw = V3d(fwp.XY, 0.0) |> Vec.normalize
                let r = V3d(-fw.Y, fw.X, 0.0) / sizeF
                let d = fw / sizeF
                let p00 = p0 - r * t - d * t
                let p10 = p0 + r * t - d * t
                let p11 = p1 + r * t + d * t
                let p01 = p1 - r * t + d * t

                let rel = t / (Vec.length fwp)

                yield { line.P0 with i = 0; pos = V4d(p00, 1.0); lc = V2d(-1.0, -rel); w = rel }
                yield { line.P0 with i = 0; pos = V4d(p10, 1.0); lc = V2d( 1.0, -rel); w = rel }
                yield { line.P1 with i = 1; pos = V4d(p01, 1.0); lc = V2d(-1.0, 1.0 + rel); w = rel }
                yield { line.P1 with i = 1; pos = V4d(p11, 1.0); lc = V2d( 1.0, 1.0 + rel); w = rel }
        }

    type VertexDepth = { [<Color>] c : V4d; [<Depth>] d : float }

    [<GLSLIntrinsic("gl_DepthRange.diff")>]
    let depthDiff()  : float = failwith ""

    [<GLSLIntrinsic("gl_DepthRange.near")>]
    let depthNear()  : float = failwith ""

    [<GLSLIntrinsic("gl_DepthRange.far")>]
    let depthFar()  : float = failwith ""

    let depthOffsetFS (v : Vertex) =
        fragment {
            let offset : float = uniform?DepthOffset
            let d = (v.pos.Z - offset)  / v.pos.W
            return { c = v.c;  d = ((depthDiff() * d) + depthNear() + depthFar()) / 2.0  }
        }

type Action =
    | CameraMessage    of ArcBallController.Message
    | RenderingAction  of RenderingParametersModel.Action
    | Move     of V3d
    | AddPoint of V3d
    | KeyDown  of key : Keys
    | KeyUp    of key : Keys
    | SetDepthOffset of Numeric.Action
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
        | SetDepthOffset d,_ -> { model with depthOffset = Numeric.update model.depthOffset d }
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
                toEffect Shaders.thickLine
                toEffect Shaders.depthOffsetFS
                ]
            |> Sg.uniform "DepthOffset" (
                    model.depthOffset.value |> Mod.map (fun depthWorld -> 
                        let dLin = depthWorld / (100.0 - 0.1)
                        printfn "linear depth offset: %A" dLin
                        dLin
                    )
               )
            |> Sg.pass (RenderPass.after "lines" RenderPassOrder.Arbitrary RenderPass.main)
            //|> Sg.depthTest (Mod.constant DepthTestMode.None)                                         

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
                        br []
                        Numeric.view model.depthOffset |> UI.map SetDepthOffset
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
        depthOffset = { Numeric.init with value = 0.0; min = 0.0; max = 5.0; step = 0.01 }
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
