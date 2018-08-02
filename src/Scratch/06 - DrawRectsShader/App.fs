module Inc.App

open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Operators

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model

type Message = 
    | Camera of CameraController.Message
    | MoveCursor of V3d


let update (model : Model) (msg : Message) =
    match msg with
        | MoveCursor c -> 
            printfn "cuirsor: %A" c
            model
        | Camera c -> { model with cameraState = CameraController.update model.cameraState c }
        | _ -> model

let dependencies = Html.semui @ [
    { name = "drawRects.css"; url = "drawRects.css"; kind = Stylesheet }
    { name = "drawRects.js";  url = "drawRects.js";  kind = Script     }
    { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
    { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
] 

module Geometry = 

    open System

    let indices =
        [|
            1;2;6; 1;6;5
            2;3;7; 2;7;6
            4;5;6; 4;6;7
            3;0;4; 3;4;7
            0;1;5; 0;5;4
            0;3;2; 0;2;1
        |]

    let unitBox =
        let box = Box3d.Unit

        let positions = 
            [|
                V3f(box.Min.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Max.Y, box.Max.Z)
                V3f(box.Min.X, box.Max.Y, box.Max.Z)
            |]

        let normals = 
            [| 
                V3f.IOO;
                V3f.OIO;
                V3f.OOI;

                -V3f.IOO;
                -V3f.OIO;
                -V3f.OOI;
            |]

        let texcoords =
            [|
                V2f.OO; V2f.IO; V2f.II;  V2f.OO; V2f.II; V2f.OI
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,

            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                    DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    DefaultSemantic.DiffuseColorCoordinates, indices |> Array.mapi (fun ti _ -> texcoords.[ti % 6]) :> Array
                ]

        )

    let createQuad (vertices : IMod<array<V2f>>) (colors : IMod<array<C4f>>) =
        let drawCall = 
            DrawCallInfo(
                FaceVertexCount = 4,
                InstanceCount = 1
            )

        let positions = 
            // strip: [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            vertices |> Mod.map (fun arr -> 
                [| V3f(arr.[0],0.0f); V3f(arr.[1],0.0f); V3f(arr.[3],0.0f); V3f(arr.[2],0.0f) |]
            )
    
        let colors = 
            colors |> Mod.map (fun arr -> 
                [| arr.[0]; arr.[1]; arr.[3]; arr.[2] |]
            )
        
        let texcoords =     
            [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            
        drawCall
            |> Sg.render IndexedGeometryMode.TriangleStrip 
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.vertexAttribute DefaultSemantic.Colors colors
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant texcoords)


    let box (colors : IMod<C4b[]>) (bounds : IMod<Box3d>) =
        let trafo = bounds |> Mod.map (fun box -> Trafo3d.Scale(box.Size) * Trafo3d.Translation(box.Min))
        SgPrimitives.Primitives.unitBox
            |> Sg.ofIndexedGeometry
            |> Sg.vertexAttribute DefaultSemantic.Colors colors
            |> Sg.trafo trafo

let viewScene (model : MModel) =
    let objectPass = RenderPass.after "bg" RenderPassOrder.Arbitrary RenderPass.main
    let background = 
        Aardvark.SceneGraph.SgPrimitives.Sg.fullScreenQuad 
        |> Sg.noEvents 
        |> Sg.pickable (PickShape.Box (Box3d(V3d(-1.0,-1.0,0.0), V3d(1.0,1.0,0.1))))
        |> Sg.withEvents [
            Sg.onMouseMove (fun p -> MoveCursor p)
            Sg.onClick(fun p -> MoveCursor p)
            //Sg.onLeave (fun _ -> Exit)
        ]   
        |> Sg.trafo (Trafo3d.Translation(1.0,1.0,0.0) * Trafo3d.Scale(0.5,0.5,0.5) |> Mod.constant)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Blue
           }

    let objectSg = 
        model.objects |> AMap.toASet |> ASet.mapM (fun (id,object) -> 
            adaptive {
                let! object = object
                match object with
                    | MPolygon(points,colors) -> 
                        return Sg.empty
                    | MRect(box,colors) -> 
                        let boxColors = colors |> Mod.map (fun colors -> Array.concat [colors |> Array.map C4b;colors|> Array.map C4b])
                        let colors2 = boxColors |> Mod.map (fun colors -> Geometry.indices |> Array.map (fun i -> colors.[i]))
                        let box = Geometry.box colors2 (box |> Mod.map (fun b2d -> Box3d.FromPoints(V3d(b2d.Min,0.0),V3d(b2d.Max,1.0)))) 
                        return box
            }
        ) 
        |> Sg.set
        |> Sg.pass objectPass
        |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
           }
        |> Sg.requirePicking
        |> Sg.noEvents 
        |> Sg.withEvents [
            Sg.onMouseMove (fun p -> MoveCursor p)
            Sg.onClick(fun p -> MoveCursor p)
            //Sg.onLeave (fun _ -> Exit)
        ]   

    Sg.ofSeq [background; objectSg]
        //|> Sg.trafo (Trafo3d.FromOrthoNormalBasis(V3d.IOO,V3d.OOI,V3d.OIO) |> Mod.constant)

let view (model : MModel) =


    let containerAttribs = 
        amap {
            yield style " width: 70%;margin:auto"; 
            //yield myMouseCbRel "onmousemove" "svgRoot" MouseMove
            //yield onKeyDown (fun k -> if k = Keys.Escape then Deselect else Nop)
        } |> AttributeMap.ofAMap

    let scene = 
        Sg.empty

    let frustum = Box3d(V3d(-0.5,-0.5,-1.0),V3d(0.5,0.5,1.0)) |> Frustum.ortho 
    //let frustum = Frustum.perspective 60.0 0.01 100.0 1.0

    let renderControl = 
        //CameraController.controlledControl' model.cameraState Camera (frustum |> Mod.constant)
        //            (AttributeMap.ofList [ style "width: 400px; height:400px; background: #222"; "useMapping" => "false"]) 
        //            (viewScene model)
        let camera = Camera.create (model.cameraState.view.GetValue()) frustum |> Mod.constant
        renderControl' camera [ style "width: 100%; height:100%; background: #222; border-style: solid; border-color: black; border-width:1px"; "useMapping" => "false"] (RenderControlConfig.noScaling true) (viewScene model)

    require dependencies (
        div [style "display: flex; flex-direction: row; width: 100%; height: 100%"] [
            Incremental.div containerAttribs <| AList.ofList [
                div [clazz "editorFrame"; ] [
                    renderControl
                ]
            ]

            Incremental.div (AttributeMap.ofList [ style "width: 30%; " ]) <| 
                alist {
                    let! selected = model.selectedObject
                    match selected with
                        | None -> yield text "no selection"
                        | Some s -> 
                            yield text (sprintf "Selection: %d" s)
                            yield br []
                }
        ]
    )

let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               objects = HMap.ofList [
                    (ObjectId.freshId(), Rect(Box2d.FromMinAndSize(V2d(0.0,0.0),V2d(0.7,0.7)), Array.init 4 (constF C4f.White)))
               ] 
               selectedObject = None
               cameraState = { CameraController.initial with view = CameraView.lookAt (V3d(0.5,0.5,0.5)) (V3d(0.5,0.5,0.0)) V3d.OIO }
            }
        update = update 
        view = view
    }
