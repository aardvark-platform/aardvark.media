namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Scratch.DomainTypes

module SimpleDrawingApp =

    open AnotherSceneGraph
    open Elmish3DADaptive
    open SimpleDrawingApp
    open Primitives


    type Action =
        | ClosePolygon
        | AddPoint   of V3d
        | MoveCursor of V3d

    let update (m : Model) (cmd : Action) =
        match cmd with
            | ClosePolygon -> 
                match m.working with
                    | None -> m
                    | Some p -> 
                        { m with 
                            working = None 
                            finished = PSet.add p.finishedPoints m.finished
                        }
            | AddPoint p ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = [ p ]; cursor = None;  }}
                    | Some v -> 
                        { m with working = Some { v with finishedPoints = p :: v.finishedPoints }}
            | MoveCursor p ->
                match m.working with
                    | None -> { m with working = Some { finishedPoints = []; cursor = Some p }}
                    | Some v -> { m with working = Some { v with cursor = Some p }}


    let viewPolygon (p : list<V3d>) =
        [ for edge in Polygon3d(p |> List.toSeq).EdgeLines do
            let v = edge.P1 - edge.P0
            yield cylinder edge.P0 v.Normalized v.Length 0.03 |> render Pick.ignore 
        ] |> group


    let view (m : MModel) = 
        let t =
           aset {
                yield [ Quad (Quad3d [| V3d(-1,-1,0); V3d(1,-1,0); V3d(1,1,0); V3d(-1,1,0) |]) 
                            |> render [ 
                                 on Event.move MoveCursor
                                 on (Event.down' MouseButtons.Left)  AddPoint 
                                 on (Event.down' MouseButtons.Right) (constF ClosePolygon)
                               ] 
                      ] |> colored (Mod.constant C4b.Gray)
                for p in m.mfinished :> aset<_> do yield viewPolygon p
                let! working = m.mworking
                match working with
                    | Some v when v.cursor.IsSome -> 
                        yield 
                            [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] 
                                |> colored (Mod.constant C4b.Red)
                                |> transform' (Mod.constant <| Trafo3d.Translation(v.cursor.Value))
                        yield viewPolygon (v.cursor.Value :: v.finishedPoints)
                    | _ -> ()
            }
        agroup  t

    let initial = { finished = PSet.empty; working = None; _id = null }

    let app =
        {
            initial = initial
            update = update
            view = view
            ofPickMsg = fun _ _ -> []
        }

module TestApp =

    open Fablish
    open Fable.Helpers.Virtualdom
    open Fable.Helpers.Virtualdom.Html

    open SharedModel

    type Model = SharedModel.Ui

    type Action = Inc | Dec | Reset | SetInfo of string

    let update e (m : Model) (a : Action) =
        match a with
            | Inc -> { m with cnt = m.cnt + 1 }
            | Dec -> { m with cnt = m.cnt - 1 }
            | Reset -> { m with cnt = 0 }
            | SetInfo info -> { m with info = info}

    let view (m : Model) : DomNode<Action> =
        div [] [
            div [Style ["width", "100%"; "height", "100%"; "background-color", "transparent"]; attribute "id" "renderControl"] [
                text (sprintf "current content: %d" m.cnt)
                br []
                button [onMouseClick (fun dontCare -> Inc); attribute "class" "ui button"] [text "increment"]
                button [onMouseClick (fun dontCare -> Dec)] [text "decrement"]
                button [onMouseClick (fun dontCare -> Reset)] [text "reset"]
                br []
                text (sprintf "ray: %s" m.info)
            ]
        ]

    let initial = { info = "not known"; cnt = 0; _id = null }

    let app =
        {
            initial = initial
            update = update 
            view = view
            subscriptions = Subscriptions.none
            onRendered = OnRendered.ignore
        }


//module PlaceTransformObjects =
//
//    open ImmutableSceneGraph
//    open Elmish3D
//    open Primitives
//
//    open TranslateController
//    open PlaceTransformObjects
//
//
//    let initial =
//        {
//            objects = [ Trafo3d.Translation V3d.OOO; Trafo3d.Translation V3d.IOO; Trafo3d.Translation V3d.OIO ]
//            hoveredObj = None
//            selectedObj = None
//            _id = null
//        }
//
//    type Action =
//        | PlaceObject of V3d
//        | SelectObject of int
//        | HoverObject  of int
//        | Unselect
//        | TransformObject of int * TranslateController.Action
//
//    let update (m : Model) (msg : Action) =
//        match msg with
//            | PlaceObject p -> { m with objects = (Trafo3d.Translation p) :: m.objects }
//            | SelectObject i -> { m with selectedObj = Some (i, { TranslateController.app.initial with trafo = List.item i m.objects }) }
//            | TransformObject(index,translation) ->
//                match m.selectedObj with
//                    | Some (i,tmodel) ->
//                        let t = TranslateController.update tmodel translation
//                        { m with 
//                            selectedObj = Some (i,t)
//                            objects = List.updateAt i (constF t.trafo) m.objects }
//                    | _ -> m
//            | HoverObject i -> { m with hoveredObj = Some i }
//            | Unselect -> { m with selectedObj = None }
//
//    let isSelected m i =
//        match m.selectedObj with
//            | Some (s,_) when s = i -> true
//            | _ -> false
//
//    let isHovered m i =
//        match m.hoveredObj with
//            | Some s when s = i -> true
//            | _ -> false
//
//    let viewObjects (m : Model) =
//        m.objects |> List.mapi (fun i o -> 
//            Sphere3d(V3d.OOO,0.1) 
//               |> Sphere 
//               |> render [
//                    yield on Event.move (constF (HoverObject i))
//                    yield on (Event.down' MouseButtons.Middle) (constF Unselect)
//                    if m.selectedObj.IsNone then 
//                        yield on Event.down (constF (SelectObject i))
//                   ]
//               |> transformed' o 
//               |> colored' (if isSelected m i then C4b.Red elif isHovered m i then C4b.Blue else C4b.Gray)
//        )
//
//    let view (m : Model) =
//        [
//            yield! viewObjects m
//            yield 
//                Quad (Quad3d [| V3d(-1,-1,0); V3d(1,-1,0); V3d(1,1,0); V3d(-1,1,0) |]) 
//                 |> render [ 
//                        on (Event.down' MouseButtons.Right) PlaceObject 
//                    ] 
//                 |> colored' C4b.Gray
//            match m.selectedObj with
//                | None -> ()
//                | Some (i,inner) -> 
//                    yield TranslateController.view inner |> Scene.map (fun a -> TransformObject(i,a))
//        ] |> group
//
//    let app =
//        {
//            initial = initial
//            update = update
//            view = view
//            ofPickMsg = 
//                fun m (NoPick(me,r)) -> 
//                    match m.selectedObj with
//                        | None -> []
//                        | Some (i,inner) -> 
//                            match me with
//                                | MouseEvent.Click MouseButtons.Middle -> [Unselect]
//                                | _ -> TranslateController.ofPickMsg inner (NoPick(me,r)) |> List.map (fun a -> TransformObject(i,a))
//        }