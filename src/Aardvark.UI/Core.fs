namespace Aardvark.UI

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Service
open Aardvark.UI.Semantics
open System.Reactive.Subjects


type RenderControlConfig =
    {
        adjustAspect : V2i -> Frustum -> Frustum 
    }

module RenderControlConfig =
    
    /// Fills height, depending in aspect ratio
    let standard = 
        {
            adjustAspect = fun (size : V2i) -> Frustum.withAspect (float size.X / float size.Y) 
        }

    /// Fills height, depending in aspect ratio
    let fillHeight = standard 

    /// Fills width, depending in aspect ratio
    let fillWidth =
        let aspect { left = l; right = r; top = t; bottom = b } =  (t - b) / (r - l)
        let withAspectFlipped (newAspect : float) ( { left = l; right = r; top = t; bottom = b } as f)  = 
            let factor = 1.0 - (newAspect / aspect f)                  
            { f with bottom = factor * t + b; top  = factor * b + t }

        {
            adjustAspect = fun (size : V2i) -> withAspectFlipped (float size.X / float size.Y) 
        }

    let noScaling =
        {
            adjustAspect = fun (size : V2i) (frustum : Frustum) -> frustum
        }
    


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

open Aardvark.Service

type RenderCommand<'msg> =
    | Clear of color : Option<IMod<C4f>> * depth : Option<IMod<float>>
    | SceneGraph of sg : ISg<'msg>
    


type IApp<'model, 'msg, 'outer> =
    abstract member ToOuter : 'model * 'msg -> seq<'outer>
    abstract member ToInner : 'model * 'outer -> seq<'msg>
    abstract member Start : unit -> MutableApp<'model, 'msg>
        
and MutableApp<'model, 'msg> =
    {
        lock        : obj
        model       : IMod<'model>
        ui          : DomNode<'msg>
        update      : Guid -> seq<'msg> -> unit
        updateSync  : Guid -> seq<'msg> -> unit
        messages    : IObservable<'msg>
        shutdown    : unit -> unit
    }

    
