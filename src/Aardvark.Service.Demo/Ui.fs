namespace Aardvark.UI

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
    //| Insert of parent : Expr * before : Option<Expr> * e : Expr


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

type ReaderContent<'msg> =
    | RChildren of IListReader<UiReader<'msg>>
    | RText of IMod<string>
    | RScene of (IRenderControl -> IRenderTask)

and UiReader<'msg>(node : Ui<'msg>) =
    inherit AdaptiveObject()

    static let mutable currentId = 0
    static let newId() =
        let id = Interlocked.Increment(&currentId)
        "n" + string id
        
    let id = newId()
    let attributes = node.Attributes.ASet.GetReader()
    let content = 
        match node.Content with
            | Children children -> 
                (AList.map UiReader children).GetReader() |> RChildren

            | Text m -> 
                RText m 

            | Scene s ->
                RScene s

    let mutable lastContent = ""

    let greatestSmaller (c : 'k) (s : seq<'k * 'a>) =
        let mutable res = None
        for (k,v) in s do
            if k < c then
                match res with
                    | Some(o,_) when o >= k ->
                        ()
                    | _ -> 
                        res <- Some (k,v)

        res

    let rec splitDeltas (d : list<Delta<'a>>) =
        match d with
            | [] -> [], []
            | d :: rest -> 
                let added, removed = splitDeltas rest
                match d with
                    | Add v -> v :: added, removed
                    | Rem v -> added, v :: removed

    let findNeighbours (a : 'a) (set : SortedSetExt<'a>) =
        let mutable l = Optional.None
        let mutable s = Optional.None
        let mutable r = Optional.None
        set.FindNeighbours(a, &l, &s, &r)

        let l = if l.HasValue then Some l.Value else None
        let s = if s.HasValue then Some s.Value else None
        let r = if r.HasValue then Some r.Value else None

        l, s, r

    static let cmpState =
        { new IComparer<ISortKey * UiReader<'msg> * bool> with
            member x.Compare((l,_,_), (r,_,_)) =
                compare l r
        }

    static let cmpDelta =
        { new IComparer<ISortKey * UiReader<'msg>> with
            member x.Compare((l,_), (r,_)) =
                compare l r
        }


    member x.Id = id
    member x.Tag = node.Tag


    member x.Destroy(state : UpdateState<'msg>) =
        for (key, v) in attributes.Content do
            match v with
                | Event _ ->
                    state.handlers.Remove((id, key)) |> ignore
                | _ ->
                    ()

        attributes.Dispose()

        match content with
            | RScene _ ->
                state.scenes.Remove id |> ignore
            | RChildren children ->
                for (_,c) in children.Content.All do
                    c.Destroy(state)

                children.Dispose()
            | _ ->
                lastContent <- ""

    member x.Update(caller : IAdaptiveObject, self : Expr, state : UpdateState<'msg>) =
        x.EvaluateIfNeeded caller self (fun () ->
            let mutable e = self


            // process attribute changes
            let values = Dictionary()
            let removed = Dictionary()
            for d in attributes.GetDelta x do
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
        
        
            match content with
                | RText t ->
                    let t = t.GetValue x
                    if t <> lastContent then
                        e <- HtmlSet(e, t)
                        lastContent <- t

                    e
                    
                | RScene sg ->
                    state.scenes.[id] <- sg
                    e
                    
                | RChildren reader ->
                    let self = { name = "_" + id }


                    let oldContent = Dictionary.ofSeq reader.Content.All
                    let added, removed = reader.GetDelta x |> splitDeltas
                    for (k,_) in removed do oldContent.Remove k |> ignore

                    let sorted = SortedSetExt<ISortKey * UiReader<'msg> * bool>(cmpState)
                    for (KeyValue(k,v)) in oldContent do sorted.Add(k, v, false) |> ignore

                    let sortedAdds = SortedSetExt<ISortKey * UiReader<'msg>>(cmpDelta)
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
                                Let(var, Create(n.Tag, n.Id), Sequential [f (Var var); n.Update(x, Var var, state)])
          
                            
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

                    let updates =
                        oldContent |> Dictionary.toList |> List.map (fun (_,n) ->
                            n.Update(x, Element n.Id, state)
                        )

//                    let inner =
//                        [
//                            let mutable prev = None
//                            for (KeyValue(k,v)) in sorted do
//                                if isNew v then
//                                    match prev with
//                                        | Some prev ->
//                                            let prev = 
//                                                if isNew prev then
//                                                    Var { name = "_" + prev.Id }
//                                                else
//                                                    Element prev.Id
//
//                                            let n = { name = "_" + v.Id }
//                                            yield Let(n, Create(v.Tag, v.Id), [Insert(Var self, Some prev, Var n); v.Update2(x, Var n, state)])
//
//                                        | None ->
//                                            let n = { name = "_" + v.Id }
//                                            yield Let(n, Create(v.Tag, v.Id), [Insert(Var self, None, Var n); v.Update2(x, Var n, state)])
//                                else
//                                    yield v.Update2(x, Element v.Id, state)
//
//                                prev <- Some v
//                        ]

                    Let(self, e, Sequential (removals @ additions @ updates))



        
        )


[<AutoOpen>]
module ``Extensions for Node`` =
    type Ui<'msg> with
        member x.GetReader() =
            UiReader(x)

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
                        Log.line "empty update"
                    else
                        let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                        lock runtime (fun () -> 
                            Log.start "update"
                            for l in lines do Log.line "%s" l
                            Log.stop()
                        )
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

module Bla =
    open Aardvark.Base.Rendering

    let test =
        let initial = List.init 20 (fun i -> System.String [|'A' + char i|])
        let elements = COrderedSet.ofList initial
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
                            AMap.ofList ["onclick", Event([], fun _ () -> elements.InsertAfter(c, c + "1") |> ignore)],
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
        



