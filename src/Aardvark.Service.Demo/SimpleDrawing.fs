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
    
    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

open TodoAddCore

let update (m : Model) (msg : Message) =
    match msg with
        | ClosePolygon -> 
            match m.workingPolygon with
                | None -> m
                | Some p -> { m with finishedPolygons = PList.append p m.finishedPolygons; workingPolygon = Some { points = [] } }
        | AddPoint pt -> 
            match m.workingPolygon with
                | None -> m
                | Some p -> { m with workingPolygon = Some { points = pt :: p.points} }


let view (m : MModel) =

    let line (f:V2d) (t:V2d) attributes = 
        Svg.line <| attributes @ [
            "x1" ==> sprintf "%f" f.X; "y1" ==> sprintf "%f" f.Y; 
            "x2" ==> sprintf "%f" t.X; "y2" ==> sprintf "%f" t.Y;
        ]

    let pairs xs = 
        match xs with
            | x::[] -> [(x,x)]
            | xs -> List.pairwise xs

    let viewPolygon (polygon : MPolygon) =
        alist {
            let! points = polygon.points
            for (p0,p1) in points |> List.pairwise do
                yield line p0 p1 [style "stroke:rgb(0,0,0);stroke-width:1"]
        }
    
    let svg =
        let attributes = 
            AttributeMap.ofList [
                attribute "width" "800" 
                attribute "height" "600" 
                onMouseDownRel AddPoint
                onMouseRightDownRel (fun _ -> ClosePolygon)
                clazz "svgRoot"
            ]
        Incremental.Svg.svg attributes <| 
            alist {
                for polygon in m.finishedPolygons do
                    yield! viewPolygon polygon

                let! currentPolygon = m.workingPolygon
                match currentPolygon with
                    | None -> ()
                    | Some p -> yield! viewPolygon p
            }
    body [] [svg]

let threads (m : Model) = 
    ThreadPool.empty

let app =
    {
        unpersist = Unpersist.instance
        threads = threads
        initial = { finishedPolygons = PList.empty; workingPolygon = Some { points = [] } }
        update = update
        view = view
    }

let start() = App.start app