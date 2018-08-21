namespace DrawRects

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.UI.Operators


module Nvg =
    let floatString (v : float) =
        System.String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}", v)

    let rect x y width height attributes = 
        Svg.rect (
            [
                "x" => floatString x; "y"  => floatString y;
                "width" => floatString width; "height" =>  floatString height;
            ] @ attributes
        )

    let line (p0 : V2d) (p1 : V2d) attributes =
        Svg.line (
            [
                "x1" => floatString p0.X; "y1" => floatString p0.Y;
                "x2" => floatString p1.X; "y2" => floatString p1.Y;
            ] @ attributes
        )


module ClientApp =
    open Aardvark.Application

    type ClientMessage = 
        | MouseDown of MouseButtons * V2d
        | MouseMove of V2d
        | MouseUp of V2d
        | Select of int
        | SetColorMode of Color
        | Deselect
        | DragEndPoint of rectId:int * vertexId:int * vertices : array<V2d> 
        | Delete of int
        | Nop
        | ChangeColor of int * C4f
        | StopDrag

    let update (model : ClientState) (msg : ClientMessage) =
        match msg with
            | ChangeColor(i,c) -> 
                model
            | MouseDown(b,v) -> 
                if b = MouseButtons.Left then
                    { model with mouseDown = Some v; }
                else model
            | MouseMove v ->    
                match model.dragEndPoint with
                    | None -> 
                        match model.selectedRect with 
                            | None -> 
                                let v = V2d(clamp 0.0 1.0 v.X, clamp 0.0 1.0 v.Y)
                                match model.mouseDown with
                                    | None -> model
                                    | Some s -> 
                                        { model with workingRect = Some { s = s; t = v }; mouseDrag = Some v; currentInteraction = Interaction.CreatingRect }
                            | Some s -> 
                                match model.mouseDown with
                                    | Some startPos -> 
                                        let shift = v - startPos
                                        { model with dragRect = Some shift;  currentInteraction = Interaction.MovingRect }
                                    | None -> model
                    | Some d -> 
                        { model with dragEndPoint = Some { d with pos = v }}
            | StopDrag -> { model with dragRect = None }
            | MouseUp(v) -> 
                if model.downOnRect then
                    { model with downOnRect = false; dragRect  = None; mouseDown = None  }
                else
                    { model with workingRect = None; currentInteraction = Nothing; mouseDown = None; mouseDrag = None; dragEndPoint = None; dragRect = None  }
            | Select id -> 
                { model with currentInteraction = Nothing; selectedRect = Some id; workingRect = None; mouseDown = None; downOnRect = true }
            | Deselect -> if model.downOnRect then { model with downOnRect = false } else { model with selectedRect = None }
            | DragEndPoint(rectId,vertexId,vertices) -> 
                let fixedPoint =
                    match vertexId with 
                        | 0 -> 2 | 1 -> 3 | 2 -> 0 | 3 -> 1 | _ -> failwith ""
                { model with dragEndPoint = Some { rect = rectId; vertexId = vertexId; fixedPoint = vertices.[fixedPoint]; pos = vertices.[vertexId];  }; currentInteraction = Interaction.MovingPoint }
            | SetColorMode mode -> model
            | Delete i -> { model with dragEndPoint = None; selectedRect = None }
            | Nop -> model

    let endRectangle (client : ClientState) =
        match client.workingRect with
            | Some b -> 
                Rect.ofBox b |> Some
            | None -> None

    let dependencies = Html.semui @ [
        { name = "drawRects.css"; url = "drawRects.css"; kind = Stylesheet }
        { name = "drawRects.js";  url = "drawRects.js";  kind = Script     }
        { name = "spectrum.js";  url = "spectrum.js";  kind = Script     }
        { name = "spectrum.css";  url = "spectrum.css";  kind = Stylesheet     }
    ] 

    let myMouseCbRel (evtName : string) (containerClass : string) (cb : V2d -> 'msg) =
        let cb = function None -> Seq.empty | Some v -> Seq.singleton (cb v)
        onEvent' evtName [sprintf "relativeCoords2(event,'%s')" containerClass] (List.head >> Pickler.json.UnPickleOfString >> cb)

    let myMouseCbRelButton (evtName : string) (containerClass : string)  (cb : MouseButtons -> V2d -> 'msg) = 
        onEvent' 
            evtName
            [sprintf "relativeCoords2(event,'%s')" containerClass; "event.which"] 
            (fun args ->
                match args with
                    | x :: b :: _ ->
                        let v : Option<V2d> = Pickler.json.UnPickleOfString x
                        let b : MouseButtons =  b |> Helpers.button
                        match v with
                            | Some v -> cb b v |> Seq.singleton
                            | None -> Seq.empty
                    | _ ->
                        failwith "asdasd"
            )

    //https://bugs.chromium.org/p/chromium/issues/detail?id=716694&can=2&q=716694&colspec=ID%20Pri%20M%20Stars%20ReleaseBlock%20Component%20Status%20Owner%20Summary%20OS%20Modified
    let view (runtime : IRuntime) (model : MModel) (clientState : MClientState) =
    
        let svgAttribs = 
            amap {
                yield clazz "svgRoot"
                yield "width" => "100%"
                yield "height" => "100%"
                let! viewport = clientState.viewport
                let viewBox = sprintf "%f %f %f %f" viewport.Min.X viewport.Min.Y viewport.Size.X viewport.Size.Y
                yield "viewBox" => viewBox
                yield "preserveAspectRatio" => "none"
                yield myMouseCbRelButton "onmouseup"   "svgRoot" (fun b v -> if b = MouseButtons.Right then Deselect else MouseUp v)
                yield myMouseCbRelButton "onmousedown" "svgRoot" (fun b v -> if b = MouseButtons.Left then MouseDown(b,v) else Nop)
                yield onKeyDown (fun k -> if k = Keys.Escape then Deselect else Nop)
            } |> AttributeMap.ofAMap 

        let containerAttribs = 
            amap {
                yield style " width: 70%;margin:auto"; 
                yield myMouseCbRel "onmousemove" "svgRoot" MouseMove
                yield onKeyDown (fun k -> if k = Keys.Escape then Deselect else Nop)
            } |> AttributeMap.ofAMap
        
        let svgContent = 
            alist {
                for (id,r) in model.rects |> AMap.toASet |> ASet.sortBy (fun (id,r) -> id) do
                    let! selection = clientState.selectedRect
                    match selection with
                        | Some s when id = s ->
                            let! draggedPoint = clientState.dragEndPoint
                            let! dragRect = clientState.dragRect
                            let! box = r.box
                            let box =
                                match draggedPoint,dragRect with
                                | Some d,_ -> 
                                    Box2d.FromPoints(d.fixedPoint,d.pos)
                                | None, Some shift -> 
                                    box.Translated shift
                                | _ -> box
                            let w = 0.008
                            let vertices = [| box.OO; box.IO; box.II; box.OI |]
                            yield Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [
                                    style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke";
                                ]
                            yield Nvg.rect (box.Min.X - w) (box.Min.Y - w) (w*2.0) (w*2.0) [myMouseCbRelButton "onmousedown" "svgRoot" (fun _ p -> DragEndPoint(id, 0,vertices)); "vector-effect" => "non-scaling-stroke"]
                            yield Nvg.rect (box.Max.X - w) (box.Min.Y - w) (w*2.0) (w*2.0) [myMouseCbRelButton "onmousedown" "svgRoot" (fun _ p -> DragEndPoint(id, 1,vertices)); "vector-effect" => "non-scaling-stroke"]
                            yield Nvg.rect (box.Max.X - w) (box.Max.Y - w) (w*2.0) (w*2.0) [myMouseCbRelButton "onmousedown" "svgRoot" (fun _ p -> DragEndPoint(id, 2,vertices)); "vector-effect" => "non-scaling-stroke"]
                            yield Nvg.rect (box.Min.X - w) (box.Max.Y - w) (w*2.0) (w*2.0) [myMouseCbRelButton "onmousedown" "svgRoot" (fun _ p -> DragEndPoint(id, 3,vertices)); "vector-effect" => "non-scaling-stroke"]
                        | _ -> 
                            printfn "RERENDER....."
                            let! box = r.box
                            let! color = r.color
                            match color with
                                | Color.Constant c -> 
                                    yield 
                                        Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [
                                            style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke";
                                            onMouseDown (fun b _ -> if b = Aardvark.Application.MouseButtons.Right then Select id else Deselect)
                                        ]
                                | Color.Gradient(d,f,t) -> 
                                    yield 
                                        Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [
                                            style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke";
                                            onMouseDown (fun b _ -> if b = Aardvark.Application.MouseButtons.Right then Select id else Deselect)
                                        ]
                                | Color.Points pts -> 
                                    ()
                                    //let data,size = DrawRects.RenderQuads.renderQuad pts.colors runtime
                                    //let attribs = 
                                    //    [
                                    //        "x" => floatString x; "y"  => floatString y;
                                    //        "width" => floatString width; "height" =>  floatString height;
                                    //        "xlink:href" => data
                                    //    ]
                                    //yield  Svg.image ["width" ]

                let! o = clientState.workingRect
                match o with
                    | None -> ()
                    | Some r ->  
                        let box = Box2d.FromPoints(r.s,r.t)
                        yield Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [
                                style "fill:rgb(0,0,255);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke"; 
                              ]
            }


        let showSelection (id : int) =
            div [] [
                yield div [style "position:relative; width:30%; height:100px"] [
                    Incremental.Svg.svg (AttributeMap.ofList [clazz "rectPreview"; "viewBox" => "0 0 1 1"; "preserveAspectRatio" => "none"; "width" => "100%"; "height" => "100%" ]) <|
                        alist {
                            let! rect = model.rects |> AMap.tryFind id 
                            match rect with
                                | None -> yield text "could not find selected rect"
                                | Some r -> 
                                    let! box = r.box
                                    yield Nvg.rect 0.0 0.0 1.0 1.0 [style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke"]
                       }
                    Incremental.div AttributeMap.empty <|
                        alist {
                            let! rect = model.rects |> AMap.tryFind id 
                            match rect with
                                | Some r -> 
                                    let! color = r.color

                                    let withPicker (c : C4f) (index : int) (s : string)  =
                                        let ev = onEvent "changeColor" [] (((curry ChangeColor) index) << Spectrum.colorFromHex << Pickler.unpickleOfJson << List.head)
                                        let color = ColorPicker.colorToHex (c.ToC4b())
                                        let boot= Spectrum.bootCode.Replace("__COLOR__", color)
                                        onBoot boot <| div [style (sprintf "%s;background-color:%s; border-color: black; border-style:solid" s color); ev] []
                                        

                                    match color with
                                        | Color.Points colors -> 
                                            yield withPicker colors.colors.[0] 0 "position:absolute; left: 0%; top: 100%; margin-top:-10px; background-color: green; width:10px; height: 10px"
                                            yield withPicker colors.colors.[1] 1 "position:absolute; left: 100%; top: 100%; margin-left:-10px; margin-top:-10px; background-color: green; width:10px; height: 10px"
                                            yield withPicker colors.colors.[2] 2 "position:absolute; left: 100%; top: 0%; margin-left:-10px; background-color: green; width:10px; height: 10px"
                                            yield withPicker colors.colors.[3] 3 "position:absolute; left: 0%; top: 0%; background-color: green; width:10px; height: 10px"
                                        | Color.Constant c -> 
                                            yield withPicker c 0 "position:absolute; left: 50%; top: 50%; margin-left: -20px; margin-top: -20px; background-color: green; width:40px; height: 40px"
                                        | Color.Gradient(Direction.Vertical,f,t) -> 
                                            yield withPicker f 0 "position:absolute; left: 0%; top: 0%;  background-color: green; width:100%; height: 20px"
                                            yield withPicker t 1 "position:absolute; left: 0%; bottom: 0%;  background-color: green; width:100%; height:20px"
                                        | Color.Gradient(Direction.Horizontal,f,t) -> 
                                            yield withPicker f 0 "position:absolute; top: 0%; left: 0%; background-color: green; height:100%; width: 20px"
                                            yield withPicker t 0 "position:absolute; top: 0%; right: 0%;  background-color: green; height:100%; width: 20px"
                                | None -> ()
                        }

                ]
                let color =
                    adaptive {
                        let! rect = model.rects |> AMap.tryFind id
                        match rect with
                            | None -> return Color.Constant C4f.White
                            | Some r -> return! r.color
                    }

                let colorMode = color |> Mod.map (function Constant _ -> "Constant" | Color.Gradient(Direction.Vertical,_,_) -> "Vertical Gradient" | Color.Gradient(Direction.Horizontal,_,_) -> "Horizontal Gradient" | Color.Points _ -> "Points")
                let changeMode s =
                    match s with
                        | "Constant" -> Color.Constant C4f.White
                        | "Vertical Gradient" -> Color.Gradient(Direction.Vertical, C4f.White, C4f.White)
                        | "Horizontal Gradient" -> Color.Gradient(Direction.Horizontal, C4f.White, C4f.White)
                        | "Points" -> Color.Points { colors = Array.init 4 (fun _ -> C4f.White) }
                        | _ -> failwith ""
                yield br []
                yield text "Color Mode: "
                yield div [] [
                    dropDown [] colorMode (changeMode >> SetColorMode) (Map.ofList [ "Constant", "Constant"; "Vertical Gradient", "Vertical Gradient";"Horizontal Gradient", "Horizontal Gradient";"Points", "Points"])
                ]
                yield button [style "ui small red button"; onClick (fun _ -> Delete id)] [text "Delete"]
            ]

        require dependencies (
            div [style "display: flex; flex-direction: row; width: 100%; height: 100%"] [
                Incremental.div containerAttribs <| AList.ofList [
                    div [clazz "editorFrame"; ] [
                        Incremental.Svg.svg svgAttribs svgContent
                    ]
                ]

                Incremental.div (AttributeMap.ofList [ style "width: 30%; " ]) <| 
                    alist {
                        let! selected = clientState.selectedRect
                        match selected with
                            | None -> yield text "no selection"
                            | Some s -> 
                                yield text (sprintf "Selection: %d" s)
                                yield br []
                                yield showSelection s
                    }
            ]
        )

    let threads (model : ClientState) = 
        ThreadPool.empty


    let app runtime outer =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                   viewport = Box2d.Unit
                   selectedRect = None
                   currentInteraction = Interaction.Nothing
                   workingRect = None
                   dragEndPoint = None
                   downOnRect = false

                   dragRect = None

                   mouseDown = None
                   mouseDrag = None
                }
            update = update 
            view = view runtime outer
        }

open ClientApp


type Message = 
    | AddRectangle of Rect
    | SetColorMode of int * Color
    | SetBoxBounds of int * V2d * V2d
    | Delete of int
    | Translate of int * V2d
    | ChangeColor of selection : int * point : int * C4f


module DrawRectsApp =
    
    let update (m : Model) (msg : Message) =
        //printfn "[server] %A" msg
        match msg with
            | AddRectangle r  ->
                if r.box.Area < 0.0001 then 
                    printfn "supersmall %A" r
                    m //{ m with rects = HMap.add r.id r m.rects }
                else { m with rects = HMap.add r.id r m.rects }
            | SetColorMode(id,c) -> 
                let update old =
                    match old with
                        | None -> None
                        | Some r -> { r with color = c } |> Some
                { m with rects = HMap.alter id update m.rects }
            | SetBoxBounds(id,startPos,endPos) ->
                let update old =
                    match old with
                        | None -> None
                        | Some r -> { r with box = Box2d.FromPoints(startPos,endPos) } |> Some
                { m with rects = HMap.alter id update m.rects }
            | Translate(id, shift) -> 
                let update old =
                    match old with
                        | None -> None
                        | Some r -> { r with box = r.box.Translated shift } |> Some
                { m with rects = HMap.alter id update m.rects }
            | Delete i -> 
                { m with rects = HMap.remove i m.rects }
            | ChangeColor(id,point,color) -> 
                let update old =
                    match old with
                        | None -> None
                        | Some r -> 
                            match r.color with  
                                | Color.Constant c -> Some { r with color = Color.Constant color }
                                | Color.Gradient(dir,f,t) -> Some { r with color = Color.Gradient(dir,(if point = 0 then color else f), (if point = 1 then color else t))}
                                | Color.Points(pts) -> 
                                    let points = pts.colors.Copy()
                                    points.[point] <- color
                                    Some { r with color = Color.Points { colors = points } }
                { m with rects = HMap.alter id update m.rects }  


    let mapOut (m : ClientState) (msg : ClientMessage) =
        seq {
            match msg with
                | ClientMessage.MouseUp v when m.currentInteraction = Interaction.CreatingRect -> 
                    yield! (ClientApp.endRectangle m |> Option.map AddRectangle |> Option.toList)
                | ClientMessage.MouseUp v when m.currentInteraction = Interaction.MovingPoint -> 
                    match m.selectedRect, m.dragEndPoint with
                        | Some id, Some d -> yield SetBoxBounds(id,d.fixedPoint, d.pos)
                        | _ -> ()
                | ClientMessage.MouseUp v when m.currentInteraction = Interaction.MovingRect -> 
                    match m.mouseDown, m.dragRect, m.selectedRect with
                        | Some d, Some c, Some id -> yield Translate(id,c)
                        | _ -> ()
                | ClientMessage.SetColorMode mode -> 
                    match m.selectedRect with
                        | None -> ()
                        | Some id -> yield SetColorMode(id,mode)
                | ClientMessage.Delete i -> yield Delete i
                | ClientMessage.ChangeColor(point,c) -> 
                    match m.selectedRect with 
                        | Some id -> yield ChangeColor(id,point,c)
                        | _ -> ()
                | _ -> ()
        }

    let view (runtime : IRuntime) (m : MModel) =
        body ["oncontextmenu" => "return false;"] [
            subApp' mapOut (fun _ msg -> match msg with Translate(_,_) -> Seq.singleton ClientMessage.StopDrag | _ -> Seq.empty) [] (ClientApp.app runtime m)
        ]

    let threads (m : Model) = ThreadPool.empty

    let app (runtime : IRuntime) =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                   rects = HMap.empty
                }
            update = update
            view = view runtime
        }