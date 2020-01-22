module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Model
open Aardvark.UI.Html
 
let (%) (v : V2d) (f : float) = V2d(f * float (int v.X / int f), f * float (int v.Y / int f))

let getPosition (model : Model) =
    match model.dragInfo, model.dragMode with
        | Some p, DragMode.Unrestricted -> p.absolutePosition % model.stepSize.value + model.pos 
        | Some p, DragMode.Horizontal   -> p.absolutePosition.XO % model.stepSize.value + model.pos 
        | Some p, DragMode.Vertical     -> p.absolutePosition.OY % model.stepSize.value + model.pos 
        | _  -> model.pos


let update (model : Model) (msg : Message) =
    match msg with
        | Drag dragInfo     -> { model with dragInfo = Some dragInfo }
        | StopDrag dragInfo -> { model with pos = getPosition model; dragInfo = None }
        | SetDragMode m -> { model with dragMode = m }
        | SetStepSize s -> { model with stepSize = Numeric.update model.stepSize s }

let dependencies = [ { url = "resources/SvgDragUtilities.js"; name = "SvgDragUtils"; kind = Script } ]

let onDrag  (cb : DragInfo -> 'msg) =
    onEvent "ondrag" [] (List.head >> Aardvark.UI.Pickler.unpickleOfJson >> cb)

let onEndDrag  (cb : DragInfo -> 'msg) =
    onEvent "onendrag" [] (List.head >> Aardvark.UI.Pickler.unpickleOfJson >> cb)

let (=>) n v = attribute n v

let view (model : MModel) =

    let position = model.Current |> AVal.map getPosition

    body [] [
        require dependencies (
            div [] [
                Svg.svg [clazz "mySvg"; style "width:600px;height:400px;stroke='blue';user-select: none;"] [
                    onBoot "draggable('mySvg',__ID__);" (
                        Incremental.Svg.circle ( 
                            amap {
                                yield attribute "r" "20"
                                yield onDrag    Drag
                                yield onEndDrag StopDrag
                                let! pos = position
                                yield attribute "cx" (sprintf "%f" pos.X)
                                yield attribute "cy" (sprintf "%f" pos.Y)
                            } |> AttributeMap.ofAMap
                        )
                    )
                ]
                br []
                SemUi.dropDown model.dragMode SetDragMode
                br []
                text "StepSize "
                Numeric.numericField (SetStepSize >> Seq.singleton) AttributeMap.empty model.stepSize Slider
            ]
        )
    ]

let threads (model : Model) = ThreadPool.empty

let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               dragInfo = None
               dragMode = DragMode.Unrestricted
               pos = V2d(100,100)
               stepSize = { min = 1.0; max = 40.0; value = 1.0; step = 1.0; format = "{0:0}" }
            }
        update = update 
        view = view
    }
