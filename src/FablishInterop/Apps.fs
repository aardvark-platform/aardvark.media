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

    let update e (m : Model) (cmd : Action) =
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
                                 on Mouse.move MoveCursor
                                 on (Mouse.down' MouseButtons.Left)  AddPoint 
                                 on (Mouse.down' MouseButtons.Right) (constF ClosePolygon)
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


module TranslateController =

    open AnotherSceneGraph
    open Elmish3DADaptive

    open Scratch.DomainTypes
    open Primitives

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Axis =
        let dir = function | X -> V3d.XAxis | Y -> V3d.YAxis | Z -> V3d.ZAxis
        let moveAxis (trafo : Trafo3d) = function
            | X -> Plane3d(trafo.Forward.TransformDir V3d.OOI, trafo.Forward.TransformPos V3d.OOO)
            | Y -> Plane3d(trafo.Forward.TransformDir V3d.OOI, trafo.Forward.TransformPos V3d.OOO)
            | Z -> Plane3d(trafo.Forward.TransformDir V3d.OIO, trafo.Forward.TransformPos V3d.OOO)

    type Action = 

        // hover overs
        | Hover           of Axis * V3d
        | NoHit       
        | MoveRay         of Ray3d

        // translations    
        | Translate       of Axis * V3d
        | EndTranslation 

        | ResetTrafo

    open TranslateController

    let hasEnded a =
        match a with
            | EndTranslation -> true
            | _ -> false

    let hover      = curry Hover
    let translate_ = curry Translate

    let initalModel = { hovered = None; activeTranslation = None; trafo = Trafo3d.Identity; _id = null }

    let initial =  { 
            scene = initalModel
            camera = Camera.create ( CameraView.lookAt (V3d.III*3.0) V3d.OOO V3d.OOI ) (Frustum.perspective 60.0 0.1 10.0 1.0)
            _id = null
        }


    let updateModel (m : TModel) (a : Action) =
        match a, m.activeTranslation with
            | NoHit, _             ->  { m with hovered = None; }
            | Hover (v,_), _       ->  { m with hovered = Some v}
            | Translate (dir,s), _ -> { m with activeTranslation = Some (Axis.moveAxis m.trafo dir, m.trafo.Backward.TransformPos s) }
            | EndTranslation, _    -> { m with activeTranslation = None;  }
            | MoveRay r, Some (t,start) -> 
                let mutable ha = RayHit3d.MaxRange
                if r.HitsPlane(t,0.0,Double.MaxValue,&ha) then
                    let v = (ha.Point - start).XOO
                    { m with trafo = Trafo3d.Translation (ha.Point - start) }
                else m
            | MoveRay r, None -> m
            | ResetTrafo, _ -> { m with trafo = Trafo3d.Identity }

    let update e (m : Scene) (a : Action) =
        let scene = updateModel m.scene a
        { m with scene = scene }

    let viewModel (m : MTModel) =
        let arrow dir = Cone(V3d.OOO,dir,0.3,0.1)

        let ifHit (a : Axis) (selection : C4b) (defaultColor : C4b) =
            adaptive {
                let! hovered = m.mhovered
                match hovered with
                    | Some v when v = a -> return selection
                    | _ -> return defaultColor
            }
            
        transform m.mtrafo [
                translate 1.0 0.0 0.0 [
                    [ arrow V3d.IOO |> render [on Mouse.move (hover X); on Mouse.down (translate_ X)] ] 
                        |> colored (ifHit X C4b.White C4b.DarkRed)
                ]
                translate 0.0 1.0 0.0 [
                    [ arrow V3d.OIO |> render [on Mouse.move (hover Y); on Mouse.down (translate_ Y)] ] 
                        |> colored (ifHit Y C4b.White C4b.DarkBlue)
                ]
                translate 0.0 0.0 1.0 [
                    [ arrow V3d.OOI |> render [on Mouse.move (hover Z); on Mouse.down (translate_ Z)] ] 
                        |> colored (ifHit Z C4b.White C4b.DarkGreen)
                ]

                [ cylinder V3d.OOO V3d.IOO 1.0 0.05 |> render [ on Mouse.move (hover X); on Mouse.down (translate_ X) ] ] |> colored (ifHit X C4b.White C4b.DarkRed)
                [ cylinder V3d.OOO V3d.OIO 1.0 0.05 |> render [ on Mouse.move (hover Y); on Mouse.down (translate_ Y) ] ] |> colored (ifHit Y C4b.White C4b.DarkBlue)
                [ cylinder V3d.OOO V3d.OOI 1.0 0.05 |> render [ on Mouse.move (hover Z); on Mouse.down (translate_ Z) ] ] |> colored (ifHit Z C4b.White C4b.DarkGreen)
                
                translate 0.0 0.0 0.0 [
                    [ Sphere3d(V3d.OOO,0.1) |> Sphere |> render Pick.ignore ] |> colored (Mod.constant C4b.Gray)
                ]
        ]

    let viewScene cam s =   
        viewModel s.mscene 
            |> camera cam
            |> effect [toEffect DefaultSurfaces.trafo; toEffect DefaultSurfaces.vertexColor; toEffect DefaultSurfaces.simpleLighting]

    let ofPickMsgModel (model : TModel) (pick : GlobalPick) =
        match pick.mouseEvent with   
            | MouseEvent.Click _ | MouseEvent.Down _  -> []
            | MouseEvent.Move when Option.isNone model.activeTranslation ->
                    [NoHit; MoveRay pick.ray]
            | MouseEvent.Move ->  [MoveRay pick.ray]
            | MouseEvent.Up _   -> [EndTranslation]

    let ofPickMsg (model : Scene) noPick =
        ofPickMsgModel model.scene noPick

    let app (camera : IMod<Camera>) = {
        initial = initial
        update = update
        view = viewScene camera
        ofPickMsg = ofPickMsg
    }

module PlaceTransformObjects =

    open AnotherSceneGraph
    open Elmish3DADaptive
    open Primitives

    open TranslateController
    open PlaceTransformObjects

    (*
    issues:
    * domaintypes not working for tuples, not for namespaces
    * render neeeds adaptive variant
    * pset find
    * pset updateAt
    * mod IDs in actions?
    *)
    
    module PSet =
        let findByUpdate f g pset =
            pset 
                |> PSet.toList 
                |> List.map (fun v -> 
                        if f v then g v else v
                    )
                |> PSet.ofList 

        let findBy f pset =
            pset |> PSet.toList |> List.find f

        let pick f pset =
            pset |> PSet.toList |> List.pick f 

    let initial =
        {
            objects = PSet.ofList [ { id = 0; t = Trafo3d.Translation V3d.OOO; _id = null } ]
            hoveredObj = None
            selectedObj = None
            _id = null
        }

    type Action =
        | PlaceObject of V3d
        | SelectObject of int
        | HoverObject  of int
        | Unselect
        | Unhover
        | TransformObject of int * TranslateController.Action

    let update e (m : Model) (msg : Action) =
        match msg with
            | PlaceObject p -> { m with objects = PSet.add { id = PSet.count m.objects; t = Trafo3d.Translation p; _id = null }  m.objects }
            | SelectObject i -> 
                { m with selectedObj = Some { _id = null; id = i; tmodel = { TranslateController.initalModel with trafo = PSet.pick (fun a -> if a.id = i then Some a.t else None) m.objects }} }
            | TransformObject(index,translation) ->
                match m.selectedObj with
                    | Some old ->
                        let t = TranslateController.updateModel old.tmodel translation
                        { m with 
                            selectedObj = Some { old with tmodel = t }
                            objects = PSet.findByUpdate (fun a -> a.id = old.id) (fun a -> { a with t = t.trafo }) m.objects }
                    | _ -> m
            | HoverObject i -> { m with hoveredObj = Some i }
            | Unhover -> { m with hoveredObj = None }
            | Unselect -> 
                { m with selectedObj = None }

    let isSelected (m : Option<int>) i =
        match m with
            | Some m when m = i -> true
            | _ -> false

    let isHovered m i =
        match m with
            | Some s when s = i -> true
            | _ -> false

    let viewObjects (m : MModel) : aset<ISg<Action>> =
        aset {
            for o in m.mobjects :> aset<_> do
                let! i = o.mid
                let! selected = m.mselectedObj
                let id = 
                    m.mselectedObj |> Mod.bind (fun a -> 
                        match a with 
                            | None -> Mod.constant None
                            | Some v -> (v.mid :> IMod<_> |> Mod.map Some)
                    )
                let color = Mod.map2 (fun s h -> if isSelected s i then C4b.Red elif isHovered h i then C4b.Blue else C4b.Gray) id m.mhoveredObj
                yield Sphere3d(V3d.OOO,0.1) 
                       |> Sphere 
                       |> render [
                            yield on (Mouse.down' MouseButtons.Right) (constF Unselect)
                            if selected.IsNone then 
                                yield on Mouse.move (fun _ -> HoverObject i)
                                yield on (Mouse.down' MouseButtons.Left) (fun _ -> SelectObject i)
                           ]
                       |> transform' o.mt
                       |> colored' color
        }


    let view (m : MModel) =
        aset {
            yield! viewObjects m
            yield 
                Quad (Quad3d [| V3d(-1,-1,0); V3d(1,-1,0); V3d(1,1,0); V3d(-1,1,0) |]) 
                 |> render [ 
                        on (Mouse.down' MouseButtons.Middle) PlaceObject 
                    ] 
                 |> colored' (Mod.constant C4b.Gray)
            let! selected = m.mselectedObj
            match selected with
                | None -> ()
                | Some s -> 
                    yield TranslateController.viewModel s.mtmodel |> Scene.map (fun a -> TransformObject(s.mid.Value,a))
        } |> agroup

    let ofPickMsgModel (m : Model) (pick : GlobalPick) =
        [
            match m.selectedObj with
                | None -> 
                    yield Unhover
                | Some o -> 
                    match pick.mouseEvent with
                        | MouseEvent.Click MouseButtons.Right -> 
                            yield Unselect
                        | _ ->
                            yield! TranslateController.ofPickMsgModel o.tmodel pick |> List.map (fun a -> TransformObject(o.id,a))
        ]

    let app =
        {
            initial = initial
            update = update
            view = view
            ofPickMsg = ofPickMsgModel 
        }