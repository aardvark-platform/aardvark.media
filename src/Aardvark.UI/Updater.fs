namespace Aardvark.UI.Internal

open System
open System.Threading
open System.Collections.Generic
open Aardvark.UI
open Aardvark.Base
open Aardvark.Service
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive

module Updaters =

    [<AutoOpen>]
    module IdGen =
        let mutable private currentId = 0
        let newId() =
            let id = Interlocked.Increment(&currentId)
            "n" + string id

    type ContraDict<'k, 'v> =
        abstract member Item : 'k -> 'v with set
        abstract member Remove : 'k -> bool

    module ContraDict =
        let ofDictionary (d : Dictionary<'k, 'v>) =
            { new ContraDict<'k, 'v> with
                member x.Item with set (k : 'k) (v : 'v) = d.[k] <- v
                member x.Remove k = d.Remove k
            }

        let map (f : 'k -> 'b -> 'v) (d : ContraDict<'k, 'v>) =
            { new ContraDict<'k, 'b> with
                member x.Item with set (k : 'k) (v : 'b) = d.[k] <- f k v
                member x.Remove k = d.Remove k
            }

    type RootHandlers<'msg> = Dictionary<string * string, Guid -> string -> list<string> -> seq<'msg>>

    type UpdateState<'msg> =
        {
            scenes              : ContraDict<string, Scene * Option<ClientInfo -> seq<'msg>> * (ClientInfo -> ClientState)>
            handlers            : ContraDict<string * string, Guid -> string -> list<string> -> seq<'msg>>
            references          : Dictionary<string * ReferenceKind, Reference>
            activeChannels      : Dict<string * string, ChannelReader>
            messages            : IObservable<'msg>
        }

    type IUpdater =
        inherit IAdaptiveObject
        abstract member Id : Option<string>
            
    type IUpdater<'msg> =
        inherit IUpdater
        abstract member Update : AdaptiveToken * UpdateState<'msg> * Option<JSExpr -> JSExpr> -> JSExpr
        abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr
        abstract member TryReplace :  AdaptiveToken * UpdateState<'msg> * DomNode<'msg> * self : JSExpr -> Option<JSExpr>


    [<AbstractClass>]
    type AbstractUpdater<'msg>(node : DomNode<'msg>) =
        inherit AdaptiveObject()
        let mutable initial = true
        let id = lazy (IdGen.newId())

        let setup (state : UpdateState<'msg>) =
            if id.IsValueCreated then
                for (name,cb) in Map.toSeq node.Callbacks do
                    state.handlers.[(id.Value,name)] <- fun _ _ v -> Seq.delay (fun () -> Seq.singleton (cb v))

            for r in node.Required do
                state.references.[(r.name, r.kind)] <- r
                
            match node.Boot with
                | Some boot ->
                    let id = if id.IsValueCreated then id.Value else "NOT AN ID"
                    let code = boot id

                    if Map.isEmpty node.Channels then
                        Raw code
                    else 
                        for (name, c) in Map.toSeq node.Channels do
                            state.activeChannels.[(id,name)] <- c.GetReader()

                        let prefix = 
                            node.Channels 
                            |> Map.toList
                            |> List.map (fun (name, _) -> 
                                sprintf "var %s = aardvark.getChannel(\"%s\", \"%s\");" name id name
                            ) 
                            |> String.concat "\r\n"
                        
                        Raw (prefix + "\r\n" + code)
                        
                | None ->
                    JSExpr.Nop

        let shutdown (state : UpdateState<'msg>) =
            if id.IsValueCreated then
                for (name, c) in Map.toSeq node.Channels do
                    state.activeChannels.Remove((id.Value,name)) |> ignore

            match node.Shutdown with
                | Some shutdown ->
                    if id.IsValueCreated then
                        Raw (shutdown id.Value)
                    else
                        Raw (shutdown "NOT AN ID")
                | None ->
                    JSExpr.Nop
                            


        abstract member CreateElement : Option<string * Option<string>>
        default x.CreateElement = None

        abstract member PerformUpdate : AdaptiveToken * UpdateState<'msg> * JSExpr -> JSExpr
        abstract member PerformDestroy : UpdateState<'msg> * JSExpr -> JSExpr

        abstract member Id : Option<string>
        default x.Id = 
            match x.CreateElement with
                | Some _ -> Some id.Value
                | None -> None

        member x.Update(token : AdaptiveToken, state : UpdateState<'msg>, insert : Option<JSExpr -> JSExpr>) =
            x.EvaluateIfNeeded token JSExpr.Nop (fun token ->
                match x.CreateElement with
                    | Some (tag, ns) ->
                        if initial then
                            initial <- false
                            if tag = "body" then
                                JSExpr.Sequential [
                                    SetAttribute(JSExpr.Body, "id", id.Value)
                                    x.PerformUpdate(token, state, JSExpr.Body)
                                    setup state
                                ]
                            else
                                let var = { name = id.Value }
                                JSExpr.Let(var, JSExpr.CreateElement(tag, ns),
                                    JSExpr.Sequential [
                                        (
                                            match insert with
                                            | Some insert -> insert (JSExpr.Var var)
                                            | None -> failwith "[UI] should not be inserted"
                                        )
                                        SetAttribute(JSExpr.Var var, "id", id.Value)
                                        x.PerformUpdate(token, state, JSExpr.Var var)
                                        setup state
                                    ]
                                )
                        else
                            let var = { name = id.Value }
                            JSExpr.Let(var, JSExpr.GetElementById(id.Value),
                                x.PerformUpdate(token, state, JSExpr.Var var)
                            )
                                    
                    | None ->
                        if initial then
                            initial <- false
                            JSExpr.Sequential [
                                setup state
                                x.PerformUpdate(token, state, JSExpr.Nop)
                            ]
                        else
                            x.PerformUpdate(token, state, JSExpr.Nop)
                    
            )

        abstract member TryReplace :  AdaptiveToken * UpdateState<'msg> * DomNode<'msg> * self : JSExpr -> Option<JSExpr>
        default x.TryReplace (token : AdaptiveToken, state : UpdateState<'msg>, updater : DomNode<'msg>, self : JSExpr) = None

        member x.Destroy(state, self) =
            lock x (fun () -> 
                match x.CreateElement with
                    | Some _ -> 
                        JSExpr.Sequential [
                            shutdown state
                            x.PerformDestroy(state, self)
                        ]
                    | None ->
                        JSExpr.Sequential [
                            shutdown state
                            x.PerformDestroy(state, self)
                        ]
            )

        interface IUpdater<'msg> with
            member x.Id = x.Id
            member x.Update(t,state, s) = x.Update(t,state, s)
            member x.Destroy(state, self) = x.Destroy(state, self)
            member x.TryReplace(t, s, n, self) = x.TryReplace(t, s, n, self)
                
    [<AbstractClass>]
    type WrappedUpdater<'msg>(node : DomNode<'msg>, id : Option<string>) =
        inherit AdaptiveObject()
        let mutable initial = true

        let setup (state : UpdateState<'msg>) =
            for r in node.Required do
                state.references.[(r.name, r.kind)] <- r

            match node.Boot with
                | Some boot ->
                    let id = "NOT AN ID"
                    let code = boot id

                    if Map.isEmpty node.Channels then
                        Raw code
                    else 
                        for (name, c) in Map.toSeq node.Channels do
                            state.activeChannels.[(id,name)] <- c.GetReader()

                        let prefix = 
                            node.Channels 
                            |> Map.toList
                            |> List.map (fun (name, _) -> 
                                sprintf "var %s = aardvark.getChannel(\"%s\", \"%s\");" name id name
                            ) 
                            |> String.concat "\r\n"
                        
                        Raw (prefix + "\r\n" + code)
                        
                | None ->
                    JSExpr.Nop

        let shutdown() =
            match node.Shutdown with
                | Some shutdown ->
                    Raw (shutdown "NOT AN ID")
                | None ->
                    JSExpr.Nop
                            
        abstract member PerformUpdate : AdaptiveToken * UpdateState<'msg> * Option<JSExpr -> JSExpr> -> JSExpr
        abstract member PerformDestroy : UpdateState<'msg> * JSExpr -> JSExpr
        abstract member TryReplace :  AdaptiveToken * UpdateState<'msg> * DomNode<'msg> * self : JSExpr -> Option<JSExpr>
        default x.TryReplace (token : AdaptiveToken, state : UpdateState<'msg>, updater : DomNode<'msg>, self : JSExpr) = None
            
        member x.Id = id

        member x.Update(token : AdaptiveToken, state : UpdateState<'msg>, insert : Option<JSExpr -> JSExpr>) =
            x.EvaluateIfNeeded token JSExpr.Nop (fun token ->
                if initial then
                    initial <- false
                    JSExpr.Sequential [
                        x.PerformUpdate(token, state, insert)
                        setup state
                    ]
                else
                    x.PerformUpdate(token, state, insert)
                    
            )

        member x.Destroy(state, self) =
            lock x (fun () -> 
                JSExpr.Sequential [
                    shutdown()
                    x.PerformDestroy(state, self)
                ]
            )

        interface IUpdater<'msg> with
            member x.Id = id
            member x.Update(t,state, s) = x.Update(t,state, s)
            member x.Destroy(state, self) = x.Destroy(state, self)
            member x.TryReplace(token, state, n, self) = x.TryReplace(token, state, n, self)
            

    type AttributeUpdater<'msg>(attributes : AttributeMap<'msg>) =
        let mutable attributes = attributes
        let mutable reader = attributes.GetReader()

        member x.Attributes = attributes

        member x.TryGetAttrbute(name : string) =
            HashMap.tryFind name reader.State

        member x.Update(token : AdaptiveToken, state : UpdateState<'msg>, id : string, self : JSExpr) =
            JSExpr.Sequential [
                let old = reader.State
                let atts = reader.GetChanges token
                for (name, op) in atts do
                    match op with
                        | Set value ->
                            let value =
                                match value with
                                    | AttributeValue.String str -> 
                                        str
                                    | AttributeValue.Event evt ->
                                        let key = (id, name)
                                        state.handlers.[key] <- evt.serverSide
                                        Event.toString id name evt

                            yield JSExpr.SetAttribute(self, name, value)

                            //yield JSExpr.SetAttribute(self, name, value)

                        | Remove ->
                            yield JSExpr.RemoveAttribute(self, name)
            ]

        member x.Replace(token : AdaptiveToken, state : UpdateState<'msg>, m : AttributeMap<'msg>, id : string, self : JSExpr) = 
            if attributes = m then
                JSExpr.Nop
            else
                let newReader = m.GetReader()
                newReader.GetChanges(token) |> ignore

                let delta = HashMap.computeDelta reader.State newReader.State
                reader.Outputs.Consume(ref [||]) |> ignore
                reader <- newReader
                attributes <- m
                JSExpr.Sequential [
                    for (name, op) in delta do
                        match op with
                            | Set value ->
                                let value =
                                    match value with
                                        | AttributeValue.String str -> 
                                            str
                                        | AttributeValue.Event evt ->
                                            let key = (id, name)
                                            state.handlers.[key] <- evt.serverSide
                                            Event.toString id name evt

                                yield JSExpr.SetAttribute(self, name, value)

                                //yield JSExpr.SetAttribute(self, name, value)

                            | Remove ->
                                yield JSExpr.RemoveAttribute(self, name)
                ]

        member x.Dispose() =
            ()
            reader <- Unchecked.defaultof<_>
            //reader.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type EmptyUpdater<'msg>(e : EmptyNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)

        override x.CreateElement = None

        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, _) =
            JSExpr.Nop

        override x.PerformDestroy(state : UpdateState<'msg>, _) =
            JSExpr.Nop

    and VoidUpdater<'msg>(e : VoidNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)
            
        let att = new AttributeUpdater<_>(e.Attributes)
        override x.CreateElement = Some(e.Tag, e.Namespace)
            
        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            let id = x.Id.Value
            att.Update(token, state, id, self)

        override x.PerformDestroy(state : UpdateState<'msg>, self : JSExpr) =
            att.Dispose()
            JSExpr.Nop

    and InnerUpdater<'msg>(e : InnerNode<'msg>, request : Request) =
        inherit AbstractUpdater<'msg>(e)
            
        let mutable e = e
        let mutable elemReader : IIndexListReader<_> = e.Children.GetReader() //(AList.map (fun n -> Foo.NewUpdater(n, request)) e.Children).GetReader()
        let att = new AttributeUpdater<_>(e.Attributes)
        let mutable dirty = IndexList.Empty

        static let cmpState =
            { new IComparer<Index * ref<IUpdater<'msg>>> with
                member x.Compare((l,_), (r,_)) =
                    compare l r
            }

        let content = SortedSetExt<Index * ref<IUpdater<'msg>>>(cmpState)

        let neighbours (i : Index) =
            let (l,s,r) = content.FindNeighbours((i, Unchecked.defaultof<_>))
            let l = if l.HasValue then Some l.Value else None
            let r = if r.HasValue then Some r.Value else None
            let s = if s.HasValue then Some (snd s.Value) else None
            l, s, r


        override x.CreateElement = Some(e.Tag, e.Namespace)


        override x.InputChangedObject(t,o) =
            match o with
            | :? IUpdater<'msg> as o -> 
                match o.Tag with
                | :? Index as i -> 
                    lock content (fun () ->
                        dirty <- IndexList.set i o dirty
                    )
                | _ -> 
                    failwith "untagged inner IUpdater"
            | _ ->
                ()

        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            JSExpr.Sequential [
                let id = x.Id.Value
                yield att.Update(token, state, id, self)
                      
                let mutable toUpdate = 
                    lock content (fun () ->
                        let d = dirty
                        dirty <- IndexList.empty
                        d
                    )
                let ops = elemReader.GetChanges token


                for (i,op) in IndexListDelta.toSeq ops do
                    match op with
                        | ElementOperation.Remove ->
                            let (_,s,_) = neighbours i
                            toUpdate <- IndexList.remove i toUpdate
                            match s with
                                | Some n -> 
                                    let n = !n
                                    n.Outputs.Remove x |> ignore
                                    content.Remove(i, Unchecked.defaultof<_>) |> ignore
                                    n.Tag <- null

                                    match n.Id with
                                        | Some id -> 
                                            let self = { name = id }
                                            yield JSExpr.Let(self, GetElementById id,
                                                JSExpr.Sequential [
                                                    n.Destroy(state, JSExpr.Var self)
                                                    JSExpr.Remove (JSExpr.Var self)
                                                ]
                                            )
                                        | _ ->
                                            yield n.Destroy(state, JSExpr.Nop)
                                | None ->
                                    failwith "[Media] UI Updater. trying to remove non existent objects (locking issue?)"

                        | ElementOperation.Set newElement ->
                            let (l,s,r) = neighbours i
                            //newElement.Tag <- i
                                    
                            let (|HasId|_|) (n : ref<IUpdater<'msg>>) =
                                match n.Value.Id with
                                    | Some id -> Some (id, n.Value)
                                    | _ -> None

                            let insert =
                                match l, r with
                                    | _, Some (ri, HasId(rid, r))  ->
                                        fun v -> JSExpr.InsertBefore(GetElementById rid, v)

                                    | Some (li, HasId(lid, l)), _ ->
                                        fun v -> JSExpr.InsertAfter(GetElementById lid, v)

                                    | _ ->
                                        fun v -> JSExpr.AppendChild(self, v)

                            match s with
                                | Some ref ->
                                    let oldUpdater = !ref

                                    let self = match oldUpdater.Id with | Some id -> GetElementById id | None -> JSExpr.Nop
                                    match oldUpdater.TryReplace(token, state, newElement, self) with
                                    | Some code -> 
                                        yield code
                                    | None ->
                                        let newUpdater = Foo.NewUpdater(newElement, request)
                                        newUpdater.Tag <- i
                                        oldUpdater.Outputs.Remove x |> ignore
                                        ref := newUpdater
                                        toUpdate <- IndexList.remove i toUpdate
                                        match oldUpdater.Id, newUpdater.Id with
                                            | Some o, Some id ->
                                                let vo = { name = o }
                                                yield 
                                                    JSExpr.Let(vo,  GetElementById o,
                                                        JSExpr.Sequential [
                                                            oldUpdater.Destroy(state, Var vo)
                                                            newUpdater.Update(token, state, Some (fun n -> JSExpr.Replace(Var vo, n)))
                                                        ]
                                                    )

                                            | None, Some id ->
                                                yield
                                                    JSExpr.Sequential [
                                                        oldUpdater.Destroy(state, JSExpr.Nop)
                                                        newUpdater.Update(token, state, Some insert)
                                                    ]

                                            | Some o, None ->
                                                yield
                                                    JSExpr.Sequential [
                                                        oldUpdater.Destroy(state, GetElementById o)
                                                        newUpdater.Update(token, state, None)
                                                    ]

                                            | None, None ->
                                                yield
                                                    JSExpr.Sequential [
                                                        oldUpdater.Destroy(state, JSExpr.Nop)
                                                        newUpdater.Update(token, state, None)
                                                    ]
                                                    

                                | _ ->
                                    let u = Foo.NewUpdater(newElement, request)
                                    u.Tag <- i
                                    content.Add(i, ref u) |> ignore
                                    yield u.Update(token, state, Some insert)

                for u in toUpdate do
                    yield u.Update(token, state, None)

            ]

        override x.PerformDestroy(state : UpdateState<'msg>, self : JSExpr) =
            
            let inner = 
                content
                |> Seq.map (fun (_, { contents = r }) -> 
                    let self = match r.Id with | Some id -> GetElementById id | None -> JSExpr.Nop
                    r.Destroy(state,self)
                ) 
                |> Seq.toList
                |> JSExpr.Sequential

            elemReader <- Unchecked.defaultof<_>
            att.Dispose()
            inner
            
        override x.TryReplace(token : AdaptiveToken, state : UpdateState<'msg>, newNode : DomNode<'msg>, self : JSExpr) = 
            match newNode with
            | :? InnerNode<'msg> as newNode when e.Tag = newNode.Tag && e.Namespace = newNode.Namespace ->
                
                let r = newNode.Children.GetReader()
                r.GetChanges token |> ignore

                let ops = IndexList.computeDelta elemReader.State r.State
                elemReader.Outputs.Consume(ref [||]) |> ignore
                e <- newNode
                elemReader <- r
                let mutable toUpdate = 
                    lock content (fun () ->
                        let d = dirty
                        dirty <- IndexList.empty
                        d
                    )
                JSExpr.Sequential [
                    yield att.Replace(token, state, newNode.Attributes, x.Id.Value, self)

                    for (i,op) in IndexListDelta.toSeq ops do
                        match op with
                            | ElementOperation.Remove ->
                                let (_,s,_) = neighbours i
                                toUpdate <- IndexList.remove i toUpdate
                                match s with
                                    | Some n -> 
                                        let n = !n
                                        n.Outputs.Remove x |> ignore
                                        content.Remove(i, Unchecked.defaultof<_>) |> ignore


                                        match n.Id with
                                            | Some id -> 
                                                let self = { name = id }
                                                yield JSExpr.Let(self, GetElementById id,
                                                    JSExpr.Sequential [
                                                        n.Destroy(state, JSExpr.Var self)
                                                        JSExpr.Remove (JSExpr.Var self)
                                                    ]
                                                )
                                            | _ ->
                                                yield n.Destroy(state, JSExpr.Nop)
                                    | None ->
                                        failwith "[Media] UI Updater. trying to remove non existent objects (locking issue?)"

                            | ElementOperation.Set newElement ->
                                let (l,s,r) = neighbours i
                                //newElement.Tag <- i
                                    
                                let (|HasId|_|) (n : ref<IUpdater<'msg>>) =
                                    match n.Value.Id with
                                        | Some id -> Some (id, n.Value)
                                        | _ -> None

                                let insert =
                                    match l, r with
                                        | _, Some (ri, HasId(rid, r))  ->
                                            fun v -> JSExpr.InsertBefore(GetElementById rid, v)

                                        | Some (li, HasId(lid, l)), _ ->
                                            fun v -> JSExpr.InsertAfter(GetElementById lid, v)

                                        | _ ->
                                            fun v -> JSExpr.AppendChild(self, v)

                                match s with
                                    | Some ref ->
                                        let oldUpdater = !ref

                                        let self = match oldUpdater.Id with | Some id -> GetElementById id | None -> JSExpr.Nop
                                        match oldUpdater.TryReplace(token, state, newElement, self) with
                                        | Some code -> 
                                            yield code
                                        | None ->
                                            let newUpdater = Foo.NewUpdater(newElement, request)
                                            oldUpdater.Outputs.Remove x |> ignore
                                            ref := newUpdater
                                            toUpdate <- IndexList.remove i toUpdate
                                            match oldUpdater.Id, newUpdater.Id with
                                                | Some o, Some id ->
                                                    let vo = { name = o }
                                                    yield 
                                                        JSExpr.Let(vo,  GetElementById o,
                                                            JSExpr.Sequential [
                                                                oldUpdater.Destroy(state, Var vo)
                                                                newUpdater.Update(token, state, Some (fun n -> JSExpr.Replace(Var vo, n)))
                                                            ]
                                                        )

                                                | None, Some id ->
                                                    yield
                                                        JSExpr.Sequential [
                                                            oldUpdater.Destroy(state, JSExpr.Nop)
                                                            newUpdater.Update(token, state, Some insert)
                                                        ]

                                                | Some o, None ->
                                                    yield
                                                        JSExpr.Sequential [
                                                            oldUpdater.Destroy(state, GetElementById o)
                                                            newUpdater.Update(token, state, None)
                                                        ]

                                                | None, None ->
                                                    yield
                                                        JSExpr.Sequential [
                                                            oldUpdater.Destroy(state, JSExpr.Nop)
                                                            newUpdater.Update(token, state, None)
                                                        ]
                                                    

                                    | _ ->
                                        let u = Foo.NewUpdater(newElement, request)
                                        content.Add(i, ref u) |> ignore
                                        yield u.Update(token, state, Some insert)

                    for u in toUpdate do
                        yield u.Update(token, state, None)


                ] |> Some

            | _ -> 
                None

    and SceneUpdater<'msg>(e : SceneNode<'msg>) as this =
        inherit AbstractUpdater<'msg>(e)
            
        let mutable initial = true
        let att = new AttributeUpdater<_>(e.Attributes)

        let renderMessage (info : ClientInfo) =
            let attributes = e.Attributes.Content.GetValue()
            match HashMap.tryFind "preRender" attributes with
                | Some (AttributeValue.Event evt) ->
                    evt.serverSide info.session this.Id.Value []
                | _ ->
                    Seq.empty
                    
        override x.CreateElement = Some ("div", None)

        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            if initial then
                initial <- false
                state.scenes.[x.Id.Value] <- (e.Scene, Some renderMessage, e.GetClientState)
                    
            let id = x.Id.Value
            att.Update(token, state, id, self)
             

        override x.PerformDestroy(state : UpdateState<'msg>, self : JSExpr) =
            state.scenes.Remove(x.Id.Value) |> ignore
            att.Dispose()
            initial <- true
            JSExpr.Nop
        
    and TextUpdater<'msg>(e : TextNode<'msg>) =
        inherit AbstractUpdater<'msg>(e)
            
        let mutable lastText = null
        let mutable e = e
        let att = new AttributeUpdater<_>(e.Attributes)

        member x.Node = e
        member x.Attributes = att

        override x.CreateElement = Some (e.Tag, e.Namespace)

        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'msg>, self : JSExpr) =
            let id = x.Id.Value
            let text = e.Text.GetValue token
            let changed = lastText <> text
            lastText <- text
            JSExpr.Sequential [
                att.Update(token, state, id, self)
                if changed then JSExpr.InnerText(self, text)
            ]

        override x.PerformDestroy(state : UpdateState<'msg>, self : JSExpr) =
            att.Dispose()
            lastText <- null
            JSExpr.Nop

        override x.TryReplace (token : AdaptiveToken, state : UpdateState<'msg>, newNode : DomNode<'msg>, self : JSExpr) = 
            match newNode with
            | :? TextNode<'msg> as tn -> 
                if e.Tag = tn.Tag && e.Namespace = tn.Namespace then
                    let newText = tn.Text
                    let oldText = e.Text
                    e <- tn

                    JSExpr.Sequential [
                        yield att.Replace(token, state, tn.Attributes, x.Id.Value, self)

                        if oldText <> newText then
                            oldText.Outputs.Remove x |> ignore
                            let t = newText.GetValue token
                            if t <> lastText then
                                lastText <- t
                                yield JSExpr.InnerText(self,  t)

                    ] |> Some
                else 
                    None
            | _ -> None


    and MapUpdater<'inner, 'outer>(m : MapNode<'inner, 'outer>, inner : IUpdater<'inner>) =
        inherit WrappedUpdater<'outer>(m, inner.Id)
            
        let mutable cache : Option<UpdateState<'inner> * FSharp.Control.Event<'inner>> = None
            
        let mapMsg handler (client : Guid) (name : string) (args : list<string>) =
            match cache with
                | Some (_,subject) ->
                    let messages = handler client name args
                    seq {
                        for msg in messages do
                            yield m.Mapping msg
                            subject.Trigger msg // if already destroyed, subapp does not receive msg any more...
                    }
                | _ ->
                    Seq.empty

        let get (state : UpdateState<'outer>) =
            match cache with    
            | Some c -> c
            | None ->
                let subject = new FSharp.Control.Event<'inner>()
                let innerState =
                    let test = state.scenes |> ContraDict.map (fun _ (scene, msg, getState) -> (scene, (msg |> Option.map (fun f -> f >> Seq.map m.Mapping)), getState))
                    {
                        scenes              = test
                        handlers            = state.handlers |> ContraDict.map (fun _ v -> mapMsg v)
                        references          = state.references
                        activeChannels      = state.activeChannels
                        messages            = subject.Publish
                    }
                cache <- Some (innerState, subject)
                (innerState, subject)
                    
        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'outer>, insert : Option<JSExpr -> JSExpr>) =
            let innerState, subject = get state
            inner.Update(token, innerState, insert)

        override x.PerformDestroy(state : UpdateState<'outer>, self : JSExpr) =
            match cache with
                | Some (innerState, subject) ->
                    //subject.Dispose()
                    cache <- None
                    inner.Destroy(innerState, self)
                | None ->
                    JSExpr.Nop
            
    and SubAppUpdater<'model, 'inner, 'outer>(n : SubAppNode<'model, 'inner, 'outer>, m : MutableApp<'model, 'inner>, inner : IUpdater<'inner>) =
        inherit WrappedUpdater<'outer>(n, inner.Id)
            
        let mutable cache : Option<UpdateState<'inner> * FSharp.Control.Event<'inner> * IDisposable> = None

        let processMsgs (client : Guid) (messages : seq<'inner>) =
            match cache with
                | Some (_, subject, _) ->
                    let messages = transact (fun () -> Seq.toList messages)
                    m.update client (messages :> seq<_>)
                    for msg in messages do subject.Trigger msg
                    let model = m.model.GetValue()
                    messages |> Seq.collect (fun msg -> n.App.ToOuter(model, msg))
                | _ ->
                    Seq.empty

        let mapMsg handler (client : Guid) (bla : string) (args : list<string>) =
            match cache with
                | Some (_, subject, _) ->
                    let messages = handler client bla args
                    processMsgs client messages
                | None ->
                    Seq.empty

        let get (state : UpdateState<'outer>) =
            match cache with    
            | Some c -> c
            | None ->
                let subject = new FSharp.Control.Event<'inner>()
                let innerState =
                    {
                        scenes              = state.scenes |> ContraDict.map (fun _ (scene, msg, getState) -> (scene, (msg |> Option.map (fun f v -> let model = m.model.GetValue() in v |> f |> processMsgs v.session)), getState))
                        handlers            = state.handlers |> ContraDict.map (fun _ v -> mapMsg v)
                        references          = state.references
                        activeChannels      = state.activeChannels
                        messages            = subject.Publish
                    }

                    
                let subscription = 
                    state.messages.Subscribe(fun msg -> 
                        let msgs = n.App.ToInner(m.model.GetValue(), msg)
                        m.update Guid.Empty msgs
                        for m in msgs do subject.Trigger m

                    )

                cache <- Some (innerState, subject, subscription)
                (innerState, subject, subscription)
                    
        override x.PerformUpdate(token : AdaptiveToken, state : UpdateState<'outer>, insert : Option<JSExpr -> JSExpr>) =
            let innerState, subject, _ = get state
            inner.Update(token, innerState, insert)

        override x.PerformDestroy(state : UpdateState<'outer>, self : JSExpr) =
            match cache with
                | Some (innerState, subject, subscription) ->
                    subscription.Dispose()
                    m.shutdown()
                    cache <- None
                    inner.Destroy(innerState, self)
                | None ->
                    JSExpr.Nop
            
    and internal Foo () =
        static member NewUpdater<'msg>(x : DomNode<'msg>, request : Request) : IUpdater<'msg> =
            x.Visit {
                new DomNodeVisitor<'msg, IUpdater<'msg>> with
                    member x.Empty e    = EmptyUpdater(e) :> IUpdater<_>
                    member x.Inner n    = InnerUpdater(n, request) :> IUpdater<_>
                    member x.Void n     = VoidUpdater(n) :> IUpdater<_>
                    member x.Scene n    = SceneUpdater(n) :> IUpdater<_>
                    member x.Text n     = TextUpdater(n) :> IUpdater<_>
                    member x.Page n     = Foo.NewUpdater(n.Content(request), request)
                    member x.SubApp n   = 
                        let mapp = n.App.Start()
                        let updater = Foo.NewUpdater(mapp.ui, request)
                        SubAppUpdater(n, mapp, updater) :> IUpdater<_>
                    member x.Map n      = MapUpdater(n, Foo.NewUpdater(n.Node, request)) :> IUpdater<_>
            }
    
[<AbstractClass; Sealed; Extension>]
type DomNodeExtensions private() =
    [<Extension>]
    static member NewUpdater<'msg>(x : DomNode<'msg>, request : Request) : Updaters.IUpdater<'msg> = Updaters.Foo.NewUpdater(x, request)
        