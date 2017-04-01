namespace Aardvark.UI

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental

type Unpersist<'model, 'mmodel> =
    {
        create : 'model -> 'mmodel
        update : 'mmodel -> 'model -> unit
    }

module Unpersist =
    let inline instance<'model, 'mmodel when 'mmodel : (static member Create : 'model -> 'mmodel) and 'mmodel : (member Update : 'model -> unit)> =
        {
            create = fun m -> (^mmodel : (static member Create : 'model -> 'mmodel) (m))
            update = fun mm m -> (^mmodel : (member Update : 'model -> unit) (mm, m))
        }

type App<'model, 'mmodel, 'msg> =
    {
        unpersist   : Unpersist<'model, 'mmodel>
        initial     : 'model
        update      : 'model -> 'msg -> 'model
        view        : 'mmodel -> DomNode<'msg>
    }

module App =
    let start (app : App<'model, 'mmodel, 'msg>) =
        let state = Mod.init app.initial
        let mstate = app.unpersist.create app.initial
        let node = app.view mstate

        let mutable running = true
        let messageQueue = List<'msg>(128)

        let updateThread =
            async {
                do! Async.SwitchToNewThread()
                while running do
                    Monitor.Enter(messageQueue)
                    while running && messageQueue.Count = 0 do
                        Monitor.Wait(messageQueue) |> ignore
                    
                    let messages = messageQueue |> CSharpList.toList
                    messageQueue.Clear()
                    Monitor.Exit(messageQueue)
                    let newState = messages |> List.fold app.update state.Value
                    transact (fun () ->
                        state.Value <- newState
                        app.unpersist.update mstate newState
                    )
            }

        Async.Start updateThread

        let update (source : Guid) (msgs : list<'msg>) =
            lock messageQueue (fun () ->
                messageQueue.AddRange msgs
                Monitor.Pulse messageQueue
            )

        {
            model = state
            ui = node
            update = update
        }

    let toWebPart (runtime : IRuntime) (app : App<'model, 'mmodel, 'msg>) =
        app |> start |> MutableApp.toWebPart runtime



