namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Service
open Aardvark.UI.Semantics



[<AutoOpen>]
module private Utils =
    open Aardvark.Base.TypeInfo

    let typename<'a> = Aardvark.Base.ReflectionHelpers.getPrettyName typeof<'a>

module Pickler =
    let json = Aardvark.Service.Pickler.json
    let unpickleOfJson s = json.UnPickleOfString s
    let jsonToString s = json.PickleToString s


type Event<'msg> =
    {
        clientSide : (string -> list<string> -> string) -> string -> string
        serverSide : Guid -> string -> list<string> -> seq<'msg>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Event =

    let private processEvent (name : string) (id : string) (args : list<string>) =
        let args = sprintf "'%s'" id :: sprintf "'%s'" name :: args
        sprintf "aardvark.processEvent(%s); event.preventDefault();" (String.concat ", " args)
        
    let toString (id : string) (name : string) (evt : Event<'msg>) =
        let send = processEvent name
        evt.clientSide send id

    let empty<'msg> : Event<'msg> =
        {
            clientSide = fun _ _ -> ""
            serverSide = fun _ _ _ -> Seq.empty
        }

    let ofTrigger (reaction : unit -> 'msg) =
        {
            clientSide = fun send id -> send id []
            serverSide = fun _ _ _ -> Seq.delay (reaction >> Seq.singleton)
        }

    let ofDynamicArgs (args : list<string>) (reaction : list<string> -> seq<'msg>) =
        {
            clientSide = fun send id -> send id args
            serverSide = fun session id args -> reaction args
        }

    let create1 (a : string) (reaction : 'a -> 'msg) =
        {
            clientSide = fun send id -> send id [a]
            serverSide = fun session id args -> 
                match args with
                    | [a] ->
                        try 
                            Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a)))
                        with e ->
                            Log.warn "[UI] expected args (%s) but got (%A)" typename<'a> a
                            Seq.empty

                    | _ -> 
                        Log.warn "[UI] expected args (%s) but got %A" typename<'a> args
                        Seq.empty
        }

    let create2 (a : string) (b : string) (reaction : 'a -> 'b -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b]
            serverSide = fun session id args -> 
                match args with
                    | [a; b] ->
                        try 
                            Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) ))
                        with e ->
                            Log.warn "[UI] expected args (%s, %s) but got (%A, %A)" typename<'a> typename<'b> a b
                            Seq.empty

                    | _ -> 
                        Log.warn "[UI] expected args (%s, %s) but got %A" typename<'a> typename<'b> args
                        Seq.empty
        }

    let create3 (a : string) (b : string) (c : string) (reaction : 'a -> 'b -> 'c -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b; c]
            serverSide = fun session id args -> 
                match args with
                    | [a; b; c] ->
                        try 
                            Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) (Pickler.json.UnPickleOfString c)))
                        with e ->
                            Log.warn "[UI] expected args (%s, %s, %s) but got (%A, %A, %A)" typename<'a> typename<'b> typename<'c> a b c
                            Seq.empty

                    | _ -> 
                        Log.warn "[UI] expected args (%s, %s, %s) but got %A" typename<'a> typename<'b> typename<'c> args
                        Seq.empty
        }

    let combine (l : Event<'msg>) (r : Event<'msg>) =
        {
            clientSide = fun send id ->
                l.clientSide (fun id args -> send id ("0" :: args)) id + "; " +
                r.clientSide (fun id args -> send id ("1" :: args)) id

            serverSide = fun session id args ->
                match args with
                    | "0" :: args -> l.serverSide session id args
                    | "1" :: args -> r.serverSide session id args
                    | _ -> 
                        Log.warn "[UI] expected args ((1|2)::args) but got %A" args
                        Seq.empty
                
        }

    let combineMany (events : seq<Event<'msg>>) =
        let events = Seq.toArray events

        match events.Length with
            | 0 -> empty
            | 1 -> events.[0]
            | _ -> 
                {
                    clientSide = fun send id ->
                        let clientScripts = 
                            events |> Seq.mapi (fun i e ->
                                e.clientSide (fun id args -> send id (string i :: args)) id
                            )
                        String.concat "; " clientScripts

                    serverSide = fun session id args ->
                        match args with
                            | index :: args ->
                                match Int32.TryParse index with
                                    | (true, index) when index >= 0 && index < events.Length ->
                                        events.[index].serverSide session id args

                                    | _ ->
                                        Log.warn "[UI] unexpected index for dispatcher: %A" index
                                        Seq.empty
                            | [] ->
                                Log.warn "[UI] expected at least one arg for dispatcher"
                                Seq.empty
                        
                        
                
                }

    let map (f : 'a -> 'b) (e : Event<'a>) = 
        {
            clientSide = e.clientSide; 
            serverSide = fun session id args -> Seq.map f (e.serverSide session id args) 
        }

[<RequireQualifiedAccess>]
type AttributeValue<'msg> =
    | String of string
    | Event of Event<'msg>
    //| RenderControlEvent of (SceneEvent -> list<'msg>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttributeValue =
    
    let combine (name : string) (l : AttributeValue<'msg>) (r : AttributeValue<'msg>) =
        match name, l, r with
            | _, AttributeValue.Event l, AttributeValue.Event r -> 
                AttributeValue.Event (Event.combine l r)

            //| _, AttributeValue.RenderControlEvent l,  AttributeValue.RenderControlEvent r ->
            //     AttributeValue.RenderControlEvent (fun a -> l a @ r a)

            | "class", AttributeValue.String l, AttributeValue.String r -> 
                AttributeValue.String (l + " " + r)



            | _ -> 
                r

    let map (f : 'a -> 'b) (v : AttributeValue<'a>) = 
        match v with
            | AttributeValue.Event e -> AttributeValue.Event (Event.map f e)
            | AttributeValue.String s -> AttributeValue.String s
            //| AttributeValue.RenderControlEvent rc -> AttributeValue.RenderControlEvent (fun s -> List.map f (rc s))


type AttributeMap<'msg>(map : amap<string, AttributeValue<'msg>>) =
    static let empty = AttributeMap<'msg>(AMap.empty)

    static member Empty = empty

    member x.AMap = map

    member x.Content = map.Content
    member x.GetReader() = map.GetReader()

module AttributeMap =

    /// the empty attributes map
    let empty<'msg> = AttributeMap<'msg>.Empty
    
    /// creates an attribute-map with one single entry
    let single (name : string) (value : AttributeValue<'msg>) =
        AttributeMap(AMap.ofList [name, value])

    /// creates an attribute-map using all entries from the sequence (conflicts are merged)
    let ofSeq (seq : seq<string * AttributeValue<'msg>>) =
        let mutable res = HMap.empty
        for (key, value) in seq do
            res <- res |> HMap.update key (fun o ->
                match o with
                    | Some ov -> AttributeValue.combine key ov value
                    | _ -> value
            )


        AttributeMap(AMap.ofHMap res)

    /// creates an attribute-map using all entries from the list (conflicts are merged)
    let ofList (list : list<string * AttributeValue<'msg>>) =
        ofSeq list

    /// creates an attribute-map using all entries from the array (conflicts are merged)
    let ofArray (arr : array<string * AttributeValue<'msg>>) =
        ofSeq arr

    let ofAMap (map : amap<string, AttributeValue<'msg>>) =
        AttributeMap(map)

    let toAMap (map : AttributeMap<'msg>) =
        map.AMap

    /// merges two attribute-maps by merging all conflicting values (preferring right)
    let union (l : AttributeMap<'msg>) (r : AttributeMap<'msg>) =
        AttributeMap(AMap.unionWith AttributeValue.combine l.AMap r.AMap)

    let rec unionMany (list : list<AttributeMap<'msg>>) =
        match list with
            | [] -> empty
            | [a] -> a
            | [a;b] -> union a b
            | a :: rest -> union a (unionMany rest)

    let ofSeqCond (seq : seq<string * IMod<Option<AttributeValue<'msg>>>>) =
        let mutable groups = HMap.empty

        for (key, value) in seq do
            groups <- groups |> HMap.update key (fun o ->
                match o with
                    | Some l -> l @ [value]
                    | None -> [value]
            )

        let unique = 
            groups |> HMap.map (fun key values ->
                match values with
                    | [v] -> v
                    | many ->
                        Mod.custom (fun token ->
                            let mutable final = None
                            for e in many do
                                match e.GetValue token with
                                    | Some v ->
                                        match final with
                                            | None -> final <- Some v
                                            | Some f -> final <- Some (AttributeValue.combine key f v)
                                    | _ ->
                                        ()

                            final
                        )
            )

        let map = AMap.ofHMap unique

        AttributeMap(AMap.flattenM map)

    let ofListCond (list : list<string * IMod<Option<AttributeValue<'msg>>>>) =
        ofSeqCond list

    let ofArrayCond (list : array<string * IMod<Option<AttributeValue<'msg>>>>) =
        ofSeqCond list


    let map (mapping : string -> AttributeValue<'a> -> AttributeValue<'b>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.map mapping map.AMap)
    
    let mapAttributes (mapping : AttributeValue<'a> -> AttributeValue<'b>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.map (fun _ v -> mapping v) map.AMap)

    let choose (mapping : string -> AttributeValue<'a> -> Option<AttributeValue<'b>>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.choose mapping map.AMap)

    let filter (predicate : string -> AttributeValue<'a> -> bool) (map : AttributeMap<'a>) =
        AttributeMap(AMap.filter predicate map.AMap)



type ReferenceKind =
    | Script 
    | Stylesheet

type Reference = { kind : ReferenceKind; name : string; url : string }


[<AbstractClass>]
type SceneEventProcessor<'msg>() =

    static let empty =
        { new SceneEventProcessor<'msg>() with
            member x.NeededEvents = ASet.empty
            member x.Process(_,_) = Seq.empty
        }

    static member Empty = empty

    abstract member NeededEvents : aset<SceneEventKind>
    abstract member Process : source : Guid * evt : SceneEvent -> seq<'msg>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SceneEventProcessor =
    
    [<AutoOpen>]
    module Implementation =
        type UnionProcessor<'msg>(inner : list<SceneEventProcessor<'msg>>) =
            inherit SceneEventProcessor<'msg>()

            let needed =
                lazy (
                    inner |> List.map (fun i -> i.NeededEvents) |> ASet.unionMany'
                )

            override x.NeededEvents = needed.Value
            override x.Process(sender, e) = 
                seq {
                    for p in inner do
                        yield! p.Process(sender, e)
                }

    let empty<'msg> = SceneEventProcessor<'msg>.Empty

    let union (l : SceneEventProcessor<'msg>) (r : SceneEventProcessor<'msg>) =
        UnionProcessor [l;r] :> SceneEventProcessor<'msg>

    let unionMany (processors : seq<SceneEventProcessor<'msg>>) =
        processors |> Seq.toList |> UnionProcessor :> SceneEventProcessor<'msg>

type Request =
    {
        requestPath : string
        queryParams : Map<string, string>
    }

type ChannelMessage = { targetId : string; channel : string; data : list<string> }

[<AbstractClass>]
type ChannelReader() =
    inherit AdaptiveObject()

    abstract member ComputeMessages : AdaptiveToken -> list<string>
    abstract member Release : unit -> unit

    member x.GetMessages(t : AdaptiveToken) =
        x.EvaluateIfNeeded t [] (fun t ->
            x.ComputeMessages(t)
        )

    member x.Dispose() =
        x.Release()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass>]
type Channel() =
    abstract member GetReader : unit -> ChannelReader

[<AutoOpen>]
module ChannelThings = 
    type private ModChannelReader<'a>(m : IMod<'a>) =
        inherit ChannelReader()

        let mutable last = None

        override x.Release() =
            last <- None

        override x.ComputeMessages t =
            let v = m.GetValue t

            if Unchecked.equals last (Some v) then
                []
            else
                last <- Some v
                [ Pickler.json.PickleToString v ]

    type private ASetChannelReader<'a>(s : aset<'a>) =
        inherit ChannelReader()

        let reader = s.GetReader()
    
        override x.Release() =
            reader.Dispose()

        override x.ComputeMessages t =
            let ops = reader.GetOperations t
            ops |> HDeltaSet.toList |> List.map Pickler.json.PickleToString
   
    type private ModChannel<'a>(m : IMod<'a>) =
        inherit Channel()
        override x.GetReader() = new ModChannelReader<_>(m) :> ChannelReader
    
    type private ASetChannel<'a>(m : aset<'a>) =
        inherit Channel()
        override x.GetReader() = new ASetChannelReader<_>(m) :> ChannelReader
    
    type IMod<'a> with
        member x.Channel = ModChannel(x) :> Channel

    type aset<'a> with
        member x.Channel = ASetChannel(x) :> Channel

    module Mod =
        let inline channel (m : IMod<'a>) = m.Channel

    module ASet =
        let inline channel (m : aset<'a>) = m.Channel



[<Sealed>]
type DomNode<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>, content : DomContent<'msg>) =
    let mutable required : list<Reference> = []
    let mutable boot : Option<string -> string> = None
    let mutable shutdown : Option<string -> string> = None
    let mutable callbacks : Map<string, (list<string> -> 'msg)> = Map.empty
    let mutable channels : Map<string, Channel> = Map.empty


    let mutable ns : Option<string> = ns

    member x.Tag = tag
    member x.Content = content

    abstract member Attributes : AttributeMap<'msg>
    default x.Attributes = attributes

    member x.Namespace 
        with get () = ns
        and private set n = ns <- n
    
    member x.Required
        with get() = required
        and private set r = required <- r

    member x.Boot
        with get() = boot
        and private set code = boot <- code

    member x.Channels
        with get() = channels
        and private set c = channels <- c

    member x.Shutdown
        with get() = shutdown
        and private set code = shutdown <- code

    member x.Callbacks
        with get() = callbacks
        and private set cbs = callbacks <- cbs

    member private x.Copy() =
        DomNode<'msg>(
            x.Tag,
            x.Namespace,
            x.Attributes,
            x.Content,
            Required = x.Required,
            Boot = x.Boot,
            Shutdown = x.Shutdown,
            Callbacks = x.Callbacks,
            Channels = x.Channels
        )

    member x.Map(f : 'msg -> 'b) = 
        DomNode<'b>(
            x.Tag,
            x.Namespace,
            x.Attributes |> AttributeMap.mapAttributes (AttributeValue.map f),
            DomContent.Map(x.Content, f),
            Required = x.Required,
            Boot = x.Boot,
            Shutdown = x.Shutdown,
            Callbacks = (x.Callbacks |> Map.map (fun k v -> fun xs -> f (v xs))),
            Channels = x.Channels
        )
    member x.WithRequired (required : list<Reference>) =
        let res = x.Copy()
        res.Required <- required
        res

    member x.WithBoot (boot : Option<string -> string>) =
        let res = x.Copy()
        res.Boot <- boot
        res
    member x.WithChannels (c : Map<string, Channel>) =
        let res = x.Copy()
        res.Channels <- c
        res


    member x.AddBoot (boot : string -> string) =
        let res = x.Copy()
        res.Boot <- 
            match x.Boot with
                | Some f -> (fun s -> f s + ";" + boot s) |> Some
                | None -> Some boot
        res

    member x.AddShutdown (shutdown : string -> string) =
        let res = x.Copy()
        res.Shutdown <- 
            match x.Shutdown with
                | Some f -> (fun s -> f s + ";" + shutdown s) |> Some
                | None -> Some shutdown
        res


    member x.AddRequired (reqs : list<Reference>) =
        let res = x.Copy()
        res.Required <- x.Required @ reqs
        res

    member x.WithNamespace (ns : string) =
        let res = x.Copy()
        res.Namespace <- Some ns
        res

    member x.WithShutdown (shutdown : Option<string -> string>) =
        let res = x.Copy()
        res.Shutdown <- shutdown
        res

    member x.WithCallbacks (callbacks : Map<string, list<string> -> 'msg>) =
        let res = x.Copy()
        res.Callbacks <- callbacks
        res

    member x.WithAttributes(att : AttributeMap<'msg>) =
        DomNode<'msg>(
            x.Tag,
            x.Namespace,
            att,
            x.Content,
            Required = x.Required,
            Boot = x.Boot,
            Shutdown = x.Shutdown,
            Callbacks = x.Callbacks,
            Channels = x.Channels
        )

    member x.WithContent(content : DomContent<'msg>) =
        DomNode<'msg>(
            x.Tag,
            x.Namespace,
            x.Attributes,
            content,
            Required = x.Required,
            Boot = x.Boot,
            Shutdown = x.Shutdown,
            Callbacks = x.Callbacks,
            Channels = x.Channels
        )

and DomContent<'msg> =
    | Empty
    | Children of alist<DomNode<'msg>>
    | Scene    of Aardvark.Service.Scene * (Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState)
    | Text     of IMod<string>
    | Page     of (Request -> DomNode<'msg>)

    with 
        static member Map(x : DomContent<'msg>, f : 'msg -> 'b) = 
            match x with
                | Empty -> Empty
                | Children xs -> Children (AList.map (fun (n:DomNode<'msg>) -> n.Map f) xs)
                | Scene(s,f)  -> Scene(s,f)
                | Text t      -> Text t
                | Page c      -> 
                    let inner request =
                        let r = c request
                        r.Map f
                    Page inner
            
open Aardvark.Service

type RenderCommand<'msg> =
    | Clear of color : Option<IMod<C4f>> * depth : Option<IMod<float>>
    | SceneGraph of sg : ISg<'msg>


type DomNode private() =
    static let eventNames =
        Map.ofList [
            SceneEventKind.Click,           "onclick"
            SceneEventKind.DoubleClick,     "ondblclick"
            SceneEventKind.Down,            "onmousedown"
            SceneEventKind.Up,              "onmouseup"
            SceneEventKind.Move,            "onmousemove"
        ]

    static let needsButton =
        let needingButton = Set.ofList [SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up]
        fun k -> Set.contains k needingButton

    static let eventKinds =
        eventNames |> Map.toSeq |> Seq.map (fun (a,b) -> (b,a)) |> Map.ofSeq

    static let button (code : int) =
        match code with
            | 1 -> MouseButtons.Left
            | 2 -> MouseButtons.Middle
            | 3 -> MouseButtons.Right
            | _ -> MouseButtons.None

    static member Text(content : IMod<string>) = 
        DomNode<'msg>("span", None, AttributeMap.empty, DomContent.Text content)

    static member Page(f : Request -> DomNode<'msg>) =
        DomNode<'msg>("page", None, AttributeMap.empty, DomContent.Page f)  

    static member SvgText(content : IMod<string>) = 
        DomNode<'msg>("tspan", Some "http://www.w3.org/2000/svg", AttributeMap.empty, DomContent.Text content)

    static member Void(tag : string, attributes : AttributeMap<'msg>) =
        DomNode<'msg>(tag, None, attributes, DomContent.Empty)     

    static member Node(tag : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
        DomNode<'msg>(tag, None, attributes, DomContent.Children content)

    static member Void(tag : string, ns : string, attributes : AttributeMap<'msg>) =
        DomNode<'msg>(tag, Some ns, attributes, DomContent.Empty)     

    static member Node(tag : string, ns : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
        DomNode<'msg>(tag, Some ns, attributes, DomContent.Children content)

    static member RenderControl(attributes : AttributeMap<'msg>, processor : SceneEventProcessor<'msg>, getState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState, scene : Aardvark.Service.Scene, htmlChildren : Option<DomNode<_>>) =


        let perform (sourceSession : Guid, sourceId : string, kind : SceneEventKind, buttons : MouseButtons, pos : V2i) : seq<'msg> =
            match scene.TryGetClientInfo(sourceSession, sourceId) with
                | Some (info, state) -> 
                    let pp = PixelPosition(pos.X, pos.Y, info.size.X, info.size.Y)
                    let ray = state |> ClientState.pickRay pp |> FastRay3d |> RayPart

                    let evt = 
                        {
                            evtKind    = kind
                            evtRay     = ray 
                            evtButtons = buttons
                            evtTrafo   = Mod.constant Trafo3d.Identity
                        }

                    let procRes = processor.Process(sourceSession, evt)
                    procRes

                    //let renderControlName = "RenderControl." + eventNames.[kind]
                    //match HMap.tryFind renderControlName (Mod.force controlEvents.Content) with
                    //    | Some cb ->
                    //        cb.s
                    //        cb evt @ procRes
                    //    | None -> 
                    //        procRes

                | None ->
                    Log.warn "[UI] could not get client info for %A/%s" sourceSession sourceId
                    Seq.empty

        let rayEvent (includeButton : bool) (kind : SceneEventKind) =
            let args =
                if includeButton then ["event.offsetX"; "event.offsetY"; "event.which"]
                else ["event.offsetX"; "event.offsetY"]

            {
                clientSide = fun send id -> send id args + "; event.preventDefault();"
                serverSide = fun session id args ->
                    match args with
                        | x :: y :: which :: _ ->
                            let x = round (float x) |> int
                            let y = round (float y) |> int
                            let button = int which |> button
                            perform(session, id, kind, button, V2i(x,y))

                        | x :: y :: _ -> 
                            let vx = round (float x) |> int
                            let vy = round (float y) |> int
                            perform(session, id, kind, MouseButtons.None, V2i(vx,vy))

                        | _ ->
                            Seq.empty
                        
            }

        let events =
           processor.NeededEvents 
                |> ASet.map (fun k -> 
                    let kind = 
                        match k with
                            | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                            | _ -> k

                    
                    let button = needsButton kind
                    eventNames.[kind], AttributeValue.Event(rayEvent button kind)
                )
                |> AMap.ofASet
                |> AMap.map (fun k vs ->
                    match HSet.toList vs with
                        | [] -> AttributeValue.Event Event.empty
                        | h :: rest ->
                            rest |> List.fold (AttributeValue.combine "urdar") h
                            
                )
                |> AttributeMap.ofAMap

        let ownAttributes =
            AttributeMap.unionMany [
                events
                attributes
                AttributeMap.single "class" (AttributeValue.String "aardvark")
            ]

        let boot (id : string) =
            sprintf "aardvark.getRenderer(\"%s\");" id


        match htmlChildren with
        | Some htmlChildren -> 
            printfn "not implemented"
            DomNode<'msg>("div", None, ownAttributes, DomContent.Scene(scene, getState)).WithBoot(Some boot)
        | None -> 
            DomNode<'msg>("div", None, ownAttributes, DomContent.Scene(scene, getState)).WithBoot(Some boot)



    static member RenderControl(attributes : AttributeMap<'msg>, getState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState, scene : Aardvark.Service.Scene, htmlChildren : Option<DomNode<_>>) =
        DomNode.RenderControl(attributes, SceneEventProcessor.empty, getState, scene, htmlChildren)
    
    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, scene : Aardvark.Service.Scene, htmlChildren : Option<DomNode<_>>) =
        let getState(c : Aardvark.Service.ClientInfo) =
            let cam = camera.GetValue(c.token)
            let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

            {
                viewTrafo = CameraView.viewTrafo cam.cameraView
                projTrafo = Frustum.projTrafo cam.frustum
            }

        DomNode.RenderControl(attributes, SceneEventProcessor.empty, getState, scene, htmlChildren)

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, sg : ClientValues -> ISg<'msg>, isOrtho: bool, htmlChildren : Option<DomNode<_>>) =
        let getState(c : Aardvark.Service.ClientInfo) =
            let cam = camera.GetValue(c.token)
            let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

            {
                viewTrafo = CameraView.viewTrafo cam.cameraView
                projTrafo = if isOrtho then Frustum.orthoTrafo cam.frustum else Frustum.projTrafo cam.frustum
            }

        let tree = Mod.init <| PickTree.ofSg (Sg.ofList [])

        let globalPicks = Mod.init AMap.empty
        
        let scene =
            Scene.custom (fun values ->
                let sg =
                    sg values
                        |> Sg.viewTrafo values.viewTrafo
                        |> Sg.projTrafo values.projTrafo
                        |> Sg.uniform "ViewportSize" values.size

                transact ( fun _ -> tree.Value <- PickTree.ofSg sg )
                
                transact ( fun _ -> globalPicks.Value <- sg.GlobalPicks() ) 
                
                values.runtime.CompileRender(values.signature, sg)
            )

        let proc =
            { new SceneEventProcessor<'msg>() with
                member x.NeededEvents = 
                    aset {
                        let! tree = tree
                        let! globalPicks = globalPicks
                        yield! ASet.union (AMap.keys globalPicks) tree.Needed
                    }

                member x.Process (source : Guid, evt : SceneEvent) = 
                    seq {
                        let consumed, msgs = tree.GetValue().Perform(evt)
                        yield! msgs

                        let m = globalPicks.GetValue().Content |> Mod.force
                        match m |> HMap.tryFind evt.kind with
                            | Some cb -> 
                                yield! cb evt
                            | None -> 
                                ()
                    }
            }

        DomNode.RenderControl(attributes, proc, getState, scene, htmlChildren)

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, sg : ISg<'msg>, isOrtho: bool, htmlChildren : Option<DomNode<_>>) =
        DomNode.RenderControl(attributes, camera, constF sg, isOrtho, htmlChildren)

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, sgs : alist<RenderCommand<'msg>>, htmlChildren : Option<DomNode<_>>) =
        let getState(c : Aardvark.Service.ClientInfo) =
            let cam = camera.GetValue(c.token)
            let cam = { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }

            {
                viewTrafo = CameraView.viewTrafo cam.cameraView
                projTrafo = Frustum.projTrafo cam.frustum
            }


        let scene =
            Scene.custom (fun values ->
                
                let invoke pass =   
                    match pass with
                        | RenderCommand.Clear(color,depth) -> 
                            match color,depth with
                                | Some c, Some d -> values.runtime.CompileClear(values.signature,c,d) 
                                | None, Some d ->  values.runtime.CompileClear(values.signature,d) 
                                | Some c, None -> values.runtime.CompileClear(values.signature,c)
                                | None, None -> RenderTask.empty
                        | RenderCommand.SceneGraph sg -> 
                            let sg =
                                sg
                                    |> Sg.viewTrafo values.viewTrafo
                                    |> Sg.projTrafo values.projTrafo
                                    |> Sg.uniform "ViewportSize" values.size
                            values.runtime.CompileRender(values.signature, sg)

                let reader = new AList.Readers.MapUseReader<_,_>(Ag.getContext(), sgs,invoke, (fun d -> d.Dispose()))
                let mutable state = PList.empty

                let update (t : AdaptiveToken) =
                    let ops = reader.GetOperations t
                    let s, _ = PList.applyDelta state ops
                    state <- s

                { new AbstractRenderTask() with
                    override x.PerformUpdate(t,rt) = 
                        update t
                    override x.Perform(t,rt,o) = 
                        update t
                        for task in state do
                            task.Run(t,rt,o)
                    override x.Release() = 
                        reader.Dispose()
                        state <- PList.empty

                    override x.Use f = f ()
                    override x.FramebufferSignature = Some values.signature
                    override x.Runtime = Some values.runtime
                } :> IRenderTask
                
            )

        let trees = sgs |> AList.choose (function Clear _ -> None | SceneGraph sg -> PickTree.ofSg sg |> Some)
        let globalPicks = sgs |> AList.toASet |> ASet.choose (function Clear _ -> None | SceneGraph sg -> sg.GlobalPicks() |> Some)

        let globalNeeded = globalPicks |> ASet.collect AMap.keys
        let treeNeeded = trees |> AList.toASet |> ASet.collect (fun t -> t.Needed)
        let needed = ASet.union globalNeeded treeNeeded


        let rec pickTrees (trees : list<PickTree<'msg>>) (evt) =
            match trees with
                | [] -> false, Seq.empty
                | x::xs -> 
                    let consumed,msgs = pickTrees xs evt
                    if consumed then true,msgs
                    else
                        let consumed, other = x.Perform evt
                        consumed, Seq.append msgs other

        let proc =
            { new SceneEventProcessor<'msg>() with
                member x.NeededEvents = needed
                member x.Process (source : Guid, evt : SceneEvent) = 
                    seq {
                        let trees = trees.Content |> Mod.force |> PList.toList

                        let consumed, msgs = pickTrees trees evt
                        yield! msgs

                        for perScene in globalPicks.Content |> Mod.force do
                            let picks = perScene.Content |> Mod.force
                            match picks |> HMap.tryFind evt.kind with
                                | Some cb -> 
                                    yield! cb evt
                                | None -> 
                                    ()
                    }
            }

        DomNode.RenderControl(attributes, proc, getState, scene, htmlChildren)


    static member RenderControl(camera : IMod<Camera>, scene : ISg<'msg>, isOrtho:bool, ?htmlChildren : DomNode<_>) : DomNode<'msg> =
        DomNode.RenderControl(AttributeMap.empty, camera, constF scene, isOrtho, htmlChildren)

    static member RenderControl(camera : IMod<Camera>, scene : ClientValues -> ISg<'msg>,isOrtho:bool, ?htmlChildren : DomNode<_>) : DomNode<'msg> =
        DomNode.RenderControl(AttributeMap.empty, camera, scene, isOrtho, htmlChildren)





