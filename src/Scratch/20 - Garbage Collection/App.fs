namespace GarbageApp

open Aardvark.UI
open Aardvark.UI.Generic
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open System

module App = 
    let initial = 
        { 
            items = IndexList.ofList [ "foo"; "bar" ]
        }

    let rnd = new Random(0)

    let update (model : Model) (msg : Message) =
        match msg with
            | Update _->
                { model with items = IndexList.ofList (List.init 5 (fun _ -> rnd.Next().ToString() )) }

    let view (model : AdaptiveModel) =
        div [clazz "ui inverted segment"; style "width: 100%; height: 100%"] [

            Incremental.div (AttributeMap.ofList [clazz "button"]) (model.items |> AList.map (fun str ->
                                let garbage = Array.zeroCreate 10000000
                                div [ clazz "item"; onClick (fun _ -> Update(garbage))] str
                            ))

        ]

    let app : App<_,_,_> =
        {
            unpersist = Unpersist.instance     
            threads = fun _ -> ThreadPool.empty 
            initial = initial
            update = update 
            view = view
        }
