namespace Aardvark.UI

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive

[<AbstractClass>]
type DomNode<'msg>() =
    let mutable required : list<Reference> = []
    let mutable boot : ValueOption<string -> string> = ValueNone
    let mutable shutdown : ValueOption<string -> string> = ValueNone
    let mutable callbacks : Map<string, list<string> -> 'msg> = Map.empty
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
    abstract member NodeTag : Option<string>

    member x.Clone() =
        x.Visit {
            new DomNodeVisitor<'msg, DomNode<'msg>> with
                member x.Empty e    = DomNode.Empty().WithAttributesFrom e
                member x.Inner n    = DomNode.Element(n.Tag, n.Namespace, n.Attributes, n.Children).WithAttributesFrom n
                member x.Void n     = DomNode.Element(n.Tag, n.Namespace, n.Attributes).WithAttributesFrom n
                member x.Scene n    = DomNode.Scene(n.Attributes, n.Scene, n.GetState).WithAttributesFrom n
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
                member x.Scene n    = DomNode.Scene(n.Attributes ||| additional, n.Scene, n.GetState).WithAttributesFrom n
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
        | ValueNone -> x.WithBoot (ValueSome r)
        | ValueSome b -> x.WithBoot (ValueSome (fun self -> b self + ";" + r self))
    member x.AddShutdown r =
        match x.Shutdown with
        | ValueNone -> x.WithShutdown (ValueSome r)
        | ValueSome b -> x.WithShutdown (ValueSome (fun self -> b self + ";" + r self))
    member x.AddCallbacks r = x.WithCallbacks (Map.union x.Callbacks r)
    member x.AddChannels r = x.WithChannels (Map.union x.Channels r)

    interface IDomNode<'msg>

and EmptyNode<'msg>() =
    inherit DomNode<'msg>()
    override x.Visit v = v.Empty x
    override x.NodeTag = None

and InnerNode<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    member x.Children = children
    override x.Visit v = v.Inner x
    override x.NodeTag = Some tag

and VoidNode<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    override x.Visit v = v.Void x
    override x.NodeTag = Some tag

and SceneNode<'msg>(attributes : AttributeMap<'msg>, scene : Scene, getState : RenderClientInfo -> RenderState) =
    inherit DomNode<'msg>()
    member x.Attributes = attributes
    member x.Scene = scene
    member x.GetState i = getState i
    override x.Visit v = v.Scene x
    override x.NodeTag = None

and TextNode<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>, text : aval<string>) =
    inherit DomNode<'msg>()
    member x.Tag = tag
    member x.Namespace = ns
    member x.Attributes = attributes
    member x.Text = text
    override x.Visit v = v.Text x
    override x.NodeTag = Some tag

and PageNode<'msg>(content : HttpRequest -> DomNode<'msg>) =
    inherit DomNode<'msg>()
    member x.Content = content
    override x.Visit v = v.Page x
    override x.NodeTag = None

and SubAppNode<'model, 'inner, 'outer>(app : ISubApp<'model, 'inner, 'outer>) =
    inherit DomNode<'outer>()
    member x.App : ISubApp<'model, 'inner, 'outer> = app
    override x.Visit v = v.SubApp x
    override x.NodeTag = None

and MapNode<'inner, 'outer>(mapping : 'inner -> 'outer, node : DomNode<'inner>) =
    inherit DomNode<'outer>()
    member x.Mapping : 'inner -> 'outer = mapping
    member x.Node : DomNode<'inner> = node
    override x.Visit v = v.Map x
    override x.NodeTag = node.NodeTag

and DomNodeVisitor<'msg, 'r> =
    abstract member Empty                   : EmptyNode<'msg> -> 'r
    abstract member Inner                   : InnerNode<'msg> -> 'r
    abstract member Void                    : VoidNode<'msg> -> 'r
    abstract member Scene                   : SceneNode<'msg> -> 'r
    abstract member Text                    : TextNode<'msg> -> 'r
    abstract member Page                    : PageNode<'msg> -> 'r
    abstract member SubApp<'model, 'inner>  : SubAppNode<'model, 'inner, 'msg> -> 'r
    abstract member Map<'inner>             : MapNode<'inner, 'msg> -> 'r

and [<AbstractClass; Sealed>] DomNode =

    static member Empty<'msg>() : DomNode<'msg> =
        EmptyNode<'msg>() :> DomNode<'msg>

    static member Element<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) : DomNode<'msg> =
        InnerNode<'msg>(tag, ns, attributes, children) :> DomNode<_>

    static member Element<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>) : DomNode<'msg> =
        VoidNode(tag, ns, attributes) :> DomNode<_>

    static member Scene<'msg>(attributes : AttributeMap<'msg>, scene : Scene, getClientState : RenderClientInfo -> RenderState) : DomNode<'msg> =
        SceneNode(attributes, scene, getClientState) :> DomNode<_>

    static member Text<'msg>(tag : string, ns : string, attributes : AttributeMap<'msg>, text : aval<string>) : DomNode<'msg> =
        TextNode(tag, ns, attributes, text) :> DomNode<_>

    static member Page<'msg>(content : HttpRequest -> DomNode<'msg>) : DomNode<'msg> =
        PageNode(content) :> DomNode<_>

    static member Map<'inner, 'outer>(mapping : 'inner -> 'outer, node : DomNode<'inner>) : DomNode<'outer> =
        MapNode<'inner, 'outer>(mapping, node) :> DomNode<_>

    static member SubApp<'model, 'inner, 'outer> (app : ISubApp<'model, 'inner, 'outer>) : DomNode<'outer> =
        SubAppNode<'model, 'inner, 'outer>(app) :> DomNode<_>

    static member Text(content : aval<string>) =
        DomNode.Text("span", null, AttributeMap.empty, content)

    static member SvgText(content : aval<string>) =
        DomNode.Text("tspan", "http://www.w3.org/2000/svg", AttributeMap.empty, content)

    static member Void(tag : string, attributes : AttributeMap<'msg>) =
        DomNode.Element(tag, null, attributes)

    static member Node(tag : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
        DomNode.Element(tag, null, attributes, content)

    static member Void(tag : string, ns : string, attributes : AttributeMap<'msg>) =
        DomNode.Element(tag, ns, attributes)

    static member Node(tag : string, ns : string, attributes : AttributeMap<'msg>, content : alist<DomNode<'msg>>) =
       DomNode.Element(tag, ns, attributes, content)

module UI =
    let inline map (mapping: 'T1 -> 'T2) (source: DomNode<'T1>) : DomNode<'T2> =
        DomNode.Map(mapping, source)