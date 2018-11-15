module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model


let rec time() =
    proclist {
        do! Proc.Sleep 10
        yield Inc
        yield! time()
    }

let sw = System.Diagnostics.Stopwatch.StartNew()

let update (model : Model) (msg : Message) =
    match msg with
        | Inc -> 
            let s = sprintf "%d" (model.value + 1)
            { model with value = model.value + 1; updateStart = sw.Elapsed.TotalSeconds; things = model.things |> PList.map (fun _ -> s)  }
        | Go -> { model with threads = ThreadPool.add "anim" (time()) ThreadPool.empty;  }
        | Done -> { model with took = sw.Elapsed.TotalSeconds - model.updateStart }

let dependencies = 
    [
        { url = "animation.css"; name = "animation"; kind = Stylesheet }
        { url = "support.js"; name = "support"; kind = Script }
    ]

let illegalString = """
abc 
new line then \²³²]³\nsuper2929
k' " , . / \ ; : & % $ # @ *
"""

let view (model : MModel) =
    div [] [
        br []
        text illegalString
        br []
        text (sprintf "%A" (Some dependencies))
        br []
        onBoot "doIt(__ID__)" (div [clazz "jsUpdater"] [text "nothing"])
        text "Hello World"
        br []
        button [onClick (fun _ -> Inc)] [text "Increment"]
        text "    "
        Incremental.text (model.value |> Mod.map string)
        br []
        text "last update took: "
        br []
        Incremental.text (model.took |> Mod.map string)
        br []
        require dependencies ( div [clazz "rotate-center"; style "width:50px;height:50px;background-color:red"] [text "abc"] )
        //Incremental.div AttributeMap.empty <|
        //    alist {
        //        let! a = model.value
        //        for i in 0 .. 10 do
        //            yield button [] [text "ADF"]//text (sprintf "%d" a)]
        //        yield onBoot "aardvark.processEvent('__ID__', 'finished');" (
        //            button [onEvent "finished" [] (fun _ -> Done)] [text "urdar"]
        //        )
        //    }
        Incremental.div AttributeMap.empty (model.things |> AList.map (fun t -> button [] [text t]))
    ]



let threads (model : Model) = 
    model.threads


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {
                threads = ThreadPool.empty
                value = 0
                took = 0.0
                updateStart = 0.0
                things = PList.ofList [for i in 1 .. 10 do yield sprintf "%d" i]
            }
        update = update 
        view = view
    }
