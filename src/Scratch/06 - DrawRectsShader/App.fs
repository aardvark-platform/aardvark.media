module Inc.App

open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Operators

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model
open Aardvark.Application

open Utils

type Where = 
    | Canvas
    | Object of int

type Message = 
    | Camera of FreeFlyController.Message
    | MouseMove of Where * V3d
    | MouseDown of Where * MouseButtons * V3d
    | MouseUp of Where
    | ClickObject of int
    | DragEndPoint of objectId : int * vertexId : int * vertices : V2d[]
    | HoverHandle of vertexId : Option<int>
    | ChangeColor of int * C4f
    | SetColorMode of ColorKind
    | SetDirection of Direction
    | Delete of int
    | ChangeAlpha of Option<int> * V2d

let defaultColor = C4f(1.0,1.0,1.0,0.3)

module Color =
    let initial =
        {
            kind = ColorKind.Constant
            gradient = { direction = Direction.Horizontal; f = defaultColor; t = defaultColor }
            points = { colors = Array.init 4 (constF defaultColor)}
            constant = defaultColor
        }

let changeColor (model : Model) (point : int) (f : C4f -> C4f) =
    let update old =
        match old with
            | None -> None
            | Some (Rect(b,c) as r) -> 
                match c.kind with  
                    | ColorKind.Constant -> Some (Rect(b, { c with constant =  f c.constant}))
                    | ColorKind.Gradient-> 
                            Some (Rect(b, { c with gradient = { c.gradient with f = (if point = 0 then f c.gradient.f else c.gradient.f); t = (if point = 1 then f c.gradient.t else c.gradient.t)}}) )
                    | ColorKind.Points-> 
                        let points = c.points.colors.Copy()
                        points.[point] <- f points.[point]
                        Some (Rect(b, {c with points = { colors = points }}))
                    | _ -> failwith ""
            | _ -> failwith ""
    match model.selectedObject with
        | None -> model
        | Some id -> { model with objects = HMap.alter id update model.objects}

let horizontalMapping =
    [ 0, 0; 1, 1; 2, 1; 3,0 ] |> Map.ofList

let verticalMapping =
    [ 0,1; 1,1; 2,0; 3,0] |> Map.ofList

