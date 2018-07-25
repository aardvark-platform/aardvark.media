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
        | ClientMessage
        | StartRectangle of V2d
        | MoveEndpoint of V2d
        | EndRectangle of V2d

    let update (model : ClientState) (msg : ClientMessage) =
        printfn "%A" msg
        match msg with
            | StartRectangle v -> 
                { model with currentInteraction = Interaction.CreatingRect; workingRect = Some (Box2d.FromMinAndSize(v,V2d.OO)) }
            | MoveEndpoint v  -> 
                match model.workingRect with
                    | None -> model
                    | Some b -> { model with workingRect = Some (Box2d.FromPoints(b.Min, v)) }
            | EndRectangle v -> 
                { model with workingRect = None; currentInteraction = Nothing }
            | _ -> model

    let endRectangle (client : ClientState) =
        match client.workingRect with
            | Some b -> 
                Rect.ofBox b |> Some
            | None -> None

    let dependencies = [
        { name = "drawRects.css"; url = "drawRects.css"; kind = Stylesheet }
        { name = "drawRects.js";  url = "drawRects.js";  kind = Script     }
    ]

    let myMouseCbRel (evtName : string) (containerClass : string) (cb : V2d -> 'msg) =
        let cb = function None -> Seq.empty | Some v -> Seq.singleton (cb v)
        onEvent' evtName [sprintf "relativeCoords2(event,'%s')" containerClass] (List.head >> Pickler.json.UnPickleOfString >> cb)

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
                //let! dragging = clientState.dragging
                //if Option.isNone dragging then 
                //    yield onMouseDownRel (toGlobalSpace >> TFMessage.AddPoint >> TFMessage)
                //yield onMouseUpRel (fun _ p -> StopDrag)
                //if Option.isSome dragging then
                //     yield onMouseMoveRel (fun n -> toGlobalSpace n |> Drag)
            } |> AttributeMap.ofAMap 

        let containerAttribs = 
            amap {
                yield style "display: flex; width: 70%;"; 
                let! currentInteraction = clientState.currentInteraction
                match currentInteraction with
                    | Nothing -> 
                        yield myMouseCbRel "onmousedown" "svgRoot" StartRectangle
                    | CreatingRect -> 
                        yield myMouseCbRel "onmousemove" "svgRoot" MoveEndpoint
                        yield myMouseCbRel "onmouseup"   "svgRoot" EndRectangle
                    | _ -> 
                        ()


            } |> AttributeMap.ofAMap
        
        let svgContent = AList.empty

        require dependencies (
            div [style "display: flex; flex-direction: row; width: 100%; height: 100%"] [
                Incremental.div containerAttribs <| AList.ofList [
                    div [clazz "editorFrame"; ] [
                        Incremental.Svg.svg svgAttribs svgContent
                    ]
                ]

                div [ style "display: flex; width: 30%;" ] [
                ]
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
                }
            update = update 
            view = view outer
        }

open ClientApp


type Message = 
    | AddRectangle of Rect


module DrawRectsApp =
    
    let update (m : Model) (msg : Message) =
        printfn "%A" msg
        match msg with
            | AddRectangle r -> { m with rects = HMap.add r.id r m.rects }

    let mapOut (m : ClientState) (msg : ClientMessage) =
        seq {
            match msg with
                | EndRectangle v -> 
                    printfn "do it"
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