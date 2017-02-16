namespace Aardvark.UI

open System
open System.Text
open System.Collections.Generic
open System.Threading
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

open Suave
open Suave.Http
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Aardvark.Service
open Aardvark.Application
open Aardvark.Rendering.Text

type AttributeValue<'msg> =
    | Event of list<string> * (list<string> -> 'msg)
    | Value of string

and UiContent<'msg> =
    | Children of alist<Ui<'msg>>
    | Text of IMod<string>
    | Scene of (IRenderControl -> IRenderTask)

and Ui<'msg>(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : UiContent<'msg>) =
    member x.Tag = tag
    member x.Attributes = attributes
    member x.Content = content

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : alist<Ui<'msg>>) =
        Ui(tag, attributes, Children content)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : IMod<string>) =
        Ui(tag, attributes, Text content)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, sg : IRenderControl -> IRenderTask) =
        Ui(tag, attributes, Scene sg)



[<AutoOpen>]
module ``Extensions for StringBuilder`` = 
    type StringBuilder with
        member x.append fmt = Printf.kprintf (fun str -> x.AppendLine str |> ignore) fmt

type UpdateState<'msg> =
    {
        handlers    : Dictionary<string * string, list<string> -> 'msg>
        scenes      : Dictionary<string, IRenderControl -> IRenderTask>
    }

type Var = { name : string }

type Expr =
    | Body
    | Var of Var
    | Element of id : string
    | Create of tag : string * id : string
    | AttributeSet of target : Expr * name : string * value : string
    | AttributeRem of target : Expr * name : string
    | HtmlSet of target : Expr * content : string

    | Sequential of list<Expr>
    | Let of Var * Expr * Expr
    | Remove of Expr
    | Prepend of parent : Expr * e : Expr
    | Append of parent : Expr * e : Expr
    | InsertAfter of before : Expr * e : Expr
    | InsertBefore of after : Expr * e : Expr

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Expr =
    open System
    open Aardvark.Base.Monads.State

    let rec private eliminateDeadCodeS (e : Expr) =
        state {
            match e with
                | Var v ->
                    do! State.modify (Set.add v)
                    return e

                | Body | Element _ | Create _  ->
                    return e

                | AttributeSet(t,n,v) ->
                    let! t = eliminateDeadCodeS t
                    return AttributeSet(t, n, v)

                | AttributeRem(t,n) ->
                    let! t = eliminateDeadCodeS t
                    return AttributeRem(t, n)
                
                | HtmlSet(t,h) ->
                    let! t = eliminateDeadCodeS t
                    return HtmlSet(t, h)
                   
                | Sequential l ->
                    let! l = l |> List.rev |> List.mapS eliminateDeadCodeS |> State.map List.rev
                    return Sequential l
                    
                | Let(v,e,b) ->
                    let! b = eliminateDeadCodeS b
                    let! used = State.get
                    let! e = eliminateDeadCodeS e

                    if Set.contains v used then
                        return Let(v, e, b)
                    else
                        return Sequential [e; b]

                | Remove(e) ->
                    let! e = eliminateDeadCodeS e
                    return Remove e

                | Prepend(p,e) ->
                    let! p = eliminateDeadCodeS p
                    let! e = eliminateDeadCodeS e
                    return Prepend(p, e)

                | Append(p,e) ->
                    let! p = eliminateDeadCodeS p
                    let! e = eliminateDeadCodeS e
                    return Append(p, e)

                | InsertAfter(b,e) ->
                    let! e = eliminateDeadCodeS e
                    let! b = eliminateDeadCodeS b
                    return InsertAfter(b, e)

                | InsertBefore(a,e) ->
                    let! e = eliminateDeadCodeS e
                    let! a = eliminateDeadCodeS a
                    return InsertBefore(a, e)
                    
        }

    let private unescape (str : string) =
        str.Replace("\"", "\\\"")

    let eliminateDeadCode (e : Expr) =
        let mutable used = Set.empty
        eliminateDeadCodeS(e).Run(&used)

    let rec private print (isStatement : bool) (e : Expr) =
        let ret str =
            if isStatement then Some (str + ";")
            else Some str

        match e with
            | Body -> 
                if isStatement then None
                else Some "$(document.body)"

            | Var { name = n } -> 
                if isStatement then None
                else Some n

            | Element id -> 
                if isStatement then None
                else String.Format("$(\"#{0}\")", id) |> Some

            | Create (tag, id) -> 
                if isStatement then None
                else String.Format("$(\"<{0}/>\").attr(\"id\", \"{1}\")", tag, id) |> Some

            | AttributeSet(target, name, value) ->
                let target = print false target |> Option.get
                String.Format("{0}.attr(\"{1}\", \"{2}\")", target, name, unescape value) |> ret

            | AttributeRem(target, name) ->
                let target = print false target |> Option.get
                String.Format("{0}.removeAttr(\"{1}\")", target, name) |> ret

            | HtmlSet(target, html) ->
                let target = print false target |> Option.get
                String.Format("{0}.html(\"{1}\")", target, unescape html) |> ret

            | Let(v, e, b) ->
                if not isStatement then
                    failwith "[JS] variable bindings cannot be expressions in JS"

                match print true b with
                    | Some b -> 
                        let e = print false e |> Option.get
                        String.Format("var {0} = {1};\r\n{2}", v.name, e, b) |> ret
                    | None ->
                        print true e

            | Sequential l ->
                let l = l |> List.choose (print true)
                match l with
                    | [] -> None
                    | _ -> String.concat "\r\n" l |> Some

            | Remove(e) ->
                let e = print false e |> Option.get
                String.Format("{0}.remove()", e) |> ret

            | Append(p, e) ->
                let e = print false e |> Option.get
                let p = print false p |> Option.get
                String.Format("{0}.append({1})", p, e) |> ret
                
            | Prepend(p, e) ->
                let e = print false e |> Option.get
                let p = print false p |> Option.get
                String.Format("{0}.prepend({1})", p, e) |> ret
                
            | InsertAfter(b, e) ->
                let e = print false e |> Option.get
                let b = print false b |> Option.get
                String.Format("{0}.insertAfter({1})", e, b) |> ret
                
            | InsertBefore(a, e) ->
                let e = print false e |> Option.get
                let a = print false a |> Option.get
                String.Format("{0}.insertBefore({1})", e, a) |> ret
    
    let toString (e : Expr) =
        match print true e with
            | Some s -> s
            | None -> ""

