module App

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Model

// port from: http://bl.ocks.org/nnattawat/8916402

let randomData =
    let rnd = RandomSystem()
    let normal = RandomGaussian(rnd)
    fun (count: int) -> List.init count (fun _ -> normal.GetDouble(20.0, 5.0))

let update (model : Model) (msg : Message) =
    match msg with
    | Generate ->  { model with data = randomData model.count }
    | ChangeCount n -> { model with count = n }
    | ChangeColor a -> { model with color = a }

let view (model : AdaptiveModel) =

    let dependencies = 
        [ 
            { kind = Script; name = "d3"; url = "resources/d3.v7.min.js" }
            { kind = Stylesheet; name = "histogramStyle"; url = "resources/Histogram.css" }
            { kind = Script; name = "histogramScript"; url = "resources/Histogram.js" }
        ]

    let channels =
        let color = model.color |> AVal.map (fun c -> $"#{c.RGB.ToHexString()}")
        let data  = model.data  |> AVal.map (fun data -> {| values = data; color = AVal.force color |})

        [ "data",  data.Channel
          "color", color.Channel ]

    let bootJs =
        String.concat "" [
            "data.onmessage = function (data) { refresh(data.values, data.color); };"
            "color.onmessage = setColor;"
        ]

    body [] [
        require dependencies (
            div [] [
                onBoot' channels bootJs (
                    div [] [
                        simplenumeric {
                            attributes [style "margin: 5px"]
                            update ChangeCount
                            value model.count
                            min 100
                            max 5000
                            step 100
                            largeStep 1000
                        }
                        button [
                            clazz "ui small button"
                            style "margin: 5px"
                            onClick (fun _ -> Generate)
                        ] [text "Generate"]
                    ]
                )

                div [style "margin-left: 5px"] [
                    ColorPicker.view ColorPicker.Config.Default ChangeColor model.color
                ]
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
               data = randomData 1000
               count = 1000
               color = C4b.SteelBlue
            }
        update = update 
        view = view
    }
