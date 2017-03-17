namespace Aardvark.UI

open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

type JSVar = { name : string }

type JSExpr =
    | Body
    | CreateElement of tag : string
    | SetAttribute of target : JSExpr * name : string * value : string
    | RemoveAttribute of target : JSExpr * name : string

    | Remove of target : JSExpr
    | InnerHTML of target : JSExpr * html : string 
    | InnerText of target : JSExpr * text : string 

    | Replace of oldElement : JSExpr * newElement : JSExpr
    | AppendChild  of parent : JSExpr * inner : JSExpr
    | InsertBefore of reference : JSExpr * inner : JSExpr // in html arguments switched
    | InsertAfter of reference : JSExpr * inner : JSExpr

    | Raw of code : string
    //| AddReferences of Map<string, string>
    | Sequential of list<JSExpr>
    | GetElementById of string
    | Let of JSVar * JSExpr * JSExpr
    | Var of JSVar
    | Nop

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JSExpr =
    let escape (str : string) =
        str.Replace("\"", "\\\"")
        
    let rec toJQueryString (e : JSExpr) =
        match e with
            //| AddReferences map ->
            //    let args = map |> Map.toSeq |> Seq.map (fun (n,u) -> sprintf "{ name: \"%s\", url: \"%s\"}" n u) |> String.concat "," |> sprintf "[%s]"
            //    sprintf "aardvark.addReferences(%s);" args

            | Raw code ->
                code

            | Body ->
                "$(document.body)"

            | Nop ->
                ""

            | CreateElement(tag) ->
                sprintf "$([document.createElement(\"%s\")])" tag

            | SetAttribute(t, name, value) ->
                let t = toJQueryString t
                sprintf "%s.attr(\"%s\", \"%s\");" t name (escape value)

            | RemoveAttribute(t, name) ->
                let t = toJQueryString t
                sprintf "%s.removeAttr(\"%s\");" t name

            | GetElementById(id) ->
                sprintf "$(\"#%s\")" id

            | Let(var, value, body) ->
                match toJQueryString body  with
                    | "" -> ""
                    | body -> sprintf "var %s = %s;\r\n%s" var.name (toJQueryString value) body

            | Sequential all ->
                match all |> List.map toJQueryString |> List.filter (fun str -> str <> "") with
                    | [] -> ""
                    | l -> l |> String.concat "\r\n"

            | InnerHTML(target,text) -> 
                sprintf "%s.html(\"%s\");" (toJQueryString target) (escape text)

            | InnerText(target,text) -> 
                sprintf "%s.html(\"%s\");" (toJQueryString target) (escape text)

            | AppendChild(parent,inner) -> 
                let parent = toJQueryString parent
                sprintf "%s.append(%s);" parent (toJQueryString inner)

            | InsertBefore(reference,element) -> 
                let ref = toJQueryString reference
                sprintf "%s.insertBefore(%s);" (toJQueryString element) ref

            | InsertAfter(reference,element) -> 
                let ref = toJQueryString reference
                sprintf "%s.insertAfter(%s);" (toJQueryString element) ref

            | Replace(o,n) ->
                let ref = toJQueryString o
                sprintf "%s.replaceWith(%s);" ref (toJQueryString n)

            | Var v ->
                v.name

            | Remove e ->
                sprintf "%s.remove();" (toJQueryString e)

    let rec toString (e : JSExpr) =
        match e with
            //| AddReferences map ->
            //    let args = map |> Map.toSeq |> Seq.map (fun (n,u) -> sprintf "{ name: \"%s\", url: \"%s\"}" n u) |> String.concat "," |> sprintf "[%s]"
            //    sprintf "aardvark.addReferences(%s);" args

            | Raw code ->
                code

            | Body ->
                "document.body"

            | Nop ->
                ""

            | CreateElement(tag) ->
                sprintf "document.createElement(\"%s\")" tag

            | SetAttribute(t, name, value) ->
                let t = toString t
                sprintf "%s.setAttribute(\"%s\", \"%s\");" t name (escape value)

            | RemoveAttribute(t, name) ->
                let t = toString t
                sprintf "%s.removeAttribute(\"%s\");" t name

            | GetElementById(id) ->
                sprintf "document.getElementById(\"%s\")" id

            | Let(var, value, body) ->
                match toString body  with
                    | "" -> ""
                    | body -> sprintf "var %s = %s;\r\n%s" var.name (toString value) body

            | Sequential all ->
                match all |> List.map toString |> List.filter (fun str -> str <> "") with
                    | [] -> ""
                    | l -> l |> String.concat "\r\n"


            | InnerHTML(target,text) -> 
                sprintf "%s.innerHTML = \"%s\";" (toString target) (escape (System.Web.HttpUtility.HtmlEncode text))
                
            | InnerText(target,text) -> 
                sprintf "%s.innerText = \"%s\";" (toString target) (escape text)

            | AppendChild(parent,inner) -> 
                let parent = toString parent
                sprintf "%s.appendChild(%s);" parent (toString inner)

            | InsertBefore(reference,element) -> 
                match reference with
                    | Var v -> 
                        sprintf "%s.parentElement.insertBefore(%s,%s);" v.name (toString element) v.name
                    | _ ->
                        let ref = toString reference
                        sprintf "var _temp = %s;\r\n_temp.parentElement.insertBefore(%s,_temp);" ref (toString element)

            | InsertAfter(reference,element) -> 
                let name, binding =
                    match reference with
                        | Var v -> v.name, ""
                        | v -> "_temp", sprintf "var _temp = %s;\r\n" (toString v)

                binding +
                sprintf "if(!%s.nextElementSibling) %s.parentElement.appendChild(%s);\r\n" name name (toString element) +
                sprintf "else %s.parentElement.insertBefore(%s, %s.nextElementSibling);" name (toString element) name

            | Replace(o,n) ->
                let ref = toString o
                sprintf "%s.parentElement.replaceChild(%s, %s);" ref (toString n) ref

            | Var v ->
                v.name

            | Remove e ->
                sprintf "%s.remove();" (toString e)
       

