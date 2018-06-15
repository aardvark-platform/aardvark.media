namespace Aardvark.UI.Internal

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Service
open Aardvark.UI

[<AutoOpen>]
module IdGen =
    let mutable private currentId = 0
    let newId() =
        let id = Interlocked.Increment(&currentId)
        "n" + string id


type UpdateState<'msg> =
    {
        scenes              : Dictionary<string, Scene * (ClientInfo -> ClientState)>
        handlers            : Dictionary<string * string, Guid -> string -> list<string> -> seq<'msg>>
        references          : Dictionary<string * ReferenceKind, Reference>
        activeChannels      : Dict<string * string, ChannelReader>
    }

type IUpdater<'msg> =
    inherit IAdaptiveObject
    abstract member Update : AdaptiveToken * JSExpr * UpdateState<'msg> -> JSExpr
    abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr

    //abstract member Html : AdaptiveToken -> 




[<AbstractClass>]
type AbstractUpdater<'msg>() =
    inherit AdaptiveObject()

    abstract member PerformUpdate : AdaptiveToken * JSExpr * UpdateState<'msg> -> JSExpr
    abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr

    member x.Update(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
        x.EvaluateIfNeeded token JSExpr.Nop (fun token ->
            x.PerformUpdate(token, self, state)
        )

    interface IUpdater<'msg> with
        member x.Update(t,s,state) = x.Update(t,s,state)
        member x.Destroy(state, self) = x.Destroy(state, self)

type SceneUpdater<'msg>(ui : DomNode<'msg>, id : string, scene : Scene, camera : ClientInfo -> ClientState) =
    inherit AbstractUpdater<'msg>()

    let mutable initial = true

    override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
        state.scenes.Remove(id) |> ignore
        JSExpr.Nop

    override x.PerformUpdate(token, self, state) =
        if initial then
            initial <- false
            state.scenes.[id] <- (scene, camera)
        JSExpr.Nop

type EmptyUpdater<'msg> private() =
    inherit ConstantObject()

    static let instance = EmptyUpdater<'msg>() :> IUpdater<_>
    static member Instance = instance

    interface IUpdater<'msg> with
        member x.Update(_,_,_) = JSExpr.Nop
        member x.Destroy(_,_) = JSExpr.Nop

