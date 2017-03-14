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



type UpdateState<'msg> =
    {
        handlers    : Dictionary<string * string, list<string> -> 'msg>
        scenes      : Dictionary<string, IRenderControl -> IRenderTask>
    }

type Var = { name : string }

type Expr =
    | Body
    | CreateElement of tag : string
    | SetAttribute of target : Expr * name : string * value : string
    | RemoveAttribute of target : Expr * name : string

    | Remove of target : Expr
    | InnerHTML of target : Expr * html : string 

    | Replace of oldElement : Expr * newElement : Expr
    | AppendChild  of parent : Expr * inner : Expr
    | InsertBefore of reference : Expr * inner : Expr // in html arguments switched
    | NextElement  of reference : Expr // implicitly option

    | Sequential of list<Expr>
    | GetElementById of string
    | Let of Var * Expr * Expr
    | Var of Var
    | Nop

module Expr =
    let escape (str : string) =
        str.Replace("\"", "\\\"")

    let force (o : Option<'a>) =
        match o with
            | None -> failwith "internal error: -30025 (adornment-factory proxy missing)"
            | Some v -> v

    let rec toString (e : Expr) =
        match e with
            | Body ->
                Some "document.body"

            | Nop ->
                None

            | CreateElement(tag) ->
                sprintf "document.createElement(\"%s\")" tag |> Some

            | SetAttribute(t, name, value) ->
                let t = toString t |> force
                sprintf "%s.setAttribute(\"%s\", \"%s\");" t name (escape value) |> Some

            | RemoveAttribute(t, name) ->
                let t = toString t |> force
                sprintf "%s.removeAttribute(\"%s\");" t name |> Some

            | GetElementById(id) ->
                sprintf "document.getElementById(\"%s\")" id |> Some

            | Let(var, value, body) ->
                match toString body  with
                    | Some body -> sprintf "var %s = %s;\r\n%s" var.name (force (toString value)) body |> Some
                    | None -> None

            | Sequential all ->
                match all |> List.choose toString with
                    | [] -> None
                    | l -> l |> String.concat "\r\n" |> Some

            | NextElement x -> 
                sprintf "%s.nextElementSibling" (force (toString x)) |> Some

            | InnerHTML(target,text) -> 
                sprintf "%s.innerHTML = \"%s\";" (force (toString target)) (escape text) |> Some

            | AppendChild(parent,inner) -> 
                let parent = toString parent |> force
                sprintf "%s.addAtEnd(%s);" parent (force (toString inner)) |> Some

            | InsertBefore(reference,element) -> 
                let ref = toString reference |> force
                sprintf "%s.parentElement.insertBefore(%s,%s);" ref (force (toString element)) ref |> Some

            | Replace(o,n) ->
                let ref = toString o |> force
                sprintf "%s.parentElement.replaceChild(%s, %s);" ref (force (toString n)) ref  |> Some

            | Var v ->
                Some v.name

            | Remove e ->
                sprintf "%s.remove();" (force (toString e)) |> Some
                    

type UiContentReader<'msg> =
    | RChildren of IListReader<UiUpdate<'msg>>
    | RText of TextUpdate
    | RScene of (IRenderControl -> IRenderTask)

and TextUpdate(text : IMod<string>) =
    inherit AdaptiveObject()

    member x.Update(token : AdaptiveToken, self : Expr) =
        x.EvaluateIfNeeded token Expr.Nop (fun token ->
            let nt = text.GetValue token
            Expr.InnerHTML(self, nt)
        )

and UiUpdate<'msg>(ui : Ui<'msg>, id : string) =
    inherit AdaptiveObject()

    static let mutable currentId = 0
    static let newId() =
        let id = Interlocked.Increment(&currentId)
        "n" + string id
            
    static let cmpState =
        { new IComparer<Index * ref<UiUpdate<'msg>>> with
            member x.Compare((l,_), (r,_)) =
                compare l r
        }
        
    let rAtt = ui.Attributes.GetReader()
    let rContent = 
        match ui.Content with
            | Children children -> (AList.map UiUpdate children).GetReader() |> RChildren
            | Text text -> RText (TextUpdate text)
            | Scene create -> RScene create

    let content = SortedSetExt<Index * ref<UiUpdate<'msg>>>(cmpState)

    let neighbours (i : Index) =
        let (l,s,r) = content.FindNeighbours((i, Unchecked.defaultof<_>))
        let l = if l.HasValue then Some l.Value else None
        let r = if r.HasValue then Some r.Value else None
        let s = if s.HasValue then Some (snd s.Value) else None
        l, s, r

    static let create (ui : UiUpdate<'msg>) (inner : Expr -> list<Expr>) =
        let v = { name = ui.Id }
        Expr.Let(
            v, Expr.CreateElement(ui.Tag),
            Expr.Sequential (
                Expr.SetAttribute(Var v, "id", ui.Id) :: inner (Var v)
            )
        )

    member x.Destroy(state : UpdateState<'msg>) =
        for (name, v) in rAtt.State do
            match v with
                | Event _ -> state.handlers.Remove(id, name) |> ignore
                | _ -> ()

        rAtt.Dispose()

    member x.Update(token : AdaptiveToken, self : Expr, state : UpdateState<'msg>) =
        x.EvaluateIfNeeded token Expr.Nop (fun token ->
            let code = List()
                
            let attOps = rAtt.GetOperations(token)
            for (name, op) in attOps do
                match op with
                    | ElementOperation.Set v -> 
                        let value = 
                            match v with
                                | Value str -> 
                                    str

                                | Event (props, cb) ->
                                    state.handlers.[(id, name)] <- cb
                                    let args = (sprintf "\"%s\"" id) :: (sprintf "\"%s\"" name) :: props |> String.concat ","
                                    sprintf "aardvark.processEvent(%s);" args

                        code.Add(SetAttribute(self, name, value))

                    | ElementOperation.Remove ->
                        code.Add(RemoveAttribute(self, name))

            match rContent with
                | RText tu ->
                    tu.Update(token, self) |> code.Add

                | RScene create ->
                    state.scenes.[id] <- create

                | RChildren children ->
                    let mutable toUpdate = children.State
                    let ops = children.GetOperations token

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
                                        n.Destroy(state)
                                    | None ->
                                        failwith "sadasdlnsajdnmsad"

                            | ElementOperation.Set newElement ->
                                let (l,s,r) = neighbours i
                                    
                                match s with
                                    | Some ref ->
                                        let oldElement = !ref
                                        oldElement.Destroy(state)
                                        ref := newElement

                                        toUpdate <- PList.remove i toUpdate

                                        let v = { name = newElement.Id }
                                        let expr = 
                                            create newElement (fun n ->
                                                [
                                                    Expr.Replace(GetElementById oldElement.Id, n)
                                                    newElement.Update(token, Var v, state)
                                                ]
                                            )

                                        code.Add expr

                                    | _ ->
                                        content.Add(i, ref newElement) |> ignore

                                        match r with
                                            | None ->
                                                let expr = create newElement (fun n -> [ AppendChild(self, n); newElement.Update(token, n, state) ] )
                                                code.Add expr

                                            | Some (_,r) ->
                                                let r = r.Value.Id
                                                let expr = create newElement (fun n -> [ InsertBefore(GetElementById r, n); newElement.Update(token, n, state) ] )
                                                code.Add expr
                                                    
                                                    
                
                    for i in toUpdate do
                        let v = { name = i.Id }
                        let expr = 
                            Expr.Let(
                                v, Expr.GetElementById i.Id,
                                i.Update(token, Var v, state)
                            )
                        code.Add expr

            Expr.Sequential (CSharpList.toList code)
        )

    member x.Tag = ui.Tag
    member x.Id = id

    new(ui : Ui<'msg>) = UiUpdate<'msg>(ui, newId())




    

[<AutoOpen>]
module ``Extensions for Node`` =
    type Ui<'msg> with
        member x.GetReader() =
            UiUpdate(x)

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
                    let expr = reader.Update(AdaptiveToken.Top, Body, state) 
                    let code = expr |> Expr.toString |> Option.defaultValue ""
                    let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]

                    if code = "" then
                        ()
                        //Log.line "empty update"
                    else

                        //let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                        //lock runtime (fun () -> 
                        //    Log.start "update"
                        //    for l in lines do Log.line "%s" l
                        //    Log.stop()
                        //)

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
            cinit : 'model -> 'mmodel
            capply : 'mmodel -> 'model -> unit
            cinitial : 'model
        }


    let startElm (runtime : IRuntime) (port : int) (app : ConcreteMApp<'m, 'mm, 'msg>) =
        let imut = Mod.init app.cinitial
        let mut = app.cinit app.cinitial
        let perform (msg : 'msg) =
            let newImut = app.cupdate imut.Value msg
            transact (fun () ->
                imut.Value <- newImut
                app.capply mut newImut
            )

        let view = app.cview mut

        start runtime port perform view

    let inline startElm'<'model, 'msg, 'mmodel when 'mmodel : (static member Create : 'model -> 'mmodel) and 'mmodel : (member Update : 'model -> unit)> (runtime : IRuntime) (port : int) (app : App<'model, 'mmodel, 'msg>) =   
        let capp = 
            {
                cview = app.view
                cupdate = app.update
                cinit = fun m -> (^mmodel : (static member Create : 'model -> 'mmodel) (m))
                capply = fun mm m -> (^mmodel : (member Update : 'model -> unit) (mm,m))
                cinitial = app.initial
            }

        startElm runtime port capp


module Bla =
    open Aardvark.Base.Rendering

    module TestApp =
        
        type Model =
            {
                lastName : Option<string>
                elements : plist<string>
            }


        and MModel =
            {
                mlastName : ResetMod<Option<string>>
                melements : ResetList<string>
            }
            static member Create(m : Model) =
                {
                    mlastName = ResetMod(m.lastName)
                    melements = ResetList(m.elements)
                }

            member x.Update(m : Model) =
                x.mlastName.Update(m.lastName)
                x.melements.Update(m.elements)

        type Message =
            | AddButton of Index * string

        let update (m : Model) (msg : Message) =

            match msg with
                | AddButton(before, str) -> 
                    let mutable m = m
                    for i in 1 .. 100 do
                        let str = Guid.NewGuid() |> string
                        let l, s, r = MapExt.neighbours before m.elements.Content
                        let l = 
                            match s with
                                | Some s -> Some s
                                | None -> l
                        let index = 
                            match l, r with
                                | Some (before,_), Some (after,_) -> Index.between before after
                                | None,            Some (after,_) -> Index.before after
                                | Some (before,_), None           -> Index.after before
                                | None,            None           -> Index.after Index.zero
                        m <- { m with lastName = Some str; elements = m.elements.Set(index, str) }
                    m

        let view (m : MModel) =
            Ui(
                "div",
                AMap.empty,
                AList.ofList [
                    Ui(
                        "div",
                        AMap.empty,
                        m.melements |> AList.mapi (fun i str ->
                            Ui(
                                "button",
                                AMap.ofList ["onclick", Event([], fun _ -> AddButton (i, Guid.NewGuid() |> string))],
                                Mod.constant str
                            )
                        )
                    )

                    //Ui(
                    //    "div",
                    //    AMap.ofList ["class", Value "aardvark"; "style", Value "height: 600px; width: 800px"],
                    //    fun (ctrl : IRenderControl) ->
                            
                    //        let value =
                    //            m.mlastName |> Mod.map (function Some str -> str | None -> "yeah")

                    //        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
                    //        let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                    //        let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time

                    //        let sg = 
                    //            Sg.markdown MarkdownConfig.light value
                    //                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                    //                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

                    //        ctrl.Runtime.CompileRender(ctrl.FramebufferSignature, sg)

                    //)

                ]
            )

        let initial =
            {
                lastName = None
                elements = PList.ofList ["A"; "B"]
            }

        let start (runtime : IRuntime) (port : int) =
            Ui.startElm' runtime port {
                view = view
                update = update
                initial = initial
            }


    let test =
        let initial = List.init 20 (fun i -> System.String [|'A' + char i|])
        let elements = clist initial
        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        let view = Mod.init view

        Ui(
            "div",
            AMap.empty,
            AList.ofList [
                Ui(
                    "div",
                    AMap.empty,
                    elements |> AList.mapi (fun ci c ->
                        Ui(
                            "button",
                            AMap.ofList ["onclick", Event([], fun _ () -> [1 .. 50] |> List.iter (fun i -> elements.Append(string elements.Count)|> ignore))],
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
        



