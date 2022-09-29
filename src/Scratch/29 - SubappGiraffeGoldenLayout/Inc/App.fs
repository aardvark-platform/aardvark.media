namespace Tmp.Inc

module App =
    open Aardvark.UI
    open Aardvark.UI.Primitives
    open FSharp.Data.Adaptive

    let update (model : Model) (msg : Message) =
        match msg with
            Inc -> { model with value = model.value + 1 }

    let view (model : AdaptiveModel) =
        div [] [
            text "Hello World"
            br []
            button [onClick (fun _ -> Tmp.Inc.Inc)] [text "Increment"]
            text "    "
            Incremental.text (model.value |> AVal.map string)
            br []
            img [
                attribute "src" "https://upload.wikimedia.org/wikipedia/commons/6/67/SanWild17.jpg"; 
                attribute "alt" "aardvark"
                style "width: 200px"
            ]
        ]

    let threads (model : Model) = 
        ThreadPool.empty

    let app id =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = 
                { 
                    value = 0
                    id    = id
                }
            update = update 
            view = view
        }