type TextUpdater<'msg>(text : IMod<string>) =
    inherit AbstractUpdater<'msg>()

    override x.PerformUpdate(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
        let nt = text.GetValue token
        JSExpr.InnerText(self, nt)

    override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
        JSExpr.Nop

type ChildrenUpdater<'msg>(id : string, children : alist<DomUpdater<'msg>>) =
    inherit AbstractUpdater<'msg>()
    let reader = children.GetReader()

    static let cmpState =
        { new IComparer<Index * ref<DomUpdater<'msg>>> with
            member x.Compare((l,_), (r,_)) =
                compare l r
        }

    let content = SortedSetExt<Index * ref<DomUpdater<'msg>>>(cmpState)

    let neighbours (i : Index) =
        let (l,s,r) = content.FindNeighbours((i, Unchecked.defaultof<_>))
        let l = if l.HasValue then Some l.Value else None
        let r = if r.HasValue then Some r.Value else None
        let s = if s.HasValue then Some (snd s.Value) else None
        l, s, r

    static let create (ui : DomUpdater<'msg>) (inner : JSExpr -> list<JSExpr>) =
        if ui.Tag = "" then
            inner JSExpr.Nop |> JSExpr.Sequential
        else
            let v = { name = ui.Id }
            JSExpr.Let(
                v, JSExpr.CreateElement(ui.Tag, ui.Namespace),
                JSExpr.Sequential (
                    JSExpr.SetAttribute(JSExpr.Var v, "id", ui.Id) :: inner (JSExpr.Var v)
                )
            )

    //let mutable initial = true
    //let lastId = id + "_last"

    override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
        let all = List()
        for (_,r) in content do
            r.Value.Destroy(state, GetElementById r.Value.Id) |> all.Add
        content.Clear()
        reader.Dispose()
        JSExpr.Sequential (CSharpList.toList all)

    override x.PerformUpdate(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
        let code = List<JSExpr>()
        //if initial then
        //    initial <- false
        //    let v = { name = lastId }
        //    code.Add(Expr.Let(v, CreateElement("span"), Expr.Sequential [SetAttribute(Var v, "id", lastId); AppendChild(self, Var v) ]))


        let mutable toUpdate = reader.State
        let ops = reader.GetOperations token


        for (i,op) in PDeltaList.toSeq ops do
            match op with
                | ElementOperation.Remove ->
                    let (_,s,_) = neighbours i
                    toUpdate <- PList.remove i toUpdate
                    match s with
                        | Some n -> 
                            let n = !n
                            content.Remove(i, Unchecked.defaultof<_>) |> ignore
                            code.Add(Remove (GetElementById n.Id))
                            code.Add(n.Destroy(state, GetElementById n.Id))
                        | None ->
                            failwith "sadasdlnsajdnmsad"

                | ElementOperation.Set newElement ->
                    let (l,s,r) = neighbours i
                                    
                    match s with
                        | Some ref ->
                            let oldElement = !ref
                            code.Add(oldElement.Destroy(state, GetElementById oldElement.Id))
                            ref := newElement

                            toUpdate <- PList.remove i toUpdate

                            let v = { name = newElement.Id }
                            let expr = 
                                create newElement (fun n ->
                                    [
                                        JSExpr.Replace(GetElementById oldElement.Id, n)
                                        newElement.Update(token, Var v, state)
                                    ]
                                )

                            code.Add expr

                        | _ ->
                            content.Add(i, ref newElement) |> ignore

                            match r with
                                | None ->
                                        
                                    //let expr = create newElement (fun n -> [ InsertBefore(GetElementById lastId, n); newElement.Update(token, n, state) ] )
                                    //code.Add expr
                                    match l with
                                        | None -> 
                                            let expr = create newElement (fun n -> [ AppendChild(self, n); newElement.Update(token, n, state) ] )
                                            code.Add expr
                                        | Some(_,l) ->
                                            let expr = create newElement (fun n -> [ InsertAfter(GetElementById l.Value.Id, n); newElement.Update(token, n, state) ] )
                                            code.Add expr
                                                

                                | Some (_,r) ->
                                    let r = r.Value.Id
                                    let expr = create newElement (fun n -> [ InsertBefore(GetElementById r, n); newElement.Update(token, n, state) ] )
                                    code.Add expr
                                                    
                                                    
                
        for i in toUpdate do
            let v = { name = i.Id }
            let expr = 
                JSExpr.Let(
                    v, JSExpr.GetElementById i.Id,
                    i.Update(token, Var v, state)
                )
            code.Add expr

        JSExpr.Sequential (CSharpList.toList code)

and SubAppUpdater<'model, 'msg, 'outermsg>(container : DomNode<'outermsg>, app : IApp<'model, 'msg>, request : Request, id : string) =
    inherit AbstractUpdater<'outermsg>()
    
    let mutable old = Set.empty
    let mutable mapp : Option<MutableApp<_,_> * DomUpdater<'msg> * UpdateState<'msg>> = None


    let update key (client : Guid) (bla : string) (args : list<string>) =
        match mapp with
            | Some (mapp, updater, myState) ->
                match myState.handlers.TryGetValue key with
                    | (true, handler) ->
                        let messages = handler client bla args
                        mapp.update client messages
                        let model = mapp.model.GetValue()
                        messages |> Seq.collect (fun msg -> app.ToOuter<'outermsg>(model, msg))

                    | _ ->
                        Seq.empty
            | _ ->
                Seq.empty

    override x.PerformUpdate(token, self, state : UpdateState<'outermsg>) =
        let mapp, updater, myState =
            match mapp with
                | Some a -> a
                | None ->
                    let m = app.Start()
                    
                    let innerAtt = 
                        container.Attributes |> AttributeMap.choose (fun key value ->
                            match value with
                                | AttributeValue.String a -> Some (AttributeValue.String a)
                                | _ -> None
                        )

                    let parent = DomNode<_>(container.Tag, container.Namespace, innerAtt, DomContent.Children(AList.ofList [m.ui]))
                    let updater = DomUpdater(parent, request, id)
                    
                    let myState =
                        {
                            scenes              = state.scenes
                            handlers            = Dictionary()
                            references          = state.references
                            activeChannels      = state.activeChannels
                        }

                    mapp <- Some (m,updater, myState)
                    (m, updater, myState)
  
        let mutable n = Set.empty
        let mutable deleted = old
        //let mutable old = myState.handlers.Keys |> Set.ofSeq
        let updateCode = updater.Update(token, self, myState)

        for KeyValue(key, handler) in myState.handlers do
            n <- Set.add key n
            deleted <- Set.remove key deleted
            state.handlers.[key] <- update key

        for key in deleted do
            state.handlers.Remove key |> ignore
        old <- n

        updateCode
        
    override x.Destroy(state, self) =
        match mapp with
            | Some (app, updater, myState) ->
                
                let code = updater.Destroy(myState, self)
                app.shutdown()
                for key in myState.handlers.Keys do
                    state.handlers.Remove key |> ignore
                myState.handlers.Clear()
                old <- Set.empty
                mapp <- None
                code
            | None ->
                JSExpr.Nop
    

and DomUpdater<'msg>(ui : DomNode<'msg>, request : Request, id : string) =
    inherit AbstractUpdater<'msg>()


    let rAtt = ui.Attributes.GetReader()
    let rContent = 
        match ui.Content with
            | Scene(scene, cam) -> SceneUpdater(ui, id, scene, cam) :> IUpdater<_>
            | Children children -> ChildrenUpdater(id, AList.map (fun c -> DomUpdater(c,request)) children) :> IUpdater<_>
            | Text text -> TextUpdater text :> IUpdater<_>
            | Empty -> EmptyUpdater.Instance
            | Page p -> 
                let updater = DomUpdater(p request, request)
                updater :> IUpdater<_>
            | SubApp a -> 
                a.Visit {
                    new IAppVisitor<IUpdater<'msg>> with
                        member x.Visit(app : IApp<'a, 'b>) =
                            SubAppUpdater<'a, 'b, 'msg>(ui, app, request, id) :> IUpdater<'msg>
                }

    let mutable initial = true


    static let toAttributeValue (state : UpdateState<'msg>) (id : string) (name : string) (v : AttributeValue<'msg>) =
        match v with
            | AttributeValue.String str -> 
                Some str

            | AttributeValue.Event desc ->
                let code = Event.toString id name desc
                state.handlers.[(id, name)] <- desc.serverSide
                Some code

            //| AttributeValue.RenderControlEvent _ ->
            //    None

    static let destroyAttribute (state : UpdateState<'msg>) (id : string) (name : string) =
        state.handlers.Remove (id,name) |> ignore
        true


    member x.Tag : string = ui.Tag
    member x.Namespace : Option<string> = ui.Namespace
    member x.Id : string = id

    override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
        for (name, v) in rAtt.State do
            match v with
                | AttributeValue.Event _ -> state.handlers.Remove(id, name) |> ignore
                | _ -> ()

        for (name,cb) in Map.toSeq ui.Callbacks do
            state.handlers.Remove (id,name) |> ignore
                
        for (name, _) in Map.toSeq ui.Channels do
            match state.activeChannels.TryRemove ((id, name)) with
                | (true, r) -> r.Dispose()
                | _ -> ()


        rAtt.Dispose()
        match ui.Shutdown with
            | Some shutdown ->
                JSExpr.Sequential [
                    rContent.Destroy(state, self)
                    Raw (shutdown id)
                ]
            | None ->
                rContent.Destroy(state, self)
                    

    override x.PerformUpdate(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
        let code = List()


        let attOps = rAtt.GetOperations(token)
        for (name, op) in attOps do
            match op with
                | ElementOperation.Set v -> 
                    match toAttributeValue state id name v with
                        | Some value -> 
                            code.Add(SetAttribute(self, name, value))
                        | None ->
                            ()

                | ElementOperation.Remove ->
                    if destroyAttribute state id name then
                        code.Add(RemoveAttribute(self, name))

        code.Add (rContent.Update(token, self, state))

                
        if initial then
            initial <- false

            for (name,cb) in Map.toSeq ui.Callbacks do
                state.handlers.[(id,name)] <- fun _ _ v -> Seq.delay (fun () -> Seq.singleton (cb v))

            for r in ui.Required do
                state.references.[(r.name, r.kind)] <- r

            match ui.Boot with
                | Some getBootCode ->
                    let boot = getBootCode id
                    if Map.isEmpty ui.Channels then
                        code.Add(Raw boot)
                    else 
                        for (name, c) in Map.toSeq ui.Channels do
                            state.activeChannels.[(id,name)] <- c.GetReader()

                        let prefix = 
                            ui.Channels 
                            |> Map.toList
                            |> List.map (fun (name, _) -> 
                                sprintf "var %s = aardvark.getChannel(\"%s\", \"%s\");" name id name
                            ) 
                            |> String.concat "\r\n"
                        
                        code.Add(Raw (prefix + "\r\n" + boot))

                | None ->
                    ()


        JSExpr.Sequential (CSharpList.toList code)



    new(ui : DomNode<'msg>, request : Request) = DomUpdater<'msg>(ui, request, newId())



[<AutoOpen>]
module ``Extensions for Node`` =
    type DomNode<'msg> with
        member x.NewUpdater(request : Request) =
            match x.Content with
                | Page f -> 
                    let p = f request
                    let mutable p = p
                    match x.Boot with
                        | Some boot -> p <- p.AddBoot boot
                        | _ -> ()
                    match x.Shutdown with
                        | Some boot -> p <- p.AddShutdown boot
                        | _ -> ()
                    match x.Required with
                        | [] -> ()
                        | r -> p <- p.AddRequired(r)
                    p.NewUpdater(request)
                | _ -> 
                    let updater = 
                        if x.Tag = "body" then DomUpdater<'msg>(x,request) :> IUpdater<_>
                        else
                            Log.warn "[Aardvark.UI.Dom] auto generating body. consider adding an explicit body to your view function"
                            DomUpdater<'msg>(DomNode<'msg>("body", None, AttributeMap.empty, Children (AList.single x)), request) :> IUpdater<_>
                    updater
