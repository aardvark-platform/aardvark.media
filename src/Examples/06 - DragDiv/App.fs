module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Model

let setPosition name objects newPos =
    let f = function | Some o -> { o with position = newPos } | None -> failwith ""
    HMap.update name f objects

let update (model : Model) (msg : Message) =
    match msg with
        | StartDrag(name,pos) -> 
            printfn "start drag: %s" name
            { model with dragObject = Some { name = name; startOffset = pos  }}
        | Move location  -> 
            match model.dragObject with
                | None -> model
                | Some d -> { model with objects = setPosition d.name model.objects (location + d.startOffset) }
        | StopDrag location  -> 
            match model.dragObject with
                | None -> model
                | Some d -> { model with objects = setPosition d.name model.objects (location + d.startOffset); dragObject = None }

let onMouseMoveRel (cb : V2d -> 'msg) =
    onEvent "onmousemove" [sprintf " { X: (getRelativeCoords(event,'container')).x.toFixed(), Y: (getRelativeCoords(event,'container')).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)

let onMouseUpRel (cb : V2d -> 'msg) =
    onEvent "onmouseup" [sprintf " { X: (getRelativeCoords(event,'container')).x.toFixed(), Y: (getRelativeCoords(event,'container')).y.toFixed()  }"] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)

let onMouseDownRel (container : string) (cb : V2d -> 'msg) =
    onEvent "onmouseup" [sprintf " { X: (getRelativeCoords(event,'%s')).x.toFixed(), Y: (getRelativeCoords(event,'%s')).y.toFixed()  }" container container] (List.head >> (fun a -> Pickler.json.UnPickleOfString a) >> cb)


let view (model : MModel) =

    let baseStyle = "width: 50px; height: 50px;border:1px solid black;background: green;position:absolute;"
    let objects =
        aset {
            for (name,o) in model.objects |> AMap.toASet do
                let attributes =
                    AttributeMap.ofAMap <|
                        amap {
                            //yield onEvent "ondragstart" []  (fun _ -> StartDrag name)
                            yield clazz name
                            yield onMouseDownRel name (fun l -> StartDrag(name,l))
                            let! pos = o.position
                            yield sprintf "%s;left:%fpx;top:%fpx;" baseStyle pos.X pos.Y |> style
                        } 
                yield Incremental.div attributes AList.empty
        }

    body [] [
        Incremental.div (
            AttributeMap.ofList [
                style "width: 300px; height: 200px;border:1px solid black;position:relative";
                onMouseMoveRel Move
                onMouseUpRel StopDrag
                clazz "container"
            ]
        ) (ASet.toAList objects)
    ]

let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               objects = 
                    HMap.ofList [ 
                        "object 1", { position = V2d(50,20) }
                        "object 2", { position = V2d(50,100) }
                    ]
               dragObject = None
            }
        update = update 
        view = view
    }
