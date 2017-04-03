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
        threads     : 'model -> ThreadPool<'msg>
        update      : 'model -> 'msg -> 'model
        view        : 'mmodel -> DomNode<'msg>
    }

module App =
    let start (app : App<'model, 'mmodel, 'msg>) =
        let l = obj()
        let state = Mod.init app.initial
        let mstate = app.unpersist.create app.initial
        let node = app.view mstate

        let mutable running = true
        let messageQueue = List<'msg>(128)

        let mutable currentThreads = ThreadPool.create()

        let update (source : Guid) (msgs : list<'msg>) =
            lock messageQueue (fun () ->
                messageQueue.AddRange msgs
                Monitor.Pulse messageQueue
            )

        let rec adjustThreads (newThreads : ThreadPool<'msg>) =
            let merge (id : string) (oldThread : Option<Command<'msg>>) (newThread : Option<Command<'msg>>) : Option<Command<'msg>> =
                match oldThread, newThread with
                    | Some o, None ->
                        o.Stop()
                        newThread
                    | None, Some n -> 
                        n.Start(emit)
                        newThread
                    | Some o, Some n ->
                        oldThread
//                        if o <> n then
//                            o.Stop()
//                            n.Start(emit)

                    | None, None -> 
                        None
            
            currentThreads <- ThreadPool<'msg>(HMap.choose2 merge currentThreads.store newThreads.store)


        and doit(msgs : list<'msg>) =
            transact (fun () ->
                lock l (fun () ->
                    let newState = msgs |> List.fold app.update state.Value
                    let newThreads = app.threads newState
                    adjustThreads newThreads
                    state.Value <- newState
                    app.unpersist.update mstate newState
                )
            )

        and emit (msg : 'msg) =
            lock messageQueue (fun () ->
                messageQueue.Add msg
                Monitor.Pulse messageQueue
            )


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

                    doit messages
            }

        Async.Start updateThread


        {
            model = state
            ui = node
            update = update
        }

    let toWebPart (runtime : IRuntime) (app : App<'model, 'mmodel, 'msg>) =
        app |> start |> MutableApp.toWebPart runtime



