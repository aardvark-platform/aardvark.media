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
            { model with dragObject = Some { name = name; startOffset = pos.relativeToElement  }}
        | Move location  -> 
            match model.dragObject with
                | None -> model
                | Some d -> { model with objects = setPosition d.name model.objects (location - d.startOffset) }
        | StopDrag location  -> 
            match model.dragObject with
                | None -> model
                | Some d -> { model with objects = setPosition d.name model.objects (location - d.startOffset); dragObject = None }

let onMouseMoveRel (cb : V2d -> 'msg) : Attribute<'msg> =
    onEvent "onmousemove" [" toFixedV2d(relativeTo(event,'container'))"] (List.head >> Pickler.json.UnPickleOfString >> cb)

let onMouseUpRel (cb : V2d -> 'msg) =
    onEvent "onmouseup" [" toFixedV2d(relativeTo(event,'container'))"] (List.head >> Pickler.json.UnPickleOfString >> cb)

let onMouseDownRel (container : string) (self : string) (cb : RelativeClick -> 'msg) =
    let args =  [
        sprintf "toFixedV2d(relativeTo(event,'%s'))" container
        sprintf "toFixedV2d(relativeTo(event,'%s'))" self
    ]
    let reaction args =
        match args with
            | containerRelative::selfRelative::_ -> 
                { relativeToContainer = Pickler.json.UnPickleOfString containerRelative
                  relativeToElement = Pickler.json.UnPickleOfString selfRelative
                }
            | _ -> failwith ""
    onEvent "onmousedown" args (reaction >> cb)

let dependencies =
    [
        { kind = Script; url = "resources/DragUtilities.js"; name = "DragUtilities" }
    ]

let view (model : MModel) =

    let baseStyle = "width: 50px; height: 50px;border:1px solid black;background: green;position:absolute;"
    let objects =
        aset {
            for (name,o) in model.objects |> AMap.toASet do
                let attributes =
                    AttributeMap.ofAMap <|
                        amap {
                            yield clazz name
                            yield onMouseDownRel "container" name (fun l -> StartDrag(name,l))
                            let! pos = o.position
                            yield sprintf "%s;left:%fpx;top:%fpx;" baseStyle pos.X pos.Y |> style
                        } 
                yield Incremental.div attributes AList.empty
        }

    require dependencies (
        body [] [
            text "abc"
            br []
            br []
            Incremental.div (
                AttributeMap.ofList [
                    style "width: 600px; height: 400px;border:1px solid black;position:relative";
                    onMouseMoveRel Move
                    onMouseUpRel StopDrag
                    clazz "container"
                ]
            ) (ASet.toAList objects)
        ]
    )

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
                        "object1", { position = V2d(50,20) }
                        "object2", { position = V2d(50,100) }
                    ]
               dragObject = None
            }
        update = update 
        view = view
    }
