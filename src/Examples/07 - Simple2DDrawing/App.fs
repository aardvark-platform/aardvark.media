// by convention, create a separate module for the app
module App

// open domain type namespace (including auto generated adaptive variants)
open Simple2DDrawing

open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.UI // defines app etc

// skip this part for the moment
module UtilitiesForThisDemoNowMergedToCore =

    // although onMouseDown (https://github.com/aardvark-platform/aardvark.media/blob/docs/src/Aardvark.UI/Attributes.fs#L118-118) provides
    // mouse coordinates, their space is not really usable for us since it is global page based.
    // getting svg realtive coordinates is not that easy but google is our friend so we find something like: https://stackoverflow.com/questions/10298658/mouse-position-inside-autoscaled-svg
    // ok so we need custom javascript to find the actual realtive coordinates.
    // in order to accomplish this, we define some event handlers, which do the magic.
    // so.. this is a prototypical example of extending aardvark.ui events by custom code. in this example the custom code lives in utilities.js.
    // we include this file as content=none and copytooutput. this way our suave seb server automatically finds the file (after we include it as 
    // js source file by using the require function in our view function).

    // TODO aardvark-team: write about how to deploy java script files.

    // add an event handler for onclick. If fired, produce a message, given the svg local coordinates.
    let onMouseDownRel (cb : V2d -> 'msg) =
        onEvent "onclick" [sprintf " { X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)

    // this goes on for other event types such as right mouse down etc... (remember, that there is no right mouse down in svg as we can quickly queck by using google).
    let onMouseRightDownRel (cb : V2d -> 'msg) =
        onEvent "oncontextmenu" [sprintf "{ X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)

    let onMouseMoveRel (cb : V2d -> 'msg) =
        onEvent "onmousemove" [sprintf "{ X: (getCursor(event)).x.toFixed(), Y: (getCursor(event)).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)
    
    // i often use this operator in order to write attribute maps more succiently. at the time of reading this will be also defined in Aardvark.UI.Operators
    let inline (==>) a b = Aardvark.UI.Attributes.attribute a b

// this module opens stuff we just defined but need later on.
open UtilitiesForThisDemoNowMergedToCore

// let us start with the update function of our elm functions
let update (m : Model) (msg : Message) =
    match msg with
        | ClosePolygon _ -> 
            match m.workingPolygon with
                | None -> m  // if we have no polygon, we cannot close one
                | Some p ->  // if we have a working polygon, add it to the list of finished polygon an start a fresh working polygon
                    { m with finishedPolygons = PList.append p m.finishedPolygons; workingPolygon = Some { points = [] }; past = Some m }
        | AddPoint pt -> 
            match m.workingPolygon with
                | None -> m // if we have no working polygon we cannot add this point (actually this should not happen, but total functions are nice and it does not hurt here)
                | Some p -> 
                    // update the working polygon by prepending a point to the lsit of points. furthermore the past is our old model
                    { m with workingPolygon = Some { points = pt :: p.points}; past = Some m }
        | MoveCursor v -> 
            // set the current cursor
            { m with cursor = Some v } 
        | Undo _ -> 
            match m.past with
                | None -> m // if we have no past our history is empty, so just return our current model
                | Some p -> 
                    // undo puts the current model into the future of the new model
                    { p with future = Some m }
        | Redo _ -> 
            match m.future with
                | None -> m
                | Some f -> f

// in aardvark.ui view functions get one argument of type MModel (instead of the plain immutable model)
// this allows for an efficient implementation since our MModel is an adaptive datastructure the gui and 
// 3d rendering can depend on.
let view (m : MModel) =

    // convinience function for creating svg line elements.
    // documentation is online: https://www.w3schools.com/graphics/svg_line.asp
    let line (f:V2d) (t:V2d) attributes = 
        Svg.line <| attributes @ [
            "x1" ==> sprintf "%f" f.X; "y1" ==> sprintf "%f" f.Y; 
            "x2" ==> sprintf "%f" t.X; "y2" ==> sprintf "%f" t.Y;
        ]

    // this function adaptively creates an alist of lines which connect our 
    // adaptive input point list (IMod<list<V2d>>)
    // in order to provide a preview for unfinished polygons, we optionally
    // pass in an additional IMod<list<V2d>> which is always prepended to the actual
    // point list. you will see why this is nice later on.
    let viewPolygon prepend points =
        alist {
            let! points = points // adpatively read the points
            let! prepend = prepend // adaptively read the prepend points (might be empty for ordinary polygons)
            for (p0,p1) in (prepend @ points) |> List.pairwise do // use List.pairwise in order to extract line segments
                yield line p0 p1 [style "stroke:rgb(0,0,0);stroke-width:1"] // emit a line svg element using our convinience function.
        }
    
    let svg =
        // read about svg elements here: https://www.w3schools.com/html/html5_svg.asp
        let attributes = 
            AttributeMap.ofList [
                attribute "width" "800"; attribute "height" "600" 

                // in order to work with svg's we need to attach event handles for various mouse inputs.
                // click adds a point, right click closes the polygon, mouse move updates the cursor
                // unfortunately vanilla aardvark.ui event handlers do not provide svg relative coordinates
                // (which cann quickly be checked by using a search engine).
                // thus we need special event handlers which do the trick. line 13 defines those ;)
                onMouseDownRel      AddPoint
                onMouseRightDownRel ClosePolygon
                onMouseMoveRel      MoveCursor

                // our event handlers require to search for the svg element to compute the coordinates realtive to.
                // currently we use hard coded class name 'svgRoot' in our javascript code. see: aardvark.js
                clazz "svgRoot"; 
                
                // show a border for our svg
                style "border: 1px solid black;"
            ]

        // finally create our svg. since our content is dynamic we use the incremental version of svg
        Incremental.Svg.svg attributes <| 
            alist {
                // loop over polygons and emit html code to render the svg
                for polygon in m.finishedPolygons do
                    yield! viewPolygon (Mod.constant []) polygon.points

                // let us check if we currently have a working polygon
                let! currentPolygon = m.workingPolygon
                // if so, emit the stuff
                match currentPolygon with
                    | None -> ()
                    | Some p -> 
                        // let us prepent our current cursor position in order to get a preview of the 
                        // last point.
                        yield! viewPolygon (Mod.map Option.toList m.cursor) p.points
            }

    // body creates a html body
    body [] [
        // add buttons and register undo/redo for those
        button [onClick Undo] [text "Undo"]
        span [] []
        button [onClick Redo] [text "Redo"]
        br []
        br []
        svg // here comes the actual svg
        br []
        text "usage: click adds points, right click closes the current polygon"
    ]

// so called thread pools are required to do animations and other coroutines such as asynchronously
// loading data. this is a topic for another day
let threads (m : Model) = 
    ThreadPool.empty

// finally provide an app instance.
let app =
    {
        unpersist = Unpersist.instance // this code automatically creates functions for updating daptive datastrucutres.
                                       // we do not care much about this here. however, if you get errors in this line, most likely
                                       // you are using the wrong MModel in your view function or your domain types do not compile
        threads = threads // not used here
        initial = // the initial model of our app after startup
            { 
               finishedPolygons = PList.empty; workingPolygon = Some { points = [] }; 
               cursor = None; past = None; future = None 
            }
        update = update // use our update and view function
        view = view
    }