type IUiUpdate<'msg> =
    inherit IAdaptiveObject
    abstract member Tag : string
    abstract member Id : string
    abstract member Update : IAdaptiveObject * Expr * UpdateState<'msg> -> Expr
    abstract member Destroy : UpdateState<'msg> -> unit


type ReaderContent<'msg> =
    | RChildren of IListReader<IUiUpdate<'msg>>
    | RText of IMod<string>
    | RScene of (IRenderControl -> IRenderTask)

[<AbstractClass>]
type AbstractUiUpdate<'msg>(id : string, n : Ui<'msg>) =
    inherit AdaptiveObject()
    member x.Id = id
    member x.Tag = n.Tag

    abstract member Update : Expr * UpdateState<'msg> -> Expr
    abstract member Destroy : UpdateState<'msg> -> unit
    
    member x.Update(caller : IAdaptiveObject, self : Expr, state : UpdateState<'msg>) = 
        x.EvaluateIfNeeded caller self (fun () ->
            x.Update(self, state)
        )

    interface IUiUpdate<'msg> with
        member x.Id = id
        member x.Tag = n.Tag
        member x.Update(caller, self, state) = x.Update(caller, self, state)
        member x.Destroy(state) = x.Destroy(state)

type AttributeUpdate<'msg>(id : string, n : Ui<'msg>) =
    inherit AbstractUiUpdate<'msg>(id, n)

    let reader = n.Attributes.ASet.GetReader()

    override x.Destroy(state : UpdateState<'msg>) =
        for (key, v) in reader.Content do
            match v with
                | Event _ -> state.handlers.Remove((id, key)) |> ignore
                | _ ->  ()
        reader.Dispose()

    override x.Update(self : Expr, state : UpdateState<'msg>) =
        let mutable e = self
        // process attribute changes
        let values = Dictionary()
        let removed = Dictionary()
        for d in reader.GetDelta x do
            match d with
                | Add(key, value) ->
                    removed.Remove key |> ignore
                    values.[key] <- value

                | Rem(key, value) ->
                    if not (values.ContainsKey key) then
                        removed.[key] <- value

        for (key, value) in Dictionary.toSeq values do
            match value with
                | Value str -> 
                    e <- AttributeSet(e, key, str)
                  
                | Event(args, f) ->
                    let args = sprintf "'%s'" id :: sprintf "'%s'" key :: args
                    let value = String.concat ", " args |> sprintf "aardvark.processEvent(%s)"
                    e <- AttributeSet(e, key, value)
                    state.handlers.[(id, key)] <- f

        for (key, value) in Dictionary.toSeq removed do
            e <- AttributeRem(e, key)
            match value with
                | Event _ -> state.handlers.Remove((id, key)) |> ignore
                | _ -> ()

        e

type ChildrenUpdate<'msg>(id : string, n : Ui<'msg>, l : alist<IUiUpdate<'msg>>) =
    inherit AbstractUiUpdate<'msg>(id, n)
    let reader = l.GetReader()

    static let splitDeltas (d : list<Delta<'a>>) =
        let rec splitDeltas (add : list<'a>) (rem : list<'a>) (d : list<Delta<'a>>) =
            match d with
                | [] -> add, rem
                | d :: rest -> 
                    match d with
                        | Add v -> splitDeltas (v :: add) rem rest
                        | Rem v -> splitDeltas add (v :: rem) rest
        splitDeltas [] [] d

    static let cmpState =
        { new IComparer<ISortKey * IUiUpdate<'msg> * bool> with
            member x.Compare((l,_,_), (r,_,_)) =
                compare l r
        }

    static let cmpDelta =
        { new IComparer<ISortKey * IUiUpdate<'msg>> with
            member x.Compare((l,_), (r,_)) =
                compare l r
        }

    static let findNeighbours (a : 'a) (set : SortedSetExt<'a>) =
        let mutable l = Optional.None
        let mutable s = Optional.None
        let mutable r = Optional.None
        set.FindNeighbours(a, &l, &s, &r)

        let l = if l.HasValue then Some l.Value else None
        let s = if s.HasValue then Some s.Value else None
        let r = if r.HasValue then Some r.Value else None

        l, s, r

    let mutable innerUpdates : Option<list<IUiUpdate<'msg> * Expr>> = None

    member x.UpdateInner(caller : IAdaptiveObject, state : UpdateState<'msg>) =
        match innerUpdates with
            | None ->
                reader.Content.All |> Seq.toList |> List.map (fun (_,n) ->
                    n.Update(caller, Element n.Id, state)
                )
            | Some inner ->  
                let res = 
                    inner |> List.map (fun (n,a) ->
                        n.Update(caller, a, state)
                    )
                innerUpdates <- None
                res

    override x.Destroy(state) =
        for (_,e) in reader.Content.All do
            e.Destroy state
        reader.Dispose()

    override x.Update(self : Expr, state : UpdateState<'msg>) =
        let mutable inner = []
        let mutable e = self
        let self = { name = "_" + id }

        let oldContent = Dictionary.ofSeq reader.Content.All
        let added, removed = reader.GetDelta x |> splitDeltas
        for (k,_) in removed do oldContent.Remove k |> ignore

        let sorted = SortedSetExt<ISortKey * IUiUpdate<'msg> * bool>(cmpState)
        for (KeyValue(k,v)) in oldContent do sorted.Add(k, v, false) |> ignore

        let sortedAdds = SortedSetExt<ISortKey * IUiUpdate<'msg>>(cmpDelta)
        for a in added do sortedAdds.Add a |> ignore

        let removals =
            removed |> List.map (fun (_,n) ->
                Remove(Element n.Id)
            )

        let additions =
            sortedAdds |> Seq.toList |> List.map (fun (k,n) ->
                let insert (f : Expr -> Expr) =
                    sorted.Add(k,n,true) |> ignore
                    let var = { name = "_" + n.Id }

                    inner <- (n, Var var) :: inner

                    Let(var, Create(n.Tag, n.Id), f (Var var) )
          
                            
                let prev, ex, next = findNeighbours (k,n,true) sorted
                if Option.isSome ex then
                    failwithf "[JS] duplicate entry %A" (k,n)

                match prev, next with
                    | _, None ->
                        // last element
                        insert (fun s -> Append(Var self, s))

                    | None, _ ->   
                        // first element
                        insert (fun s -> Prepend(Var self, s))

                    | Some (pk, pn, false), _ ->
                        // prev not new
                        insert (fun s -> InsertAfter(Element pn.Id, s))

                    | _, Some (nk, nn, false) ->
                        // next not new
                        insert (fun s -> InsertBefore(Element nn.Id, s))

                    | Some (_,_,true), Some (_,_,true) ->
                        failwith "should be impossible"
            )

        for (KeyValue(_,n)) in oldContent do
            inner <- (n, Element n.Id) :: inner

        innerUpdates <- Some inner

        Let(self, e, Sequential (removals @ additions))

type ContentUpdate<'msg>(id : string, n : Ui<'msg>, content : IMod<string>) =
    inherit AbstractUiUpdate<'msg>(id, n)

    let mutable old = ""

    override x.Destroy(state : UpdateState<'msg>) =
        old <- ""

    override x.Update(self : Expr, state : UpdateState<'msg>) =
        let v = content.GetValue x
        if old <> v then
            old <- v
            HtmlSet(self, v)
        else
            self
 
type SceneUpdate<'msg>(id : string, n : Ui<'msg>, content : IRenderControl -> IRenderTask) =
    inherit AbstractUiUpdate<'msg>(id, n)
    let mutable content = content

    override x.Destroy(state : UpdateState<'msg>) =
        state.scenes.Remove id |> ignore
        content <- unbox

    override x.Update(self : Expr, state : UpdateState<'msg>) =
        state.scenes.[id] <- content
        self
    
type UiUpdate<'msg> private (id : string, node : Ui<'msg>) =
    inherit AbstractUiUpdate<'msg>(id, node)

    static let mutable currentId = 0
    static let newId() =
        let id = Interlocked.Increment(&currentId)
        "n" + string id
   
    let att = 
        AttributeUpdate(id, node) :> IUiUpdate<_>

    let contentUpdate, updateInner = 
        match node.Content with
            | Scene sg -> 
                let u = SceneUpdate(id, node, sg) :> IUiUpdate<_>
                u, fun _ -> []

            | Text t -> 
                let u = ContentUpdate(id, node, t) :> IUiUpdate<_>
                u, fun _ -> []
            | Children l -> 
                let u = ChildrenUpdate(id, node, l |> AList.map (fun n -> UiUpdate<'msg>(n) :> IUiUpdate<_>))
                u :> IUiUpdate<_>, u.UpdateInner

    override x.Destroy(state : UpdateState<'msg>) =
        att.Destroy(state)
        contentUpdate.Destroy(state)

    override x.Update(self : Expr, state : UpdateState<'msg>) =
        let self = att.Update(x, self, state)
        let self = contentUpdate.Update(x, self, state)

        let inner = updateInner(x, state)
        match inner with
            | [] -> self
            | l -> Sequential (self :: inner)



    new(n : Ui<'msg>) = UiUpdate(newId(), n)   

[<AutoOpen>]
module ``Extensions for Node`` =
    type Ui<'msg> with
        member x.GetReader() =
            UiUpdate(x) :> IUiUpdate<_>

type App<'model, 'mmodel, 'msg> =
    {
        view : 'mmodel -> Ui<'msg>
        update : 'model -> 'msg -> 'model
        initial : 'model
    }

module Ui = 

    let private main =
        String.concat "\r\n" [
            "<html>"
            "   <head>"
            "       <title>Aardvark rocks \\o/</title>"
            "       <script src=\"https://code.jquery.com/jquery-3.1.1.min.js\"></script>"
            "       <script src=\"https://cdnjs.cloudflare.com/ajax/libs/jquery-resize/1.1/jquery.ba-resize.min.js\"></script>"
            "       <script src=\"/aardvark.js\"></script>"
            "       <script>"
            "           var aardvark = {};"
            "           function getUrl(proto, subpath) {"
            "               var l = window.location;"
            "               var path = l.pathname;"
            "               if(l.port === \"\") {"
            "                   return proto + l.hostname + path + subpath;"
            "               }"
            "               else {"
            "                   return proto + l.hostname + ':' + l.port + path + subpath;"
            "               }"
            "           }"
            "           var url = getUrl('ws://', 'events');"
            "           var eventSocket = new WebSocket(url);"
            ""
            "           aardvark.processEvent = function () {"
            "               console.warn(\"websocket not opened yet\");"
            "           }"
            ""
            "           eventSocket.onopen = function () {"
            "               aardvark.processEvent = function () {"
            "                   var sender = arguments[0];"
            "                   var name = arguments[1];"
            "                   var args = [];"
            "                   for (var i = 2; i < arguments.length; i++) {"
            "                       args.push(JSON.stringify(arguments[i]));"
            "                   }"
            "                   var message = JSON.stringify({ sender: sender, name: name, args: args });"
            "                   eventSocket.send(message);"
            "               }"
            "           };"
            ""
            "           eventSocket.onmessage = function (m) {"
            "              eval(m.data);"
            "           };"
            ""
            "       </script>"
            "   </head>"
            "   <body>"
            "   </body>"
            "</html>"
        ]

    type WebSocket with
        member x.readMessage() =
            socket {
                let! (t,d,fin) = x.read()
                if fin then 
                    return (t,d)
                else
                    let! (_, rest) = x.readMessage()
                    return (t, Array.append d rest)
            }

    let start (runtime : IRuntime) (port : int) (perform : 'action -> unit) (ui : Ui<'action>) =
        let state =
            {
                handlers = Dictionary()
                scenes = Dictionary()
            }

        let events (s : WebSocket) (ctx : HttpContext) =
            let reader = ui.GetReader()
            let mutable initial = true
            let performUpdate (s : WebSocket) =
                lock reader (fun () ->
                    let code = reader.Update(null, Body, state) |> Expr.eliminateDeadCode |> Expr.toString
                    let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]

                    if code = "" then
                        ()
                        //Log.line "empty update"
                    else
//                        let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
//                        lock runtime (fun () -> 
//                            Log.start "update"
//                            for l in lines do Log.line "%s" l
//                            Log.stop()
//                        )
                        let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes(code))) true |> Async.RunSynchronously
                        match res with
                            | Choice1Of2 () ->
                                ()
                            | Choice2Of2 err ->
                                failwithf "[WS] error: %A" err
                )
            
            let pending = MVar.create()
            let subsription = reader.AddMarkingCallback(fun () -> MVar.put pending ())

            
            let mutable running = true
            let runner =
                async {
                    while running do
                        let! _ = MVar.takeAsync pending
                        performUpdate s
                }

            Async.Start runner

            socket {
                try
                    while running do
                        let! msg = s.readMessage()
                        match msg with
                            | (Opcode.Text, str) ->
                                let event : Event = Pickler.json.UnPickle str

                                match state.handlers.TryGetValue ((event.sender, event.name)) with
                                    | (true, f) ->
                                        let action = f (Array.toList event.args)
                                        perform action
                                    | _ ->
                                        ()

                            | (Opcode.Close,_) ->
                                running <- false
                        
                            | _ ->
                                ()
                finally
                    reader.Destroy state
            }

        let parts = 
            [
                GET >=> path "/main/" >=> OK main
                GET >=> path "/main/events" >=> handShake events
            ]

        Server.start runtime port parts (fun id ctrl ->
            match state.scenes.TryGetValue id with
                | (true, f) -> ctrl |> f |> Some
                | _ -> None
        )



    type ConcreteMApp<'model, 'mmodel, 'msg> =
        {
            cview : 'mmodel -> Ui<'msg>
            cupdate : 'model -> 'msg -> 'model
            cinit : 'model -> ReuseCache -> 'mmodel
            capply : 'mmodel -> ReuseCache -> 'model -> unit
            cinitial : 'model
        }


    let startElm (runtime : IRuntime) (port : int) (app : ConcreteMApp<'m, 'mm, 'msg>) =
        let cache = ReuseCache()
        let imut = Mod.init app.cinitial
        let mut = app.cinit app.cinitial cache
        let perform (msg : 'msg) =
            let newImut = app.cupdate imut.Value msg
            transact (fun () ->
                imut.Value <- newImut
                app.capply mut cache newImut
            )

        let view = app.cview mut

        start runtime port perform view

    let inline startElm'<'model, 'msg, 'mmodel when 'model : (member ToMod : ReuseCache -> 'mmodel) and 'mmodel : (member Apply : 'model * ReuseCache -> unit)> (runtime : IRuntime) (port : int) (app : App<'model, 'mmodel, 'msg>) =   
        let capp = 
            {
                cview = app.view
                cupdate = app.update
                cinit = fun m c -> (^model : (member ToMod : ReuseCache -> 'mmodel) (m, c))
                capply = fun mm c m -> (^mmodel : (member Apply : 'model * ReuseCache -> unit) (mm, m, c))
                cinitial = app.initial
            }

        startElm runtime port capp


module Bla =
    open Aardvark.Base.Rendering

    module TestApp =
        
        type Model =
            {
                lastName : Option<string>
                elements : pset<string>
            }

            member x.ToMod(cache : ReuseCache) =
                {
                    mlastName = Mod.init x.lastName
                    melements = ResetSet<string>(x.elements)
                }

        and MModel =
            {
                mlastName : ModRef<Option<string>>
                melements : ResetSet<string>
            }

            member x.Apply(m : Model, cache : ReuseCache) =
                x.mlastName.Value <- m.lastName
                x.melements.Update(m.elements)

        type Message =
            | AddButton of string

        let update (m : Model) (msg : Message) =
            match msg with
                | AddButton str -> { m with lastName = Some str; elements = PSet.add str m.elements }

        let view (m : MModel) =
            Ui(
                "div",
                AMap.empty,
                AList.ofList [
                    Ui(
                        "div",
                        AMap.empty,
                        m.melements |> ASet.sort |> AList.map (fun str ->
                            Ui(
                                "button",
                                AMap.ofList ["onclick", Event([], fun _ -> AddButton (Guid.NewGuid() |> string))],
                                Mod.constant str
                            )
                        )
                    )

                    Ui(
                        "div",
                        AMap.ofList ["class", Value "aardvark"; "style", Value "height: 600px; width: 800px"],
                        fun (ctrl : IRenderControl) ->
                            
                            let value =
                                m.mlastName |> Mod.map (function Some str -> str | None -> "yeah")

                            let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
                            let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                            let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time

                            let sg = 
                                Sg.markdown MarkdownConfig.light value
                                    |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                                    |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

                            ctrl.Runtime.CompileRender(ctrl.FramebufferSignature, sg)

                    )

                ]
            )

        let initial =
            {
                lastName = None
                elements = PSet.ofList ["A"; "B"]
            }

        let start (runtime : IRuntime) (port : int) =
            Ui.startElm' runtime port {
                view = view
                update = update
                initial = initial
            }


    let test =
        let initial = List.init 20 (fun i -> System.String [|'A' + char i|])
        let elements = COrderedSet.ofList initial
        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        let view = Mod.init view

        Ui(
            "div",
            AMap.empty,
            AList.ofList [
                Ui(
                    "div",
                    AMap.empty,
                    elements |> AList.map (fun c ->
                        Ui(
                            "button",
                            AMap.ofList ["onclick", Event([], fun _ () -> [1 .. 50] |> List.iter (fun i -> elements.InsertAfter(c,string elements.Count)|> ignore))],
                            AList.ofList [ Ui("span", AMap.empty, Mod.constant c) ]
                        )
                    )   
                )

                Ui(
                    "div",
                    AMap.ofList ["class", Value "aardvark"; "style", Value "width: 800px; height: 600px"],
                    fun (ctrl : IRenderControl) ->
                        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
                        let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                        let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time

                        let sg =
                            Sg.box' C4b.Red (Box3d(-V3d.III, V3d.III))
                                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
                                |> Sg.shader {
                                    do! DefaultSurfaces.trafo
                                    do! DefaultSurfaces.simpleLighting
                                }

                        ctrl.Runtime.CompileRender(ctrl.FramebufferSignature, sg)
                    
                )


            ]
        )

    let runTest (runtime : IRuntime) (port : int) =
        Ui.start runtime port transact test
        



