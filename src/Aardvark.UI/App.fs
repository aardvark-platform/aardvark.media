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

    type private Message<'msg> = { msgs : seq<'msg>; processed : Option<System.Threading.ManualResetEventSlim> }

    let start (app : App<'model, 'mmodel, 'msg>) =
        let l = obj()
        let initial = app.initial
        let state = Mod.init initial
        let mstate = app.unpersist.create initial
        let initialThreads = app.threads initial
        let node = app.view mstate

        let mutable running = true
        let messageQueue = List<Message<'msg>>(128)

        let mutable currentThreads = ThreadPool.empty

        let update (source : Guid) (msgs : seq<'msg>) =
            //use mri = new System.Threading.ManualResetEventSlim()
            lock messageQueue (fun () ->
                messageQueue.Add { msgs = msgs; processed = None }
                Monitor.Pulse messageQueue
            )
          //  mri.Wait()

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
                    | None, None -> 
                        None
            
            currentThreads <- ThreadPool<'msg>(HMap.choose2 merge currentThreads.store newThreads.store)


        and doit(msgs : list<Message<'msg>>) =
            lock l (fun () ->
                if Config.shouldTimeUnpersistCalls then Log.startTimed "[Aardvark.UI] update/adjustThreads/unpersist"
                for msg in msgs do
                    for msg in msg.msgs do
                        let newState = app.update state.Value msg
                        let newThreads = app.threads newState
                        adjustThreads newThreads
                        transact (fun () ->
                            state.Value <- newState
                            app.unpersist.update mstate newState
                        )
                    // if somebody awaits message processing, trigger it
                    msg.processed |> Option.iter (fun mri -> mri.Set())
                if Config.shouldTimeUnpersistCalls then Log.stop ()
            )

        and emit (msg : 'msg) =
            lock messageQueue (fun () ->
                messageQueue.Add { msgs = Seq.singleton msg; processed = None }
                Monitor.Pulse messageQueue
            )


        // start initial threads
        adjustThreads initialThreads

        let updateThread =
            let update () = 
                while running do
                    Monitor.Enter(messageQueue)
                    while running && messageQueue.Count = 0 do
                        Monitor.Wait(messageQueue) |> ignore
                    
                    let messages = messageQueue |> CSharpList.toList
                    messageQueue.Clear()                 
                    Monitor.Exit(messageQueue)

                    doit messages
            Thread(ThreadStart update)

        updateThread.Name <- "[Aardvark.Media.App] updateThread"
        updateThread.IsBackground <- true
        updateThread.Start()


        {
            lock = l
            model = state
            ui = node
            update = update
        }

    let toWebPart (runtime : IRuntime) (app : App<'model, 'mmodel, 'msg>) =
        app |> start |> MutableApp.toWebPart runtime