and [<AbstractClass>] DomNode<'msg>() =
    let mutable required : list<Reference> = []
    let mutable boot : Option<string -> string> = None
    let mutable shutdown : Option<string -> string> = None
    let mutable callbacks : Map<string, (list<string> -> 'msg)> = Map.empty
    let mutable channels : Map<string, Channel> = Map.empty
        
    member x.Required
        with get() = required
        and set v = required <- v

    member x.Boot
        with get() = boot
        and set v = boot <- v

    member x.Shutdown
        with get() = shutdown
        and set v = shutdown <- v

    member x.Callbacks
        with get() = callbacks
        and set v = callbacks <- v

    member x.Channels
        with get() = channels
        and set v = channels <- v

    member x.WithAttributesFrom(other : DomNode<'msg>) =
        required <- other.Required
        boot <- other.Boot
        shutdown <- other.Shutdown
        callbacks <- other.Callbacks
        channels <- other.Channels
        x

    abstract member Visit : DomNodeVisitor<'msg, 'r> -> 'r

    member x.Clone() =
        x.Visit {
            new DomNodeVisitor<'msg, DomNode<'msg>> with
                member x.Empty e    = DomNode.Empty().WithAttributesFrom e
                member x.Inner n    = DomNode.Element(n.Tag, n.Namespace, n.Attributes, n.Children).WithAttributesFrom n
                member x.Void n     = DomNode.Element(n.Tag, n.Namespace, n.Attributes).WithAttributesFrom n
                member x.Scene n    = DomNode.Scene(n.Attributes, n.Scene, n.GetClientState).WithAttributesFrom n
                member x.Text n     = DomNode.Text(n.Tag, n.Namespace, n.Attributes, n.Text).WithAttributesFrom n
                member x.Page n     = DomNode.Page(n.Content).WithAttributesFrom n
                member x.SubApp n   = DomNode.SubApp(n.App).WithAttributesFrom n
                member x.Map n      = DomNode.Map(n.Mapping, n.Node).WithAttributesFrom n
        }

    member x.WithAttributes(additional : AttributeMap<'msg>) =
        let (|||) a b = AttributeMap.union a b
        x.Visit {
            new DomNodeVisitor<'msg, DomNode<'msg>> with
                member x.Empty e    = DomNode.Empty().WithAttributesFrom e
                member x.Inner n    = DomNode.Element(n.Tag, n.Namespace, n.Attributes ||| additional, n.Children).WithAttributesFrom n
                member x.Void n     = DomNode.Element(n.Tag, n.Namespace, n.Attributes ||| additional).WithAttributesFrom n
                member x.Scene n    = DomNode.Scene(n.Attributes ||| additional, n.Scene, n.GetClientState).WithAttributesFrom n
                member x.Text n     = DomNode.Text(n.Tag, n.Namespace, n.Attributes ||| additional, n.Text).WithAttributesFrom n
                member x.Page n     = DomNode.Page(n.Content).WithAttributesFrom n
                member x.SubApp n   = DomNode.SubApp(n.App).WithAttributesFrom n
                member x.Map n      = DomNode.Map(n.Mapping, n.Node).WithAttributesFrom n
        }
        
    member x.WithRequired r =
        let res = x.Clone()
        res.Required <- r
        res

    member x.WithBoot r =
        let res = x.Clone()
        res.Boot <- r
        res

    member x.WithShutdown r =
        let res = x.Clone()
        res.Shutdown <- r
        res

    member x.WithCallbacks r =
        let res = x.Clone()
        res.Callbacks <- r
        res

    member x.WithChannels r =
        let res = x.Clone()
        res.Channels <- r
        res

    member x.AddRequired r = x.WithRequired (x.Required @ r)
    member x.AddBoot r = 
        match x.Boot with
            | None -> x.WithBoot (Some r)
            | Some b -> x.WithBoot (Some (fun self -> b self + ";" + r self))
    member x.AddShutdown r = 
        match x.Shutdown with
            | None -> x.WithShutdown (Some r)
            | Some b -> x.WithShutdown (Some (fun self -> b self + ";" + r self))
    member x.AddCallbacks r = x.WithCallbacks (Map.union x.Callbacks r)
    member x.AddChannels r = x.WithChannels (Map.union x.Channels r)


and EmptyNode<'msg>() =
    inherit DomNode<'msg>()
    override x.Visit v = v.Empty x

and InnerNode<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    member x.Children = children
    override x.Visit v = v.Inner x

and VoidNode<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    override x.Visit v = v.Void x

and SceneNode<'msg>(attributes : AttributeMap<'msg>, scene : Aardvark.Service.Scene, getClientState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState) =
    inherit DomNode<'msg>()
    member x.Attributes = attributes
    member x.Scene = scene
    member x.GetClientState i = getClientState i
    override x.Visit v = v.Scene x

and TextNode<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>, text : IMod<string>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    member x.Text = text
    override x.Visit v = v.Text x

and PageNode<'msg>(content : Request -> DomNode<'msg>) =
    inherit DomNode<'msg>()
    member x.Content = content
    override x.Visit v = v.Page x

and SubAppNode<'model, 'inner, 'outer>(app : IApp<'model, 'inner, 'outer>) =
    inherit DomNode<'outer>()
    member x.App : IApp<'model, 'inner, 'outer> = app
    override x.Visit v = v.SubApp x

