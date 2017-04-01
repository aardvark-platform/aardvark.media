namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Base.Incremental
open Aardvark.Application

[<AutoOpen>]
module private Utils =
    open Aardvark.Base.TypeInfo

    let typename<'a> = Aardvark.Base.ReflectionHelpers.getPrettyName typeof<'a>

module Pickler =
    let json = Aardvark.Service.Pickler.json


type Event<'msg> =
    {
        clientSide : (string -> list<string> -> string) -> string -> string
        serverSide : Guid -> string -> list<string> -> list<'msg>
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
            serverSide = fun _ _ _ -> []
        }

    let ofTrigger (reaction : unit -> 'msg) =
        {
            clientSide = fun send id -> send id []
            serverSide = fun _ _ _ -> [reaction()]
        }

    let ofDynamicArgs (args : list<string>) (reaction : list<string> -> list<'msg>) =
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
                            [ reaction (Pickler.json.UnPickleOfString a)]
                        with e ->
                            Log.warn "[UI] expected args (%s) but got (%A)" typename<'a> a
                            []

                    | _ -> 
                        Log.warn "[UI] expected args (%s) but got %A" typename<'a> args
                        []
        }

    let create2 (a : string) (b : string) (reaction : 'a -> 'b -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b]
            serverSide = fun session id args -> 
                match args with
                    | [a; b] ->
                        try 
                            [ reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) ]
                        with e ->
                            Log.warn "[UI] expected args (%s, %s) but got (%A, %A)" typename<'a> typename<'b> a b
                            []

                    | _ -> 
                        Log.warn "[UI] expected args (%s, %s) but got %A" typename<'a> typename<'b> args
                        []
        }

    let create3 (a : string) (b : string) (c : string) (reaction : 'a -> 'b -> 'c -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b; c]
            serverSide = fun session id args -> 
                match args with
                    | [a; b; c] ->
                        try 
                            [ reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) (Pickler.json.UnPickleOfString c) ]
                        with e ->
                            Log.warn "[UI] expected args (%s, %s, %s) but got (%A, %A, %A)" typename<'a> typename<'b> typename<'c> a b c
                            []

                    | _ -> 
                        Log.warn "[UI] expected args (%s, %s, %s) but got %A" typename<'a> typename<'b> typename<'c> args
                        []
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
                        []
                
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
                                        []
                            | [] ->
                                Log.warn "[UI] expected at least one arg for dispatcher"
                                []
                        
                        
                
                }

