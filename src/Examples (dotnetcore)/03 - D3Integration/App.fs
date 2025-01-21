module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Model

// port from: http://bl.ocks.org/nnattawat/8916402

let rnd = RandomSystem()
let normal = RandomGaussian(rnd)

let update (model : Model) (msg : Message) =
    match msg with
        | Generate -> 
            { model with 
                data = 
                    Array.init (model.count.value |> round |> int) (fun _ -> normal.GetDouble(20.0,5.0)) |> Array.toList 
            }
        | ChangeCount n -> { model with count = Numeric.update model.count n }
        | ChangeColor a -> { model with color = a }

let view (model : AdaptiveModel) =

    let dependencies = 
        [ 
            { kind = Script; name = "d3"; url = "http://d3js.org/d3.v3.min.js" }
            { kind = Stylesheet; name = "histogramStyle"; url = "resources/Histogram.css" }
            { kind = Script; name = "histogramScript"; url = "resources/Histogram.js" }
        ]

    let dataChannel = model.data.Channel
    let updateChart =
        "data.onmessage = function (values) { if(values.length > 0) refresh(values); };"

    body [] [
        require dependencies (
            div [] [
                onBoot' ["data", dataChannel] updateChart (
                    div [] [
                        Numeric.view model.count |> UI.map ChangeCount
                        text "  "
                        button [onClick (fun _ -> Generate)] [text "Generate"]
                    ]
                )

                ColorPicker.view ColorPicker.Config.Default ChangeColor model.color
            ]
        )
    ]

let threads (model : Model) = 
    ThreadPool.empty


let app : App<_,_,_> =
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               data = []
               count =  { min = 100.0; max = 5000.0; value = 1000.0; step = 100.0; format = "{0:0}" }
               color = C4b.White
            }
        update = update 
        view = view
    }
