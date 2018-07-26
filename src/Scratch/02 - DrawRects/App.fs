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

    type ClientMessage = 
        | MouseDown of V2d
        | MouseMove of V2d
        | MouseUp of V2d
        | Select of int
        | Deselect

    let update (model : ClientState) (msg : ClientMessage) =
        printfn "[Client] %A" msg
        match msg with
            | MouseDown v -> 
                { model with mouseDown = Some v; }
            | MouseMove v ->    
                let v = V2d(clamp 0.0 1.0 v.X, clamp 0.0 1.0 v.Y)
                match model.mouseDown with
                    | None -> model
                    | Some s -> 
                        { model with workingRect = Some { s = s; t = v }; mouseDrag = Some v; currentInteraction = Interaction.CreatingRect }
            | MouseUp v -> 
                { model with workingRect = None; currentInteraction = Nothing; mouseDown = None; mouseDrag = None; }
            | Select id -> 
                { model with currentInteraction = Nothing; selectedRect = Some id; workingRect = None; mouseDown = None }
            | Deselect -> { model with selectedRect = None }

    let endRectangle (client : ClientState) =
        match client.workingRect with
            | Some b -> 
                Rect.ofBox b |> Some
            | None -> None

    let dependencies = Html.semui @ [
        { name = "drawRects.css"; url = "drawRects.css"; kind = Stylesheet }
        { name = "drawRects.js";  url = "drawRects.js";  kind = Script     }
    ] 

    let myMouseCbRel (evtName : string) (containerClass : string) (cb : V2d -> 'msg) =
        let cb = function None -> Seq.empty | Some v -> Seq.singleton (cb v)
        onEvent' evtName [sprintf "relativeCoords2(event,'%s')" containerClass] (List.head >> Pickler.json.UnPickleOfString >> cb)

    //https://bugs.chromium.org/p/chromium/issues/detail?id=716694&can=2&q=716694&colspec=ID%20Pri%20M%20Stars%20ReleaseBlock%20Component%20Status%20Owner%20Summary%20OS%20Modified
    let view (model : MModel) (clientState : MClientState) =
    
        let svgAttribs = 
            amap {
                yield clazz "svgRoot"
                yield "width" => "100%"
                yield "height" => "100%"
                let! viewport = clientState.viewport
                let viewBox = sprintf "%f %f %f %f" viewport.Min.X viewport.Min.Y viewport.Size.X viewport.Size.Y
                yield "viewBox" => viewBox
                yield "preserveAspectRatio" => "none"
                yield myMouseCbRel "onmousedown" "svgRoot" MouseDown
                yield myMouseCbRel "onmouseup"   "svgRoot" MouseUp
            } |> AttributeMap.ofAMap 

        let containerAttribs = 
            amap {
                yield style " width: 70%;margin:auto"; 
                yield myMouseCbRel "onmousemove" "svgRoot" MouseMove
            } |> AttributeMap.ofAMap
        
        let svgContent = 
            alist {
                for (id,r) in model.rects |> AMap.toASet |> ASet.sortBy (fun (id,r) -> id) do
                    let! box = r.box
                    yield Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [
                            style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"; "vector-effect" => "non-scaling-stroke";
                            onMouseDown (fun b _ -> if b = Aardvark.Application.MouseButtons.Right then Select id else Deselect)
                            ]

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
            div [style "position:absolute; width:30%; height:100px"] [
                Incremental.Svg.svg (AttributeMap.ofList [clazz "rectPreview"; "viewBox" => "0 0 100 100"; "preserveAspectRatio" => "none"; "width" => "100%"; "height" => "100%" ]) <|
                    alist {
                        let! rect = model.rects |> AMap.tryFind id 
                        match rect with
                            | None -> yield text "could not find selected rect"
                            | Some r -> 
                                let! box = r.box
                                yield Nvg.rect box.Min.X box.Min.Y box.Size.X box.Size.Y [style "fill:rgba(0,0,255,0.4);stroke-width:1;stroke:rgb(0,0,0);"]
                   }
                div [style "position:relative; left: 10%; top: 10%; background-color: green; width:10px; height: 10px"] []
            ]

        require dependencies (
            div [style "display: flex; flex-direction: row; width: 100%; height: 100%"] [
                Incremental.div containerAttribs <| AList.ofList [
                    div [clazz "editorFrame"; ] [
                        Incremental.Svg.svg svgAttribs svgContent
                    ]
                ]

                Incremental.div (AttributeMap.ofList [ style "width: 30%; margin:auto" ]) <| 
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


    let app outer =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                   viewport = Box2d.Unit
                   selectedRect = None
                   currentInteraction = Interaction.Nothing
                   workingRect = None

                   mouseDown = None
                   mouseDrag = None
                }
            update = update 
            view = view outer
        }

open ClientApp


type Message = 
    | AddRectangle of Rect


module DrawRectsApp =
    
    let update (m : Model) (msg : Message) =
        printfn "[server] %A" msg
        match msg with
            | AddRectangle r -> { m with rects = HMap.add r.id r m.rects }


    let mapOut (m : ClientState) (msg : ClientMessage) =
        seq {
            match msg with
                | MouseUp v when m.currentInteraction = Interaction.CreatingRect -> 
                    yield! (ClientApp.endRectangle m |> Option.map AddRectangle |> Option.toList)
                | _ -> ()
        }

    let view (m : MModel) =
        body [] [
            subApp' mapOut (fun _ _ -> Seq.empty) [] (ClientApp.app m)
        ]

    let threads (m : Model) = ThreadPool.empty

    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                   rects = HMap.empty
                }
            update = update 
            view = view
        }