[<RequireQualifiedAccess>]
type AttributeValue<'msg> =
    | String of string
    | Event of Event<'msg>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttributeValue =
    
    let combine (name : string) (l : AttributeValue<'msg>) (r : AttributeValue<'msg>) =
        match name, l, r with
            | _, AttributeValue.Event l, AttributeValue.Event r -> 
                AttributeValue.Event (Event.combine l r)

            | "class", AttributeValue.String l, AttributeValue.String r -> 
                AttributeValue.String (l + " " + r)

            | _ -> 
                r


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

    let choose (mapping : string -> AttributeValue<'a> -> Option<AttributeValue<'b>>) (map : AttributeMap<'a>) =
        AttributeMap(AMap.choose mapping map.AMap)

    let filter (predicate : string -> AttributeValue<'a> -> bool) (map : AttributeMap<'a>) =
        AttributeMap(AMap.filter predicate map.AMap)



type ReferenceKind =
    | Script 
    | Stylesheet

type Reference = { kind : ReferenceKind; name : string; url : string }



type SceneEventKind =
    | Enter
    | Leave
    | Move
    | Click
    | DoubleClick
    | Down
    | Up

type SceneEvent =
    {
        kind    : SceneEventKind
        ray     : RayPart
        buttons : MouseButtons
    }


[<AbstractClass>]
type SceneEventProcessor<'msg>() =

    static let empty =
        { new SceneEventProcessor<'msg>() with
            member x.NeededEvents = ASet.empty
            member x.Process _ = []
        }

    static member Empty = empty


    abstract member NeededEvents : aset<SceneEventKind>
    abstract member Process : evt : SceneEvent -> list<'msg>

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
            override x.Process e = inner |> List.collect (fun i -> i.Process e)
    
    let empty<'msg> = SceneEventProcessor<'msg>.Empty

    let union (l : SceneEventProcessor<'msg>) (r : SceneEventProcessor<'msg>) =
        UnionProcessor [l;r] :> SceneEventProcessor<'msg>

    let unionMany (processors : seq<SceneEventProcessor<'msg>>) =
        processors |> Seq.toList |> UnionProcessor :> SceneEventProcessor<'msg>



[<Sealed>]
type DomNode<'msg>(tag : string, attributes : AttributeMap<'msg>, content : DomContent<'msg>) =
    let mutable required : list<Reference> = []
    let mutable boot : Option<string -> string> = None
    let mutable shutdown : Option<string -> string> = None
    let mutable callbacks : Map<string, (list<string> -> 'msg)> = Map.empty

    member x.Tag = tag
    member x.Content = content

    abstract member Attributes : AttributeMap<'msg>
    default x.Attributes = attributes

    
    member x.Required
        with get() = required
        and private set r = required <- r

    member x.Boot
        with get() = boot
        and private set code = boot <- code

    member x.Shutdown
        with get() = shutdown
        and private set code = shutdown <- code

    member x.Callbacks
        with get() = callbacks
        and private set cbs = callbacks <- cbs

    member private x.Copy() =
        DomNode<'msg>(
            x.Tag,
            x.Attributes,
            x.Content,
            Required = x.Required,
            Boot = x.Boot,
            Shutdown = x.Shutdown,
            Callbacks = x.Callbacks
        )


    member x.WithRequired (required : list<Reference>) =
        let res = x.Copy()
        res.Required <- required
        res

    member x.WithBoot (boot : Option<string -> string>) =
        let res = x.Copy()
        res.Boot <- boot
        res

    member x.WithShutdown (shutdown : Option<string -> string>) =
        let res = x.Copy()
        res.Shutdown <- shutdown
        res

    member x.WithCallbacks (callbacks : Map<string, list<string> -> 'msg>) =
        let res = x.Copy()
        res.Callbacks <- callbacks
        res

and DomContent<'msg> =
    | Empty
    | Children of alist<DomNode<'msg>>
    | Scene of Aardvark.Service.Scene * (Aardvark.Service.ClientInfo -> Camera)
    | Text of IMod<string>

open Aardvark.Service

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
            | 0 -> MouseButtons.Left
            | 1 -> MouseButtons.Middle
            | 2 -> MouseButtons.Right
            | _ -> MouseButtons.None

    static member Text(content : IMod<string>) = 
        DomNode<'msg>("span", AttributeMap.empty, DomContent.Text content)

    static member Void(tag : string, attributes : AttributeMap<'msg>) =
        DomNode<'msg>(tag, attributes, DomContent.Empty)
        

    static member Node(tag : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
        DomNode<'msg>(tag, attributes, DomContent.Children content)

    static member RenderControl(attributes : AttributeMap<'msg>, processor : SceneEventProcessor<'msg>, camera : Aardvark.Service.ClientInfo -> Camera,scene : Aardvark.Service.Scene) =

        let perform (sourceSession : Guid, sourceId : string, kind : SceneEventKind, buttons : MouseButtons, pos : V2i) : list<'msg> =
            match scene.TryGetClientInfo(sourceSession, sourceId) with
                | Some (info, camera) -> 
                    let pp = PixelPosition(pos.X, pos.Y, info.size.X, info.size.Y)
                    let ray = Camera.pickRay camera pp |> FastRay3d |> RayPart

                    processor.Process {
                        kind    = kind
                        ray     = ray 
                        buttons = buttons
                    } 
                | None ->
                    Log.warn "[UI] could not get client info for %A/%s" sourceSession sourceId
                    []

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
                            let x = round (float x) |> int
                            let y = round (float y) |> int
                            perform(session, id, kind, MouseButtons.None, V2i(x,y))

                        | _ ->
                            []
                        
            }

        let events =
            processor.NeededEvents 
                |> ASet.choose (fun k -> Map.tryFind k eventNames) 
                |> AMap.mapSet (fun name ->
                    match Map.tryFind name eventKinds with
                        | Some kind ->
                            let button = needsButton kind
                            AttributeValue.Event(rayEvent button kind)
                        | _ ->
                            failwithf "[Scene] unknown event %A" name
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

        DomNode<'msg>("div", ownAttributes, DomContent.Scene(scene, camera)).WithBoot(Some boot)

    static member RenderControl(attributes : AttributeMap<'msg>, camera : Aardvark.Service.ClientInfo -> Camera, scene : Aardvark.Service.Scene) =
        DomNode.RenderControl(attributes, SceneEventProcessor.empty, camera, scene)
    
    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, scene : Aardvark.Service.Scene) =
        let getCamera(c : Aardvark.Service.ClientInfo) =
            let cam = camera.GetValue(c.token)
            { cam with frustum = cam.frustum |> Frustum.withAspect (float c.size.X / float c.size.Y) }
        DomNode.RenderControl(attributes, SceneEventProcessor.empty, getCamera, scene)

    static member RenderControl(attributes : AttributeMap<'msg>, camera : IMod<Camera>, scene : ISg) =
        let scene =
            Scene.custom (fun values ->
                let sg =
                    scene
                        |> Sg.viewTrafo (values.camera |> Mod.map CameraView.viewTrafo)
                        |> Sg.projTrafo (values.frustum |> Mod.map Frustum.projTrafo)
                        |> Sg.uniform "ViewportSize" values.size

                values.runtime.CompileRender(values.signature, sg)
            )

        DomNode.RenderControl(attributes, camera, scene)

    static member RenderControl(camera : IMod<Camera>, scene : ISg) : DomNode<'msg> =
        DomNode.RenderControl(AttributeMap.empty, camera, scene)







