module Simple2DDrawingApp

open Simple2DDrawing

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI
open Aardvark.UI.Primitives

module TodoAddCore =

    let onMouseDownRel (cb : V2d -> 'msg) =
        onEvent "onclick" [sprintf " { X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)
    let onMouseRightDownRel (cb : V2d -> 'msg) =
        onEvent "oncontextmenu" [sprintf "{ X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)
    let onMouseMoveRel (cb : V2d -> 'msg) =
        onEvent "onmousemove" [sprintf "{ X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)
    

    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

open TodoAddCore

let update (m : Model) (msg : Message) =
    match msg with
        | ClosePolygon _ -> 
            match m.workingPolygon with
                | None -> m
                | Some p -> { m with finishedPolygons = PList.append p m.finishedPolygons; workingPolygon = Some { points = [] }; past = Some m }
        | AddPoint pt -> 
            match m.workingPolygon with
                | None -> m
                | Some p -> { m with workingPolygon = Some { points = pt :: p.points}; past = Some m }
        | MoveCursor v -> { m with cursor = Some v } 
        | Undo _ -> 
            match m.past with
                | None -> m
                | Some p -> { p with future = Some m }
        | Redo _ -> 
            match m.future with
                | None -> m
                | Some f -> f


let view (m : MModel) =

    let line (f:V2d) (t:V2d) attributes = 
        Svg.line <| attributes @ [
            "x1" ==> sprintf "%f" f.X; "y1" ==> sprintf "%f" f.Y; 
            "x2" ==> sprintf "%f" t.X; "y2" ==> sprintf "%f" t.Y;
        ]

    let viewPolygon prepend points =
        alist {
            let! points = points
            let! prepend = prepend
            for (p0,p1) in (prepend @ points) |> List.pairwise do
                yield line p0 p1 [style "stroke:rgb(0,0,0);stroke-width:1"]
        }
    
    let svg =
        let attributes = 
            AttributeMap.ofList [
                attribute "width" "99%"; attribute "height" "600" 
                onMouseDownRel      AddPoint
                onMouseRightDownRel ClosePolygon
                onMouseMoveRel      MoveCursor
                clazz "svgRoot"; style "border: 1px solid black;"
            ]
        require [{ kind = Script; name = "utilities"; url = "utilities.js" }] (
            Incremental.Svg.svg attributes <| 
                alist {
                    for polygon in m.finishedPolygons do
                        yield! viewPolygon (Mod.constant []) polygon.points

                    let! currentPolygon = m.workingPolygon
                    match currentPolygon with
                        | None -> ()
                        | Some p -> yield! viewPolygon (Mod.map Option.toList m.cursor) p.points
                }
        )

    let prettyPrinted =
        let polygons = m.finishedPolygons |> AList.toMod
        let printPolygon (m : MPolygon) =
            sprintf "{ points = %A }" m.points
        adaptive {
            let! (finished : plist<MPolygon>) = polygons
            let! working = m.workingPolygon
            let! cursor = m.cursor
            let! past = m.past |> Mod.map Option.isSome
            let! future = m.future |> Mod.map Option.isSome
            let t = sprintf "{ finishedPolygons = %A; cursor = %A; workingPolygon = %A; past = %A (content omitted); future = %A (content omitted) }" (finished |> PList.toList |> List.map printPolygon) cursor (working |> Option.toList |> List.map printPolygon) past future
            return t.Replace("\n","\\")
        }

    body [] [
        button [onClick Undo] [text "Undo"]
        span [] []
        button [onClick Redo] [text "Redo"]
        br []
        br []
        svg
        br []; br []
        div [style "border: 1px solid black;width:99%"] [Incremental.text prettyPrinted]
        //textarea [style "border: 1px solid black;"] [Incremental.text prettyPrinted]
    ]

let threads (m : Model) = 
    ThreadPool.empty

let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial = { 
                    finishedPolygons = PList.empty; workingPolygon = Some { points = [] }; 
                    cursor = None; past = None; future = None 
                  }
        update = update
        view = view
    }

let start() = App.start app