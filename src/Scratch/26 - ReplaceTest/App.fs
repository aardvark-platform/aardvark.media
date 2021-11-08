module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Inc.Model


//let update (model : Model) (msg : Message) =
//    match msg with
//        Inc l ->    Config.shouldPrintDOMUpdates <- false; { model with value = model.value + 1 }

//let view (model : AdaptiveModel) =
//    let things = List.init 80000 (fun i -> text (sprintf "%d" i)) |> IndexList.ofList
//    let guhu = AList.ofIndexList things
//    div [] [
//        button [onClick (fun _ -> Inc)] [text "doIt"]

//        Incremental.div AttributeMap.empty (
//            List.init 1000 id |> AList.ofList |> AList.mapA (fun _ ->
//                model.value |> AVal.map (fun v ->
//                    text (string v)
//                )
//            )
//        )

let update (model : Model) (msg : Message) =
    match msg with
       | Longer -> { model with elems = model.elems + 1 }
       | Inc l ->  
            //Config.shouldPrintDOMUpdates <- true; 
            { model with value = IndexList.update l (fun l -> l + 1000) model.value }


open FSharp.Data.Traceable

module AList =

    let truncate (count : aval<int>) (xs : alist<'a>) = 
        AList.ofReader (fun () -> 
            let reader = xs.GetReader()

            let mutable maxIndex = Index.zero

            { new AbstractReader<IndexList<'a>, IndexListDelta<'a>>(IndexList.trace) with
                override x.Compute(t : AdaptiveToken) = 
                    let mutable old = x.State
                    let count = count.GetValue(t)
                    let changes = reader.GetChanges(t)

                    if IndexList.isEmpty old then
                        if IndexListDelta.isEmpty changes then
                            IndexListDelta.empty
                        else
                            let trunc = 
                                changes 
                                |> IndexListDelta.toSeq 
                                |> Seq.filter (function (_, Set _) -> true | _ -> false)
                                |> Seq.truncate count 
                                |> IndexListDelta.ofSeq

                            maxIndex <-
                                trunc 
                                |> IndexListDelta.toSeq
                                |> Seq.last
                                |> fst

                            trunc
                    else
                        let mutable res = IndexListDelta.empty

                        for i, op in IndexListDelta.toSeq changes do
                            if i <= maxIndex then
                                res <- IndexListDelta.add i op res
                                match op with
                                | Set newValue ->
                                    old <- IndexList.set i newValue old
                                | Remove ->
                                    old <- IndexList.remove i old
                        
                        if old.Count < count then   
                            if reader.State.Count > old.Count then
                                let ne =  IndexList.skip old.Count reader.State |> IndexList.take (count - old.Count)
                                res <- IndexListDelta.combine res (IndexList.computeDelta IndexList.empty ne)
                                maxIndex <- ne.MaxIndex
                                res
                            else
                                res
                        elif old.Count > count then
                            let oe = old |> IndexList.skip count
                            res <- IndexListDelta.combine res (IndexList.computeDelta oe IndexList.empty)
                            maxIndex <- old.TryGetIndex (count - 1) |> Option.get
                            res

                        else
                            res
            }
        )

let view (model : AdaptiveModel) =
    div [] [

        let elsems = 
            model.value 
            |> AList.indexed
            |> AList.sortBy snd
            |> AList.truncate model.elems |> AList.map (fun (k, e) -> 
                button [onClick (fun _ -> Inc k)] [text (string e)]
            )

        div [] [
            button [onClick (fun _ -> Longer)] [text "mklonger"]
            Incremental.div AttributeMap.empty elsems
        ]

    ]


let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            {   
                value = IndexList.ofList [1..10000]
                elems = 10
            }
        update = update 
        view = view
    }
