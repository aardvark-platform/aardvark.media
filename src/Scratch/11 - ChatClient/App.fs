namespace Chat
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open System

module App =
    open Aardvark.Base.Geometry

    type Message = 
        | SendMessage of string
        | Connect of Client
        | ChangeName of string * string
        | Disconnect of string
        | SetText of string * string
        | SetName of string * string

    let update (model : Model) (msg : Message) =
        match msg with
        | SendMessage id ->
            let name = model.clients.[id].name
            let m = model.currentMsg.[id]
            let message = "<"+name+"> "+m
            { model with lines = model.lines |> HMap.add DateTime.Now message }
        | SetText (id,m) ->
            { model with currentMsg = model.currentMsg |> HMap.add id m}
        | SetName (id,m) ->
            { model with clients = model.clients |> HMap.add id ({model.clients.[id] with name = m})}
        | Connect c ->
            { model with clients = model.clients |> HMap.add c.id c }
        | ChangeName (id,name) ->
            match model.clients |> HMap.tryFind id with
            | None -> 
                Log.warn "key not found: %A" id
                model
            | Some ci ->
                let c = { ci with name = name}
                { model with clients = model.clients |> HMap.add id c }
        | Disconnect (id) ->
            { model with clients = model.clients |> HMap.remove id }

    let view (model : MModel) = 
        
        onBoot ("window.top.clientid = Math.random().toString(36).substr(2, 9); aardvark.processEvent('__ID__', 'clientConnected', { id : window.top.clientid, name : 'aard'});") (
            div [onEvent "clientConnected" [] (List.head >> Pickler.json.UnPickleOfString >> Connect) ] [
                text "Hello World"
                Incremental.div (AttributeMap.empty) (alist {
                    yield div[][text ("connected:")]
                    for (_,c) in model.clients |> AMap.toASet |> ASet.sortBy fst do
                        yield div[][text ("id="+c.id+" name="+c.name)]
                })
                Incremental.div (AttributeMap.empty) (alist {
                    yield div[][text ("Messages:")]
                    for (date,line) in model.lines |> AMap.toASet |> ASet.sortBy fst do
                        yield div[][text (""+date.ToString()+" "+line)]
                })
                input [attribute "type" "text"; onEvent "onchange" ["{ id : window.top.clientid, name : event.target.value}"] (List.head >> Pickler.json.UnPickleOfString >> (fun (c : Client) -> SetText(c.id,c.name)))] 
                button [
                    onEvent "onclick" ["window.top.clientid"] (List.head >> (fun id -> SendMessage(id.Trim('\"'))))
                ] [
                    text "sendMessage"
                ]
                input [attribute "type" "text"; onEvent "onchange" ["{ id : window.top.clientid, name : event.target.value}"] (List.head >> Pickler.json.UnPickleOfString >> (fun (c : Client) -> SetName(c.id,c.name)))] 
            ]


        )


    let threads (model : Model) = 
        ThreadPool.empty


    let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
        {
            unpersist = Unpersist.instance     
            threads = threads 
            initial = Model.initial
            update = update 
            view = view
        }