type UpdateState<'msg> =
    {
        handlers        : Dictionary<string * string, list<string> -> 'msg>
        scenes          : Dictionary<string, ('msg -> unit) -> IRenderControl -> IRenderTask>
        references      : Dictionary<string * ReferenceKind, Reference>
        activeChannels  : Dictionary<string * string, Channel>
    }
          
type IUiReader<'msg> =
    inherit IAdaptiveObject
    abstract member Update : AdaptiveToken * JSExpr * UpdateState<'msg> -> JSExpr
    abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr

module UiReaders =

    [<AbstractClass>]
    type AbstractUiReader<'msg>() =
        inherit AdaptiveObject()

        abstract member PerformUpdate : AdaptiveToken * JSExpr * UpdateState<'msg> -> JSExpr
        abstract member Destroy : UpdateState<'msg> * JSExpr -> JSExpr

        member x.Update(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
            x.EvaluateIfNeeded token JSExpr.Nop (fun token ->
                x.PerformUpdate(token, self, state)
            )

        interface IUiReader<'msg> with
            member x.Update(t,s,state) = x.Update(t,s,state)
            member x.Destroy(state, self) = x.Destroy(state, self)

    and EmptyReader<'msg> private() =
        inherit ConstantObject()

        static let instance = EmptyReader<'msg>() :> IUiReader<_>
        static member Instance = instance

        interface IUiReader<'msg> with
            member x.Update(_,_,_) = JSExpr.Nop
            member x.Destroy(_,_) = JSExpr.Nop
            

    and TextReader<'msg>(text : IMod<string>) =
        inherit AbstractUiReader<'msg>()

        override x.PerformUpdate(token : AdaptiveToken, self : JSExpr, state : UpdateState<'msg>) =
            let nt = text.GetValue token
            JSExpr.InnerText(self, nt)

        override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
            JSExpr.Nop

    and ChildrenReader<'msg>(id : string, children : alist<UiReader<'msg>>) =
        inherit AbstractUiReader<'msg>()
        let reader = children.GetReader()

        static let cmpState =
            { new IComparer<Index * ref<UiReader<'msg>>> with
                member x.Compare((l,_), (r,_)) =
                    compare l r
            }

        let content = SortedSetExt<Index * ref<UiReader<'msg>>>(cmpState)

        let neighbours (i : Index) =
            let (l,s,r) = content.FindNeighbours((i, Unchecked.defaultof<_>))
            let l = if l.HasValue then Some l.Value else None
            let r = if r.HasValue then Some r.Value else None
            let s = if s.HasValue then Some (snd s.Value) else None
            l, s, r

        static let create (ui : UiReader<'msg>) (inner : JSExpr -> list<JSExpr>) =
            let v = { name = ui.Id }
            JSExpr.Let(
                v, JSExpr.CreateElement(ui.Tag),
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

    and SceneReader<'msg>(id : string, create : ('msg -> unit) -> IRenderControl -> IRenderTask) =
        inherit AbstractUiReader<'msg>()

        let mutable initial = true

        override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
            state.scenes.Remove(id) |> ignore
            JSExpr.Nop

        override x.PerformUpdate(token, self, state) =
            if initial then
                initial <- false
                state.scenes.[id] <- create
            JSExpr.Nop

    and UiReader<'msg>(ui : Ui<'msg>, id : string) =
        inherit AbstractUiReader<'msg>()

        static let mutable currentId = 0
        static let newId() =
            let id = Interlocked.Increment(&currentId)
            "n" + string id

        let rAtt = ui.Attributes.GetReader()
        let rContent = 
            match ui.Content with
                | Children children -> ChildrenReader(id, AList.map UiReader children) :> IUiReader<_>
                | Text text -> TextReader text :> IUiReader<_>
                | Scene create -> SceneReader(id, create) :> IUiReader<_>
                | Empty -> EmptyReader.Instance

        let mutable initial = true


        member x.Tag = ui.Tag
        member x.Id = id

        override x.Destroy(state : UpdateState<'msg>, self : JSExpr) =
            for (name, v) in rAtt.State do
                match v with
                    | Event _ -> state.handlers.Remove(id, name) |> ignore
                    | _ -> ()

               
            for (name,cb) in Map.toSeq ui.Callbacks do
                state.handlers.Remove (id,name) |> ignore
                
            match ui.Boot with
                | Some getBootCode ->
                    for c in ui.Channels do
                        state.activeChannels.Remove(id,c.Name) |> ignore
                | None ->
                    ()

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
                
            if initial then
                initial <- false

                for (name,cb) in Map.toSeq ui.Callbacks do
                    state.handlers.[(id,name)] <- cb

                for r in ui.Required do
                    state.references.[(r.name, r.kind)] <- r

                // let a = Mod.map ....
                // a.onmessage = function()

                // channels = {};
                // dataSocket.onmessage = function(e) {
                //     var msg = JSON.parse(e.data);
                //     channels[msg.channel].onmessage(msg.data);
                //}

                match ui.Boot with
                    | Some getBootCode ->
                        for c in ui.Channels do
                            state.activeChannels.[(id,c.Name)] <- c
                        let prefix = ui.Channels |> List.map (fun c -> sprintf "var %s = aardvark.getChannel(\"%s\", \"%s\");" c.Name id c.Name) |> String.concat "\r\n"
                        let boot = getBootCode id
                        code.Add(Raw (prefix + boot))
                    | None ->
                        ()

                for (name, value) in Map.toSeq ui.InitialAttributes do
                    let value = 
                        match value with
                            | Value str -> 
                                str

                            | Event (props, cb) ->
                                state.handlers.[(id, name)] <- cb
                                let args = (sprintf "\"%s\"" id) :: (sprintf "\"%s\"" name) :: props |> String.concat ","
                                sprintf "aardvark.processEvent(%s);" args

                            | ClientEvent getCode ->
                                let code = getCode id
                                code

                    code.Add(SetAttribute(self, name, value))

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
                                    
                                | ClientEvent getCode ->
                                    let code = getCode id
                                    code

                        code.Add(SetAttribute(self, name, value))

                    | ElementOperation.Remove ->
                        code.Add(RemoveAttribute(self, name))

            code.Add (rContent.Update(token, self, state))

            JSExpr.Sequential (CSharpList.toList code)



        new(ui : Ui<'msg>) = UiReader<'msg>(ui, newId())

[<AutoOpen>]
module ``Extensions for Node`` =
    type Ui<'msg> with
        member x.GetReader() =
            UiReaders.UiReader<'msg>(x)