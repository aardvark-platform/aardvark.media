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

    let update (model : ClientState) (msg : ClientMessage) =
        model

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
        
        let svgContent = AList.empty

        div [style "display: flex; flex-direction: row; width: 100%; height: 100%"] [
            div [style "display: flex; width: 70%;"] [
                Incremental.Svg.svg svgAttribs svgContent
            ]

            div [ style "display: flex; width: 30%;" ] [
            ]
        ]

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
                }
            update = update 
            view = view outer
        }


module DrawRectsApp =
    
    let update (m : Model) (msg : Message) =
        m

    let view (m : MModel) =
        div [] []

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