and MapNode<'inner, 'outer>(mapping : 'inner -> 'outer, node : DomNode<'inner>) =
    inherit DomNode<'outer>()
    member x.Mapping : 'inner -> 'outer = mapping
    member x.Node : DomNode<'inner> = node
    override x.Visit v = v.Map x
    
and DomNodeVisitor<'msg, 'r> =
    abstract member Empty                   : EmptyNode<'msg> -> 'r
    abstract member Inner                   : InnerNode<'msg> -> 'r
    abstract member Void                    : VoidNode<'msg> -> 'r
    abstract member Scene                   : SceneNode<'msg> -> 'r
    abstract member Text                    : TextNode<'msg> -> 'r
    abstract member Page                    : PageNode<'msg> -> 'r
    abstract member SubApp<'model, 'inner>  : SubAppNode<'model, 'inner, 'msg> -> 'r
    abstract member Map<'inner>             : MapNode<'inner, 'msg> -> 'r

and DomNode private() =
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

    static member Empty<'msg>() : DomNode<'msg> = 
        EmptyNode<'msg>() :> DomNode<'msg>

    static member Element<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) : DomNode<'msg> =
        InnerNode<'msg>(tag, ns, attributes, children) :> DomNode<_>

    static member Element<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>) : DomNode<'msg> =
        VoidNode(tag, ns, attributes) :> DomNode<_>
        
    static member Scene<'msg>(attributes : AttributeMap<'msg>, scene : Aardvark.Service.Scene, getClientState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState) : DomNode<'msg> =
        SceneNode(attributes, scene, getClientState) :> DomNode<_>

    static member Text<'msg>(tag : string, ns : Option<string>, attributes : AttributeMap<'msg>, text : IMod<string>) : DomNode<'msg> =
        TextNode(tag, ns, attributes, text) :> DomNode<_>

    static member Page<'msg>(content : Request -> DomNode<'msg>) : DomNode<'msg> =
        PageNode(content) :> DomNode<_>

    static member Map<'inner, 'outer>(mapping : 'inner -> 'outer, node : DomNode<'inner>) : DomNode<'outer> =
        MapNode<'inner, 'outer>(mapping, node) :> DomNode<_>

    static member SubApp<'model, 'inner, 'outer>(app : IApp<'model, 'inner, 'outer>) : DomNode<'outer> =
        SubAppNode<'model, 'inner, 'outer>(app) :> DomNode<_>
        
    static member Text(content : IMod<string>) = 
        DomNode.Text("span", None, AttributeMap.empty, content)
        
    static member SvgText(content : IMod<string>) = 
        DomNode.Text("tspan", Some "http://www.w3.org/2000/svg", AttributeMap.empty, content)

    static member Void(tag : string, attributes : AttributeMap<'msg>) =
        DomNode.Element(tag, None, attributes)     

    static member Node(tag : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
        DomNode.Element(tag, None, attributes, content)

    static member Void(tag : string, ns : string, attributes : AttributeMap<'msg>) =
        DomNode.Element(tag, Some ns, attributes)     

    static member Node(tag : string, ns : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
       DomNode.Element(tag, Some ns, attributes, content)

    static member RenderControl(attributes : AttributeMap<'msg>, processor : SceneEventProcessor<'msg>, getState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState, 
                                scene : Aardvark.Service.Scene, htmlChildren : Option<DomNode<_>>) =


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
            DomNode.Scene(ownAttributes, scene, getState).WithBoot(Some boot)
        | None -> 
            DomNode.Scene(ownAttributes, scene, getState).WithBoot(Some boot)



    static member RenderControl(attributes : AttributeMap<'msg>, getState : Aardvark.Service.ClientInfo -> Aardvark.Service.ClientState, 
                                scene : Aardvark.Service.Scene, htmlChildren : Option<DomNode<_>>) =
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
    
    

    

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, sg : ClientValues -> ISg<'msg>, config: RenderControlConfig, htmlChildren : Option<DomNode<_>>) =

        let getState(c : Aardvark.Service.ClientInfo) =
            let cam = camera.GetValue(c.token)
            let cam = { cam with frustum = config.adjustAspect c.size cam.frustum }
            {
                viewTrafo = CameraView.viewTrafo cam.cameraView
                projTrafo = Frustum.projTrafo cam.frustum
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

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, sg : ISg<'msg>, config: RenderControlConfig, htmlChildren : Option<DomNode<_>>) =
        DomNode.RenderControl(attributes, camera, constF sg, config, htmlChildren)

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


    static member RenderControl(camera : IMod<Camera>, scene : ISg<'msg>, config : RenderControlConfig, ?htmlChildren : DomNode<_>) : DomNode<'msg> =
        DomNode.RenderControl(AttributeMap.empty, camera, constF scene, config, htmlChildren)

    static member RenderControl(camera : IMod<Camera>, scene : ClientValues -> ISg<'msg>, config : RenderControlConfig, ?htmlChildren : DomNode<_>) : DomNode<'msg> =
        DomNode.RenderControl(AttributeMap.empty, camera, scene, config, htmlChildren)




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