let update (model : Model) (msg : Message) =    
    printfn "%A" msg
    match msg with
        | MouseDown(Canvas, MouseButtons.Left, p) -> 
            { model with down = Some p; selectedObject = None  } 
        | MouseDown(Object _, MouseButtons.Left, p) -> 
            { model with down = Some p;  } 
        | MouseMove(location,p) -> 
            match model.dragEndpoint with 
                | None -> 
                    match model.down with
                    | Some source -> // are we dragging
                        match model.selectedObject with
                            | None -> // we are dragging nothing -> create rect
                                { model with dragging = Some { f = source; t = p }; openRect = Some (Box3d.FromPoints(source, p)) }
                            | Some obj -> // we are dragging something
                                { model with dragging = Some { f = source; t = p }; translation = Some (p - source) }
                    | None -> 
                        // nothing
                        model
                | Some d -> 
                    { model with dragEndpoint = Some { d with pos = p.XY }}
        | MouseUp(_) -> 
            let model' =
                match model.openRect, model.translation, model.selectedObject, model.dragEndpoint with
                    | Some r, None, None, None -> 
                        let newObject = Rect(Box2d(r.Min.XY,r.Max.XY), Color.initial)
                        let freshId = ObjectId.freshId()
                        { model with objects = HMap.add freshId newObject model.objects; openRect = None; selectedObject = Some freshId }
                    | None, Some t, Some id, None -> 
                        let update o =
                            match o with
                                | None -> None
                                | Some old ->
                                    match old with
                                        | Rect(b,c) -> Rect(b.Translated t.XY, c) |> Some
                                        | Polygon(vs,cs) -> failwith "not implemented"
                        { model with objects = HMap.alter id update model.objects; translation = None }
                    | None, None, Some id, Some d -> 
                        let update o =
                            match o with
                                | None -> None
                                | Some old ->
                                    match old with
                                        | Rect(b,c) -> Rect(Box2d.FromPoints(d.fixedPoint,d.pos),c) |> Some
                                        | Polygon(vs,cs) -> failwith "not implemented"
                        { model with objects = HMap.alter id update model.objects; dragEndpoint = None }
                    | o,t,i,d -> 
                        Log.warn "%A" (o,t,i,d)
                        model
                    
            { model' with dragging = None; down = None }
        | ClickObject(obj) when model.dragging.IsNone ->    
            { model with selectedObject = Some obj }
        | DragEndPoint(objectId,vertexId,vertices) ->
            let fixedPoint =
                    match vertexId with 
                        | 0 -> 2 | 1 -> 3 | 2 -> 0 | 3 -> 1 | _ -> failwith ""
            let dragEndPoint = { rect = objectId; vertexId = vertexId; fixedPoint = vertices.[fixedPoint]; pos = vertices.[vertexId];  }; 
            { model with dragEndpoint = Some dragEndPoint }
        | SetColorMode m -> 
            match model.selectedObject with
                | None -> model
                | Some s -> 
                    let update old =
                        match old with
                            | None -> None
                            | Some (Rect(b,c)) -> Some (Rect(b,{c with kind = m }))
                            | Some other -> Some other
                    { model with objects = HMap.alter s update model.objects}
        | SetDirection d -> 
            match model.selectedObject with
                | None -> model
                | Some s -> 
                    let update old =
                        match old with
                            | None -> None
                            | Some (Rect(b,c)) -> Some (Rect(b,{c with gradient = {c.gradient with direction = d} }))
                            | Some other -> Some other
                    { model with objects = HMap.alter s update model.objects}
        | ChangeColor(point,color) -> 
            changeColor model point (constF color)
        | Delete index -> 
            { model with objects = HMap.remove index model.objects }
        | HoverHandle(vertexId) -> 
            match model.selectedObject with
                | None -> model
                | Some i -> 
                    match HMap.tryFind i model.objects with
                        | None -> model
                        | Some o ->
                            match o with
                                | Rect(b,c) -> 
                                    match c.kind,c.gradient.direction with
                                        | ColorKind.Constant,_ -> { model with hoverHandle = vertexId }
                                        | ColorKind.Points, _ -> { model with hoverHandle = vertexId }
                                        | ColorKind.Gradient, Direction.Horizontal -> { model with hoverHandle = Option.map ((flip Map.find) horizontalMapping) vertexId }
                                        | ColorKind.Gradient, Direction.Vertical -> { model with hoverHandle = Option.map ((flip Map.find) verticalMapping) vertexId }
                                        | _ -> failwith ""
                                 | _ -> failwith ""
        | ChangeAlpha(targetId, delta) -> 
            let changeAlpha (c : C4f) =
                let newAlpha = c.A + float32 delta.Y / -1000.0f
                C4f(c.R,c.G,c.B,clamp 0.0f 1.0f newAlpha)
            match targetId, model.hoverHandle with
                | Some h, _ -> changeColor model h changeAlpha
                | None, Some h -> changeColor model h changeAlpha
                | _ -> model
        | Camera c -> { model with cameraState = FreeFlyController.update model.cameraState c }
        | _ -> model

let dependencies = Html.semui @ [
    { name = "drawRects.css"; url = "drawRects.css"; kind = Stylesheet }
    { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
    { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
] 


let viewBox (box : IMod<Box2d>) (colors : IMod<array<C4f>>) =
    let boxColors = colors |> Mod.map (fun colors -> Array.concat [colors |> Array.map C4b;colors|> Array.map C4b])
    let colors2 = boxColors |> Mod.map (fun colors -> Utils.Geometry.indices |> Array.map (fun i -> colors.[i]))
    let b = box |> Mod.map (fun b2d -> Box3d.FromPoints(V3d(b2d.Min,0.0),V3d(b2d.Max,1.0)))
    let box = Utils.Geometry.box colors2 b 
    let s = b |> Mod.map (fun b -> if b.Min.AnyNaN || b.Max.AnyNaN then failwith "" else PickShape.Box b)
    box |> Sg.pickable' s
    
let unpackColor (color : Color) =
    let t = color.gradient.t
    let f = color.gradient.f
    match color.kind, color.gradient.direction with
        | ColorKind.Constant,_ -> Array.init 4 (constF color.constant)
        | ColorKind.Points,_  -> color.points.colors
        | ColorKind.Gradient, Direction.Horizontal -> 
            [| f; t; t; f |]
        | ColorKind.Gradient, Direction.Vertical -> 
            [| t; t; f; f |]
        | _ -> failwith ""

let viewObject (object : MObject) =
    match object with
        | MPolygon(points,colors) -> 
            Sg.empty
        | MRect(box,color) -> 
            let colors = color |> Mod.map unpackColor
            viewBox box colors

let viewScene (model : MModel) =
    let objectPass = RenderPass.after "bg" RenderPassOrder.Arbitrary RenderPass.main
    let background = 
        Aardvark.SceneGraph.SgPrimitives.Sg.fullScreenQuad 
        |> Sg.noEvents 
        |> Sg.pickable (PickShape.Box (Box3d(V3d(-1.0,-1.0,0.0), V3d(1.0,1.0,0.1))))
        |> Sg.withEvents [
            Sg.onMouseMove (fun p -> MouseMove(Where.Canvas, p))
            Sg.onMouseDown (fun button p -> MouseDown(Where.Canvas,button,p))
            Sg.onMouseUp   (fun _ -> MouseUp Where.Canvas)
        ]   
        |> Sg.trafo (Trafo3d.Translation(1.0,1.0,0.0) * Trafo3d.Scale(0.5,0.5,0.5) |> Mod.constant)
        |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Black
           }

    let selection =
       adaptive {
           let! id = model.selectedObject
           match id with
               | Some id -> 
                   let! object = AMap.tryFind id model.objects
                   match object with
                        | None -> return Sg.empty
                        | Some object -> 
                            let! object = object
                            let trafo = model.translation |> Mod.map (function Some t -> Trafo3d.Translation t | _ -> Trafo3d.Identity)
                            match object with
                                | MRect(originalBox,color) -> 
                                    let! box =
                                        Mod.map2 (fun originalBox dragEndPoint -> 
                                            match dragEndPoint with
                                                | None -> originalBox
                                                | Some d -> Box2d.FromPoints(d.fixedPoint,d.pos)
                                        ) originalBox model.dragEndpoint
                                    let vertices = [|box.Min; box.Min + box.Size.XO; box.Min + box.Size; box.Min + box.Size.OY|]
                                    let colors = color |> Mod.map unpackColor
                                    let baseObject = 
                                        viewBox (Mod.constant box) (colors) 
                                        |> Sg.withEvents [ 
                                            Sg.onClick (fun p -> ClickObject id); 
                                            Sg.onMouseDown (fun b p -> MouseDown(Where.Object id, b, p))
                                            Sg.onEnter (fun _ -> HoverHandle (Some 0))
                                            Sg.onLeave (fun _ -> HoverHandle None)
                                          ]

                                    return
                                        vertices |> Array.mapi (fun i origin ->
                                            let b = Box3d.FromCenterAndSize(V3d(origin,1.0),V3d.III*0.01)
                                            Sg.box (Mod.constant C4b.White) (Mod.constant b)
                                            |> Sg.pickable (PickShape.Box b)
                                            |> Sg.withEvents [
                                                    Sg.onMouseDown (fun b p -> DragEndPoint(id, i, vertices))
                                                    Sg.onEnter (fun _ -> HoverHandle (Some i))
                                                    Sg.onLeave (fun _ -> HoverHandle None)
                                                ]
                                        ) 
                                        |> Sg.ofSeq
                                        |> Sg.andAlso baseObject
                                        |> Sg.trafo trafo
                                | _ -> return Sg.empty
               | None -> return Sg.empty
       } |> Sg.dynamic

    let openRect =
        adaptive {
            let! openRect = model.openRect
            match openRect with
                | None -> return Sg.empty
                | Some o -> 
                    return viewBox (Mod.constant (Box2d(o.Min.XY,o.Max.XY))) (Array.init 4 (constF defaultColor) |> Mod.constant)
                    
        } |> Sg.dynamic

    let objectSg = 
        model.objects 
            |> AMap.toASet 
            |> ASet.filterM (fun (id,_) -> model.selectedObject |> Mod.map (function Some s -> s <> id | _ -> true)) 
            |> ASet.mapM (fun (id,object) -> 
                adaptive {
                    let! object = object
                    let sg = viewObject object 
                    return sg |> Sg.withEvents [
                        Sg.onMouseMove (fun p -> MouseMove(Where.Object id, p))
                        Sg.onClick(fun p -> ClickObject id)
                        Sg.onMouseDown (fun b p -> MouseDown(Where.Object id, b, p))
                        Sg.onMouseUp (fun p -> MouseUp(Where.Object id))

                    ]  
                }
        ) 
        |> Sg.set
        |> Sg.andAlso openRect
        |> Sg.andAlso selection
        |> Sg.pass objectPass
        |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
           }
        |> Sg.requirePicking
        |> Sg.noEvents 
 

    Sg.ofSeq [background; objectSg] 
        |> Sg.blendMode (Mod.constant BlendMode.Blend)
        |> Sg.depthTest (Mod.constant DepthTestMode.None)
        //|> Sg.trafo (Trafo3d.FromOrthoNormalBasis(V3d.IOO,V3d.OOI,V3d.OIO) |> Mod.constant)

 

let view (model : MModel) =

    let containerAttribs = 
        amap {
            yield style " width: 70%;margin:auto"; 
        } |> AttributeMap.ofAMap

    let scene = 
        Sg.empty

    let frustum = Box3d(V3d(-0.5,-0.5,-1.0),V3d(0.5,0.5,1.0)) |> Frustum.ortho 
    //let frustum = Frustum.perspective 60.0 0.01 100.0 1.0

    let renderControl = 
        //FreeFlyController.controlledControl' model.cameraState Camera (frustum |> Mod.constant)
        //            (AttributeMap.ofList [ style "width: 400px; height:400px; background: #222"; "useMapping" => "false"]) 
        //            (viewScene model)
        let camera = Camera.create (model.cameraState.view.GetValue()) frustum |> Mod.constant
        renderControl' camera [ style "width: 100%; height:100%; background: #222; border-style: solid; border-color: black; border-width:1px"; 
                                "useMapping" => "false"
                                onWheel' (fun delta pos -> ChangeAlpha(None,delta))] RenderControlConfig.noScaling (viewScene model)


    let showSelection (id : int) =
        div [] [
            yield div [style "position:relative; width:30%; height:100px"] [
                Incremental.Svg.svg (AttributeMap.ofList [clazz "rectPreview"; "viewBox" => "0 0 1 1"; "preserveAspectRatio" => "none"; "width" => "100%"; "height" => "100%" ]) <|
                    alist {
                        let! obj = model.objects |> AMap.tryFind id 
                        match obj with
                            | None -> yield text "could not find selected rect"
                            | Some o -> 
                                let! o = o
                                match o with
                                    | MRect(b,colors) -> 
                                        yield Nvg.rect 0.0 0.0 1.0 1.0 [style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke"]
                                    | _ -> failwith "not implemented"
                    }
                Incremental.div AttributeMap.empty <|
                    alist {
                        let! o = model.objects |> AMap.tryFind id 
                        match o with
                            | Some o -> 
                                let! o = o 
                                match o with    
                                    | MRect(b,color) -> 
                                        let! colors = color |> Mod.map unpackColor
                                        let! color = color

                                        let withPicker (c : C4f) (index : int) (s : string)  =
                                            let cc i c = ChangeColor(i,c)
                                            let ev args =
                                                match args with
                                                    | colorString::indexString::_ -> 
                                                        let color = colorString |> Pickler.unpickleOfJson |> Spectrum.colorFromHex
                                                        let id = Pickler.unpickleOfJson indexString
                                                        cc id color |> Seq.singleton
                                                    | _ -> Seq.empty
                                            let ev = onEvent' "changeColor" [] ev
                                            let color = ColorPicker.colorToHex (c.ToC4b())
                                            let boot= Spectrum.bootCode.Replace("__COLOR__", color).Replace("__INDEX__",sprintf "%d" index)
                                            onBoot boot <| div [
                                                style (sprintf "%s;background-color:%s; border-color: black; border-style:solid" s color); 
                                                ev
                                                onWheel (fun delta -> ChangeAlpha(Some index, delta*(-100.0)))
                                            ] []
                                        

                                        match color.kind with
                                            | ColorKind.Points -> 
                                                let colors = color.points.colors
                                                yield withPicker colors.[0] 0 "position:absolute; left: 0%; top: 100%; margin-top:-10px; background-color: green; width:10px; height: 10px"
                                                yield withPicker colors.[1] 1 "position:absolute; left: 100%; topf: 100%; margin-left:-10px; margin-top:-10px; background-color: green; width:10px; height: 10px"
                                                yield withPicker colors.[2] 2 "position:absolute; left: 100%; top: 0%; margin-left:-10px; background-color: green; width:10px; height: 10px"
                                                yield withPicker colors.[3] 3 "position:absolute; left: 0%; top: 0%; background-color: green; width:10px; height: 10px"
                                            | ColorKind.Constant -> 
                                                let c = color.constant
                                                yield withPicker c 0 "position:absolute; left: 50%; top: 50%; margin-left: -20px; margin-top: -20px; background-color: green; width:40px; height: 40px"
                                            | ColorKind.Gradient when color.gradient.direction = Direction.Vertical -> 
                                                yield withPicker color.gradient.f 0 "position:absolute; left: 0%; top: 0%;  background-color: green; width:100%; height: 20px"
                                                yield withPicker color.gradient.t 1 "position:absolute; left: 0%; bottom: 0%;  background-color: green; width:100%; height:20px"
                                            | ColorKind.Gradient when color.gradient.direction = Direction.Horizontal-> 
                                                yield withPicker color.gradient.f 0 "position:absolute; top: 0%; left: 0%; background-color: green; height:100%; width: 20px"
                                                yield withPicker color.gradient.t 1 "position:absolute; top: 0%; right: 0%;  background-color: green; height:100%; width: 20px"
                                            | _ -> ()
                                    | _ -> failwith "not implemented"
                            | None -> ()
                    }

            ]
            yield br []
            yield text "Color Mode: "
            yield div [] [
                let mode = 
                    adaptive {
                        let! selection = model.selectedObject
                        match selection with
                            | None -> return ColorKind.Constant, Direction.Horizontal
                            | Some s -> 
                                let! o = model.objects |> AMap.tryFind s
                                match o with
                                    | None -> return ColorKind.Constant, Direction.Horizontal
                                    | Some o -> 
                                        let! o = o
                                        match o with
                                            | MRect(b,c) -> 
                                                let! c = c
                                                return c.kind, c.gradient.direction
                                            | _ -> return ColorKind.Constant, Direction.Horizontal
                    }
                yield Html.SemUi.dropDown (Mod.map fst mode) SetColorMode
                yield br []
                yield text "Gradient Direction"
                yield br []
                yield Html.SemUi.dropDown (Mod.map snd mode) SetDirection
            ]
            yield button [style "ui small red button"; onClick (fun _ -> Delete id)] [text "Delete"]
        ]

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
                            yield showSelection s
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
                     (ObjectId.freshId(), Rect(Box2d.FromMinAndSize(V2d(0.0,0.0),V2d(0.7,0.7)), Color.initial))
                ] 
                selectedObject = None
                cameraState = { FreeFlyController.initial with view = CameraView.lookAt (V3d(0.5,0.5,0.5)) (V3d(0.5,0.5,0.0)) V3d.OIO }
                
                dragEndpoint = None
                translation = None
                down = None
                dragging = None
                openRect = None
                hoverHandle = None
            }
        update = update 
        view = view
    }
