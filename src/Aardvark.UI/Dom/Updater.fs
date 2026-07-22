namespace Aardvark.UI

open System
open System.Text
open System.Threading
open System.Collections.Generic
open Aardvark.UI
open Aardvark.Base
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive

module internal Updaters =

    module private Id =
        let mutable private currentId = 0
        let newId() =
            let id = Interlocked.Increment(&currentId)
            $"n{id}"

    type ContraDict<'k, 'v> =
        abstract member Item : 'k -> 'v with set
        abstract member Remove : 'k -> bool

    module ContraDict =
        let ofDictionary (d : Dictionary<'k, 'v>) =
            { new ContraDict<'k, 'v> with
                member _.Item with set (k : 'k) (v : 'v) = d.[k] <- v
                member _.Remove k = d.Remove k
            }

        let map (f : 'k -> 'b -> 'v) (d : ContraDict<'k, 'v>) =
            { new ContraDict<'k, 'b> with
                member _.Item with set (k : 'k) (v : 'b) = d.[k] <- f k v
                member _.Remove k = d.Remove k
            }

    type SceneMessages<'msg> =
        {
            preRender  : RenderClientInfo -> seq<'msg>
            postRender : RenderClientInfo -> seq<'msg>
        }

    module SceneMessages =
        let map (mapping : 'a -> 'b) (msgs : SceneMessages<'a>) =
            {
                preRender = msgs.preRender >> Seq.map mapping
                postRender = msgs.postRender >> Seq.map mapping
            }
    type EventHandler<'msg> =
        {
            version : byte
            invoke  : Guid -> string -> string list -> 'msg seq
        }

    [<Struct>]
    type ChannelId =
        val ElementId   : string
        val ChannelName : string
        new (elementId, channelName) = { ElementId = elementId; ChannelName = channelName }

    module EventHandler =
        let map mapping (handler: EventHandler<'T1>) : EventHandler<'T2> =
            { version = handler.version; invoke = mapping handler.invoke }

    type EventHandlers<'msg>() =
        let store = Dictionary<ChannelId, {| current: EventHandler<'msg> voption; pending: Queue<EventHandler<'msg>> |}>()

        // Dequeues pending handlers until the one with the given version is found or the queue is empty
        static let rec tryGet (queue: Queue<EventHandler<'msg>>) version =
            if queue.Count > 0 then
                let handler = queue.Dequeue()
                if handler.version = version then
                    ValueSome handler
                else
                    tryGet queue version
            else
                ValueNone

        member _.Enqueue(key, handler) =
            lock store (fun _ ->
                let value = store.GetCreate(key, fun _ -> {| current = ValueNone; pending = Queue() |} )
                value.pending.Enqueue handler
            )

        member _.TryGet(key) =
            lock store (fun _ ->
                match store.TryGetValue key with
                | true, state -> state.current
                | _ -> ValueNone
            )

        member _.TryGet(key, version) =
            lock store (fun _ ->
                match store.TryGetValue key with
                | true, state ->
                    match state.current with
                    | ValueSome handler when handler.version = version -> state.current
                    | _ ->
                        let result = tryGet state.pending version
                        store.[key] <- {| state with current = result |} // Make the found handler active
                        result
                | _ ->
                    ValueNone
            )

        member this.TryGet(key, version) =
            match version with
            | ValueSome version -> this.TryGet(key, version)
            | _ -> this.TryGet key

        member _.Remove(key) =
            lock store (fun _ -> store.Remove key)

        interface ContraDict<ChannelId, EventHandler<'msg>> with
            member this.Item with set key value = this.Enqueue(key, value)
            member this.Remove key = this.Remove key

    type SceneInfo<'msg> =
        { scene    : Scene
          messages : SceneMessages<'msg> voption
          getState : RenderClientInfo -> RenderState }

    module SceneInfo =
        let map mapping (info: SceneInfo<'T1>) : SceneInfo<'T2> =
            { scene    = info.scene
              messages = info.messages |> ValueOption.map (SceneMessages.map mapping)
              getState = info.getState }

    [<Struct>]
    type ReferenceId =
        val Name : string
        val Kind : ReferenceKind
        new (name, kind) = { Name = name; Kind = kind }

    type UpdateState<'msg> =
        {
            scenes     : ContraDict<string, SceneInfo<'msg>>
            handlers   : ContraDict<ChannelId, EventHandler<'msg>>
            references : Dictionary<ReferenceId, Reference>          // Added references
            channels   : Dictionary<ChannelId, ChannelReader>        // Added or removed channels (value = null -> removed)
            messages   : IObservable<'msg>
        }

    type IUpdater =
        inherit IAdaptiveObject
        abstract member Id : string

    type IUpdater<'msg> =
        inherit IUpdater
        abstract member Update : AdaptiveToken * UpdateState<'msg> * ValueOption<JSExpr -> JSExpr> -> JSExpr
        abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr

    [<AbstractClass>]
    type AbstractUpdater<'msg>(node : DomNode<'msg>) =
        inherit AdaptiveObject()
        let mutable initial = true
        let id = lazy Id.newId()

        let setup (state : UpdateState<'msg>) =
            for r in node.Required do
                state.references.[ReferenceId(r.name, r.kind)] <- r

            match node.Boot with
            | ValueSome boot ->
                let id = if id.IsValueCreated then id.Value else "NOT AN ID"
                let code = boot id

                if Map.isEmpty node.Channels then
                    Raw code
                else
                    let sb = StringBuilder()

                    for KeyValue(name, c) in node.Channels do
                        state.channels.[ChannelId(id, name)] <- c.GetReader()
                        sb.Append $"var {name} = aardvark.getChannel(\"{id}\", \"{name}\");" |> ignore

                    sb.Append code |> ignore

                    Raw <| sb.ToString()

            | _ ->
                JSExpr.Nop

        let shutdown (state : UpdateState<'msg>) =
            if id.IsValueCreated then
                for KeyValue(name, _) in node.Channels do
                    state.channels.[ChannelId(id.Value, name)] <- Unchecked.defaultof<_>

            match node.Shutdown with
            | ValueSome shutdown ->
                if id.IsValueCreated then
                    Raw (shutdown id.Value)
                else
                    Raw (shutdown "NOT AN ID")
            | _ ->
                JSExpr.Nop

        abstract member CreateElement : ValueOption<struct {| tag: string; ns: string |}>
        default _.CreateElement = ValueNone

        abstract member PerformUpdate : AdaptiveToken * UpdateState<'msg> * JSExpr -> JSExpr
        abstract member PerformDestroy : UpdateState<'msg> * JSExpr -> JSExpr

        abstract member Id : string
        default this.Id =
            if this.CreateElement.IsSome then id.Value
            else null

        member this.Update(token : AdaptiveToken, state : UpdateState<'msg>, insert : ValueOption<JSExpr -> JSExpr>) =
            this.EvaluateIfNeeded token JSExpr.Nop (fun token ->
                match this.CreateElement with
                | ValueSome elem ->
                    if initial then
                        initial <- false
                        if elem.tag = "body" then
                            JSExpr.Sequential [
                                SetAttribute(JSExpr.Body, "id", id.Value)
                                this.PerformUpdate(token, state, JSExpr.Body)
                                setup state
                            ]
                        else
                            let var = id.Value
                            JSExpr.Let(var, JSExpr.CreateElement(elem.tag, elem.ns),
                                JSExpr.Sequential [
                                    (
                                        match insert with
                                        | ValueSome insert -> insert (JSExpr.Var var)
                                        | ValueNone -> failwith "[UI] should not be inserted"
                                    )
                                    SetAttribute(JSExpr.Var var, "id", id.Value)
                                    this.PerformUpdate(token, state, JSExpr.Var var)
                                    setup state
                                ]
                            )
                    else
                        let var = id.Value
                        JSExpr.Let(var, JSExpr.GetElementById(id.Value),
                            this.PerformUpdate(token, state, JSExpr.Var var)
                        )
                | _ ->
                    if initial then
                        initial <- false
                        JSExpr.Sequential [
                            setup state
                            this.PerformUpdate(token, state, JSExpr.Nop)
                        ]
                    else
                        this.PerformUpdate(token, state, JSExpr.Nop)
            )

        member this.Destroy(state, self) =
            lock this (fun () ->
                JSExpr.Sequential [
                    shutdown state
                    this.PerformDestroy(state, self)
                ]
            )

        interface IUpdater<'msg> with
            member x.Id = x.Id
            member x.Update(t,state, s) = x.Update(t,state, s)
            member x.Destroy(state, self) = x.Destroy(state, self)

    [<AbstractClass>]
    type WrappedUpdater<'msg>(node : DomNode<'msg>, id : string) =
        inherit AdaptiveObject()
        let mutable initial = true

        let setup (state : UpdateState<'msg>) =
            for r in node.Required do
                state.references.[ReferenceId(r.name, r.kind)] <- r

            match node.Boot with
            | ValueSome boot ->
                let code = boot id

                if Map.isEmpty node.Channels then
                    Raw code
                else
                    let sb = StringBuilder()

                    for KeyValue(name, c) in node.Channels do
                        state.channels.[ChannelId(id, name)] <- c.GetReader()
                        sb.Append $"var {name} = aardvark.getChannel(\"{id}\", \"{name}\");" |> ignore

                    sb.Append code |> ignore

                    Raw <| sb.ToString()

            | _ ->
                JSExpr.Nop

        let shutdown (state : UpdateState<'msg>) =
            for KeyValue(name, _) in node.Channels do
                state.channels.[ChannelId(id, name)] <- Unchecked.defaultof<_>

            match node.Shutdown with
            | ValueSome shutdown ->
                Raw <| shutdown id
            | _ ->
                JSExpr.Nop

        abstract member PerformUpdate : AdaptiveToken * UpdateState<'msg> * ValueOption<JSExpr -> JSExpr> -> JSExpr
        abstract member PerformDestroy : UpdateState<'msg> * JSExpr -> JSExpr

        member this.Id = id

        member this.Update(token : AdaptiveToken, state : UpdateState<'msg>, insert : ValueOption<JSExpr -> JSExpr>) =
            this.EvaluateIfNeeded token JSExpr.Nop (fun token ->
                if initial then
                    initial <- false
                    JSExpr.Sequential [
                        this.PerformUpdate(token, state, insert)
                        setup state
                    ]
                else
                    this.PerformUpdate(token, state, insert)
            )

        member x.Destroy(state, self) =
            lock x (fun () -> 
                JSExpr.Sequential [
                    shutdown state
                    x.PerformDestroy(state, self)
                ]
            )

        interface IUpdater<'msg> with
            member this.Id = this.Id
            member this.Update(t,state, s) = this.Update(t,state, s)
            member this.Destroy(state, self) = this.Destroy(state, self)

    type AttributeUpdater<'msg>(attributes : AttributeMap<'msg>) =
        let mutable reader = attributes.GetReader()
        let registeredHandlers = System.Collections.Generic.HashSet<ChannelId>()

        // We assign a version number to each event handler and each sent event.
        // Event handlers are not made active immediately but put in a 'pending' queue.
        // When an event is received by the server, the version of the event is compared against the version of active handler (if present).
        // If the comparsion fails, event handlers are dequeued until the correct handler is found.
        // This makes sure that the correct handler is dispatched for an incoming event, which is crucial in case
        // the parameters expected by the handlers change.
        // A common example of diverging parameters is switching between one and multiple event handlers (see Event.combine).
        let getVersion =
            let mutable next = 0uy

            fun () ->
                let version = next
                next <- next + 1uy
                version

        member _.Update(token : AdaptiveToken, state : UpdateState<'msg>, id : string, self : JSExpr) =
            JSExpr.Sequential [
                let atts = reader.GetChanges token
                for name, op in atts do
                    match op with
                    | Set value ->
                        match value with
                        | AttributeValue.RenderEvent _ -> ()
                        | AttributeValue.String str ->
                            yield JSExpr.SetAttribute(self, name, str)

                        | AttributeValue.Event evt ->
                            let key = ChannelId(id, name)
                            let version = getVersion()
                            state.handlers.[key] <- { version = version; invoke = evt.serverSide } // Enqueue handler
                            registeredHandlers.Add(key) |> ignore

                            let str = Event.toString' id name version evt
                            yield JSExpr.SetAttribute(self, name, str)
                            yield JSExpr.SetEventHandler(self, name, version) // Sends and empty event to set the event handler active

                    | Remove ->
                        let key = ChannelId(id, name)
                        registeredHandlers.Remove(key) |> ignore
                        state.handlers.Remove(key) |> ignore
                        yield JSExpr.RemoveAttribute(self, name)
            ]

        member x.Destroy(state : UpdateState<'msg>) =
            for k in registeredHandlers do
                state.handlers.Remove(k) |> ignore
            registeredHandlers.Clear()
            reader <- Unchecked.defaultof<_>

    type EmptyUpdater<'msg>(e : EmptyNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)

        override _.CreateElement = ValueNone

        override _.PerformUpdate(_, _, _) =
            JSExpr.Nop

        override _.PerformDestroy(_, _) =
            JSExpr.Nop

    and VoidUpdater<'msg>(e : VoidNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)

        let att = AttributeUpdater<_>(e.Attributes)
        override _.CreateElement = ValueSome {| tag = e.Tag; ns = e.Namespace |}

        override this.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            att.Update(token, state, this.Id, self)

        override _.PerformDestroy(state : UpdateState<'msg>, _) =
            att.Destroy(state)
            JSExpr.Nop

    and InnerUpdater<'msg>(e : InnerNode<'msg>, request : IHttpRequest) =
        inherit AbstractUpdater<'msg>(e)

        let mutable elemReader : IIndexListReader<IUpdater<'msg>> = (AList.map (fun n -> Updater.New(n, request)) e.Children).GetReader()
        let att = AttributeUpdater<_>(e.Attributes)

        static let cmpState =
            { new IComparer<Index * ref<IUpdater<'msg>>> with
                member x.Compare((l,_), (r,_)) =
                    compare l r
            }

        let content = SortedSetExt<Index * ref<IUpdater<'msg>>>(cmpState)

        let neighbours (i : Index) =
            let l, s, r = content.FindNeighbours((i, Unchecked.defaultof<_>))
            let l = if l.HasValue then ValueSome l.Value else ValueNone
            let r = if r.HasValue then ValueSome r.Value else ValueNone
            let s = if s.HasValue then ValueSome (snd s.Value) else ValueNone
            l, s, r

        override _.CreateElement = ValueSome {| tag = e.Tag; ns = e.Namespace |}

        override this.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            JSExpr.Sequential [
                let id = this.Id
                yield att.Update(token, state, id, self)

                let mutable toUpdate = elemReader.State
                let ops = elemReader.GetChanges token

                for i, op in IndexListDelta.toSeq ops do
                    match op with
                    | ElementOperation.Remove ->
                        let _, s, _ = neighbours i
                        toUpdate <- IndexList.remove i toUpdate
                        match s with
                        | ValueSome n ->
                            let n = n.Value
                            content.Remove(i, Unchecked.defaultof<_>) |> ignore

                            if notNull n.Id then
                                let self = n.Id
                                yield JSExpr.Let(self, GetElementById self,
                                    JSExpr.Sequential [
                                        n.Destroy(state, JSExpr.Var self)
                                        JSExpr.Remove (JSExpr.Var self)
                                    ]
                                )
                            else
                                yield n.Destroy(state, JSExpr.Nop)
                        | _ ->
                            failwith "[Media] UI Updater. trying to remove non existent objects (locking issue?)"

                    | ElementOperation.Set newElement ->
                        let l, s, r = neighbours i

                        let insert =
                            match l, r with
                            | _, ValueSome (_, r) when notNull r.Value.Id  ->
                                fun v -> JSExpr.InsertBefore(GetElementById r.Value.Id, v)

                            | ValueSome (_, l), _ when notNull l.Value.Id ->
                                fun v -> JSExpr.InsertAfter(GetElementById l.Value.Id, v)

                            | _ ->
                                fun v -> JSExpr.AppendChild(self, v)

                        match s with
                        | ValueSome ref ->
                            let oldElement = ref.Value
                            ref.Value <- newElement
                            toUpdate <- IndexList.remove i toUpdate

                            let oid = oldElement.Id
                            let nid = newElement.Id

                            yield
                                if notNull oid then
                                    if notNull nid then
                                        JSExpr.Let(oid,  GetElementById oid,
                                            JSExpr.Sequential [
                                                oldElement.Destroy(state, Var oid)
                                                newElement.Update(token, state, ValueSome (fun n -> JSExpr.Replace(Var oid, n)))
                                            ]
                                        )
                                    else
                                        JSExpr.Sequential [
                                            oldElement.Destroy(state, GetElementById oid)
                                            newElement.Update(token, state, ValueNone)
                                        ]
                                else
                                    if notNull nid then
                                        JSExpr.Sequential [
                                            oldElement.Destroy(state, JSExpr.Nop)
                                            newElement.Update(token, state, ValueSome insert)
                                        ]
                                    else
                                        JSExpr.Sequential [
                                            oldElement.Destroy(state, JSExpr.Nop)
                                            newElement.Update(token, state, ValueNone)
                                        ]

                        | _ ->
                            content.Add(i, ref newElement) |> ignore
                            yield newElement.Update(token, state, ValueSome insert)

                for u in toUpdate do
                    yield u.Update(token, state, ValueNone)

            ]

        override _.PerformDestroy(state : UpdateState<'msg>, _ : JSExpr) =
            let inner = 
                elemReader.State 
                |> Seq.map (fun elemState -> elemState.Destroy(state,JSExpr.GetElementById(elemState.Id)))
                |> Seq.toList
                |> JSExpr.Sequential

            elemReader <- Unchecked.defaultof<_>
            att.Destroy(state)
            inner

    and SceneUpdater<'msg>(e : SceneNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)

        let mutable initial = true
        let att = AttributeUpdater<_>(e.Attributes)

        static let processDeprecatedRenderEvents =
            let renderEventNames = ["onRendered"; "onRender"; "onrendered"; "onrender"]
            let mutable found = Set.empty

            fun (info: RenderClientInfo) (attributes: HashMap<string, AttributeValue<'msg>>) ->
                let event =
                    renderEventNames |> List.tryPickV (fun name ->
                        match attributes |> HashMap.tryFindV name with
                        | ValueSome event -> ValueSome (name, event)
                        | _ -> ValueNone
                    )

                match event with
                | ValueSome (name, AttributeValue.Event evt) ->
                    if found |> Set.contains name |> not then
                        Log.warn $"[Media] Deprecated render event '{name}'. Use predefined attribute 'onAfterRender' instead"
                        found <- found |> Set.add name

                    evt.serverSide info.session info.id.elementId [Pickler.json.PickleToString info.size]
                | _ ->
                    Seq.empty

        let sceneMessages =
            { 
                preRender = fun (info : RenderClientInfo) ->
                    let attributes = e.Attributes.Content.GetValue()
                    match attributes |> HashMap.tryFindV "onBeforeRender" with
                    | ValueSome (AttributeValue.RenderEvent f) -> f info
                    | _ -> Seq.empty

                postRender = fun (info : RenderClientInfo) ->
                    let attributes = e.Attributes.Content.GetValue()
                    match attributes |> HashMap.tryFindV "onAfterRender" with
                    | ValueSome (AttributeValue.RenderEvent f) -> f info
                    | _ ->
                        if Config.allowDeprecatedRenderEvents then
                            processDeprecatedRenderEvents info attributes
                        else
                            Seq.empty
            }

        override _.CreateElement = ValueSome {| tag = "div"; ns = null |}

        override this.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            if initial then
                initial <- false
                state.scenes.[this.Id] <- { scene = e.Scene; messages = ValueSome sceneMessages; getState = e.GetState }

            let id = this.Id
            att.Update(token, state, id, self)

        override this.PerformDestroy(state : UpdateState<'msg>, _ : JSExpr) =
            state.scenes.Remove(this.Id) |> ignore
            att.Destroy(state)
            initial <- true
            JSExpr.Nop

    and TextUpdater<'msg>(e : TextNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)

        let att = AttributeUpdater<_>(e.Attributes)
        override _.CreateElement = ValueSome {| tag = e.Tag; ns = e.Namespace |}

        override this.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            let id = this.Id
            let text = e.Text.GetValue token

            JSExpr.Sequential [
                att.Update(token, state, id, self)
                JSExpr.InnerText(self, text)
            ]

        override _.PerformDestroy(state : UpdateState<'msg>, _ : JSExpr) =
            att.Destroy(state)
            JSExpr.Nop

    and MapUpdater<'inner, 'outer>(m : MapNode<'inner, 'outer>, inner : IUpdater<'inner>) =
        inherit WrappedUpdater<'outer>(m, inner.Id)

        let mutable cache : ValueOption<UpdateState<'inner> * FSharp.Control.Event<'inner>> = ValueNone

        let mapMsg handler (session : Guid) (id : string) (args : list<string>) =
            match cache with
            | ValueSome (_,subject) ->
                let messages = handler session id args
                seq {
                    for msg in messages do
                        yield m.Mapping msg
                        subject.Trigger msg // if already destroyed, subapp does not receive msg anymore...
                }
            | _ ->
                Seq.empty

        let get (state : UpdateState<'outer>) =
            match cache with    
            | ValueSome c -> c
            | _ ->
                let subject = new FSharp.Control.Event<'inner>()
                let innerState =
                    {
                        scenes              = state.scenes |> ContraDict.map (fun _  -> SceneInfo.map m.Mapping)
                        handlers            = state.handlers |> ContraDict.map (fun _ -> EventHandler.map mapMsg)
                        references          = state.references
                        channels            = state.channels
                        messages            = subject.Publish
                    }
                cache <- ValueSome (innerState, subject)
                innerState, subject

        override _.PerformUpdate(token : AdaptiveToken, state : UpdateState<'outer>, insert : ValueOption<JSExpr -> JSExpr>) =
            let innerState, _ = get state
            inner.Update(token, innerState, insert)

        override _.PerformDestroy(_ : UpdateState<'outer>, self : JSExpr) =
            match cache with
            | ValueSome (innerState, _) ->
                //subject.Dispose()
                cache <- ValueNone
                inner.Destroy(innerState, self)
            | _ ->
                JSExpr.Nop

    and SubAppUpdater<'model, 'inner, 'outer>(n : SubAppNode<'model, 'inner, 'outer>, m : IMutableApp<'model, 'inner>, inner : IUpdater<'inner>) =
        inherit WrappedUpdater<'outer>(n, inner.Id)

        let mutable cache : ValueOption<UpdateState<'inner> * FSharp.Control.Event<'inner> * IDisposable> = ValueNone

        let processMsgs (session: Guid) (messages : seq<'inner>) =
            match cache with
            | ValueSome (_, subject, _) ->
                let messages = transact (fun () -> Seq.toList messages)
                m.Update(session, messages)
                for msg in messages do subject.Trigger msg
                let model = m.Model.GetValue()
                messages |> Seq.collect (fun msg -> n.App.ToOuter(model, msg))
            | _ ->
                Seq.empty

        let mapMsg handler (session: Guid) (id: string) (args : list<string>) =
            match cache with
            | ValueSome _ ->
                let messages = handler session id args
                processMsgs session messages
            | ValueNone ->
                Seq.empty

        let get (state : UpdateState<'outer>) =
            match cache with    
            | ValueSome c -> c
            | ValueNone ->
                let subject = new FSharp.Control.Event<'inner>()

                let mapSceneInfo (info: SceneInfo<'inner>) =
                    let messages =
                        info.messages |> ValueOption.map (fun msgs ->
                            { preRender  = fun (v : RenderClientInfo) -> msgs.preRender v |> processMsgs v.id.session
                              postRender = fun (v : RenderClientInfo) -> msgs.postRender v |> processMsgs v.id.session }
                        )

                    { scene    = info.scene
                      messages = messages
                      getState = info.getState }

                let innerState =
                    {
                        scenes              = state.scenes |> ContraDict.map (fun _ -> mapSceneInfo)
                        handlers            = state.handlers |> ContraDict.map (fun _ -> EventHandler.map mapMsg)
                        references          = state.references
                        channels            = state.channels
                        messages            = subject.Publish
                    }

                let subscription = 
                    state.messages.Subscribe(fun msg -> 
                        let msgs = n.App.ToInner(m.Model.GetValue(), msg)
                        m.Update(Guid.Empty, msgs)
                        for m in msgs do subject.Trigger m
                    )

                cache <- ValueSome (innerState, subject, subscription)
                (innerState, subject, subscription)

        override _.PerformUpdate(token : AdaptiveToken, state : UpdateState<'outer>, insert : ValueOption<JSExpr -> JSExpr>) =
            let innerState, _, _ = get state
            inner.Update(token, innerState, insert)

        override _.PerformDestroy(_ : UpdateState<'outer>, self : JSExpr) =
            match cache with
            | ValueSome (innerState, _, subscription) ->
                subscription.Dispose()
                m.Dispose()
                cache <- ValueNone
                inner.Destroy(innerState, self)
            | ValueNone ->
                JSExpr.Nop

    and [<AbstractClass; Sealed>] Updater =
        static member New<'msg>(node : DomNode<'msg>, request : IHttpRequest) : IUpdater<'msg> =
            node.Visit {
                new DomNodeVisitor<'msg, IUpdater<'msg>> with
                    member _.Empty e    = EmptyUpdater(e) :> IUpdater<_>
                    member _.Inner n    = InnerUpdater(n, request) :> IUpdater<_>
                    member _.Void n     = VoidUpdater(n) :> IUpdater<_>
                    member _.Scene n    = SceneUpdater(n) :> IUpdater<_>
                    member _.Text n     = TextUpdater(n) :> IUpdater<_>
                    member _.Page n     = Updater.New(n.Content(request), request)
                    member _.SubApp n   =
                        let mapp = n.App.Start()
                        let updater = Updater.New(mapp.Dom :?> DomNode<_>, request)
                        SubAppUpdater(n, mapp, updater) :> IUpdater<_>
                    member _.Map n      = MapUpdater(n, Updater.New(n.Node, request)) :> IUpdater<_>
            }

[<AbstractClass; Sealed>]
type internal DomNodeExtensions =
    [<Extension>]
    static member NewUpdater<'msg>(node: DomNode<'msg>, request: IHttpRequest) =
        Updaters.Updater.New(node, request)