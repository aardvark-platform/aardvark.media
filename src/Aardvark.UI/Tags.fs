namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Incremental


module Incremental =

    let renderControl (cam : IMod<Camera>) (attributes : AttributeMap<'msg>) (sg : ISg<'msg>) =
        DomNode.RenderControl(attributes, cam, sg, RenderControlConfig.standard, None)

    let renderControl' (cam : IMod<Camera>) (attributes : AttributeMap<'msg>) (config : RenderControlConfig) (sg : ISg<'msg>) =
        DomNode.RenderControl(attributes, cam, sg, config, None)

    let renderControlWithClientValues (cam : IMod<Camera>) (attributes : AttributeMap<'msg>) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        DomNode.RenderControl(attributes, cam, sg, RenderControlConfig.standard, None)

    let renderControlWithClientValues' (cam : IMod<Camera>) (attributes : AttributeMap<'msg>) (config : RenderControlConfig) (sg : Aardvark.Service.ClientValues -> ISg<'msg>) =
        DomNode.RenderControl(attributes, cam, sg, config, None)

    let inline elem (tagName : string) (attrs : AttributeMap<'msg>) (children : alist<DomNode<'msg>>) =
        DomNode.Node(tagName, attrs, children)

    let inline voidElem (tagName : string) (attrs : AttributeMap<'msg>) = 
        DomNode.Void(tagName, attrs)

    let inline elemNS (tagName : string) (ns : string) (attrs : AttributeMap<'msg>) (children : alist<DomNode<'msg>>) =
        DomNode.Node(tagName, ns, attrs, children)

    let inline voidElemNS (tagName : string) (ns : string) (attrs : AttributeMap<'msg>) = 
        DomNode.Void(tagName, ns, attrs)
   
    let inline text (content : IMod<string>) =
        DomNode.Text(content)
        
    // Elements - list of elements here: https://developer.mozilla.org/en-US/docs/Web/HTML/Element
    // Void elements
    let inline br x = voidElem "br" x
    let inline area x = voidElem "area" x
    let inline baseHtml x = voidElem "base" x
    let inline col x = voidElem "col" x
    let inline embed x = voidElem "embed" x
    let inline hr x = voidElem "hr" x
    let inline img x = voidElem "img" x
    let inline input x = voidElem "input" x
    let inline link x = voidElem "link" x
    let inline meta x = voidElem "meta" x
    let inline param x = voidElem "param" x
    let inline source x = voidElem "source" x
    let inline track x = voidElem "track" x
    let inline wbr x = voidElem "wbr" x

    // Metadata
    let inline head x = elem "head" x
    let inline style x = elem "style" x
    let inline title x = elem "title" x

    // Content sectioning
    let inline address x = elem "address" x
    let inline article x = elem "article" x
    let inline aside x = elem "aside" x
    let inline footer x = elem "footer" x
    let inline header x = elem "header" x
    let inline h1 x = elem "h1" x
    let inline h2 x = elem "h2" x
    let inline h3 x = elem "h3" x
    let inline h4 x = elem "h4" x
    let inline h5 x = elem "h5" x
    let inline h6 x = elem "h6" x
    let inline hgroup x = elem "hgroup" x
    let inline nav x = elem "nav" x

    // Text content
    let inline dd x = elem "dd" x
    let inline div x = elem "div" x
    let inline div' x children = elem "div" x (AList.ofList children)
    let inline dl x = elem "dl" x
    let inline dt x = elem "dt" x
    let inline figcaption x = elem "figcaption" x
    let inline figure x = elem "figure" x
    let inline li x = elem "li" x
    let inline main x = elem "main" x
    let inline ol x = elem "ol" x
    let inline p x = elem "p" x
    let inline pre a x = DomNode.Text("pre", None, a, x)
    let inline section x = elem "section" x
    let inline ul x = elem "ul" x

    // Inline text semantics
    let inline a x = elem "a" x
    let inline abbr x = elem "abbr" x
    let inline b x = elem "b" x
    let inline bdi x = elem "bdi" x
    let inline bdo x = elem "bdo" x
    let inline cite x = elem "cite" x
    let inline code x = elem "code" x
    let inline data x = elem "data" x
    let inline dfn x = elem "dfn" x
    let inline em x = elem "em" x
    let inline i x = elem "i" x
    let inline kbd x = elem "kbd" x
    let inline mark x = elem "mark" x
    let inline q x = elem "q" x
    let inline rp x = elem "rp" x
    let inline rt x = elem "rt" x
    let inline rtc x = elem "rtc" x
    let inline ruby x = elem "ruby" x
    let inline s x = elem "s" x
    let inline samp x = elem "samp" x
    let inline small x = elem "small" x
    let inline span x = elem "span" x
    let inline strong x = elem "strong" x
    let inline sub x = elem "sub" x
    let inline sup x = elem "sup" x
    let inline time x = elem "time" x
    let inline u x = elem "u" x
    let inline var x = elem "var" x

    // Image and multimedia
    let inline audio x = elem "audio" x
    let inline map x = elem "map" x
    let inline video x = elem "video" x

    // Embedded content
    let inline objectHtml x = elem "object" x

    // Demarcasting edits
    let inline del x = elem "del" x
    let inline ins x = elem "ins" x

    // Table content
    let inline caption x = elem "caption" x
    let inline colgroup x = elem "colgroup" x
    let inline table x = elem "table" x
    let inline tbody x = elem "tbody" x
    let inline td x = elem "td" x
    let inline tfoot x = elem "tfoot" x
    let inline th x = elem "th" x
    let inline thead x = elem "thead" x
    let inline tr x = elem "tr" x

    // Forms
    let inline button x = elem "button" x
    let inline datalist x = elem "datalist" x
    let inline fieldset x = elem "fieldset" x
    let inline form x = elem "form" x
    let inline label x = elem "label" x
    let inline legend x = elem "legend" x
    let inline meter x = elem "meter" x
    let inline optgroup x = elem "optgroup" x
    let inline option x = elem "option" x
    let inline output x = elem "output" x
    let inline progress x = elem "progress" x
    let inline select x = elem "select" x
    let inline textarea a x = DomNode.Text("textarea", None, a, x)

    // Interactive elements
    let inline details x = elem "details" x
    let inline dialog x = elem "dialog" x
    let inline menu x = elem "menu" x
    let inline menuitem x = elem "menuitem" x
    let inline summary x = elem "summary" x

    module Svg =
        [<Literal>]
        let svgNS = "http://www.w3.org/2000/svg"
        let inline attribute k v = k,v

        let inline svg x = elemNS "svg" svgNS x
        let inline defs x = elemNS "defs" svgNS x
        let inline linearGradient  x = elemNS "linearGradient" svgNS x
        let inline radialGradient  x = elemNS "radialGradient" svgNS x
        let inline text x c = elemNS "text" svgNS x (AList.ofList [DomNode.SvgText(c)])
        let inline filter x = elemNS "filter" svgNS x

        let inline feBlend x = voidElemNS "feBlend" svgNS x
        let inline feColorMatrix x = voidElemNS "feColorMatrix" svgNS x
        let inline feComponentTransfer x = voidElemNS "feComponentTransfer" svgNS x
        let inline feComposite x = voidElemNS "feComposite" svgNS x
        let inline feConvolveMatrix x = voidElemNS "feConvolveMatrix" svgNS x
        let inline feDiffuseLighting x = voidElemNS "feDiffuseLighting" svgNS x
        let inline feDisplacementMap x = voidElemNS "feDisplacementMap" svgNS x
        let inline feDistantLight x = voidElemNS "feDistantLight" svgNS x
        let inline feFlood x = voidElemNS "feFlood" svgNS x
        let inline feFuncA x = voidElemNS "feFuncA" svgNS x
        let inline feFuncB x = voidElemNS "feFuncB" svgNS x
        let inline feFuncG x = voidElemNS "feFuncG" svgNS x
        let inline feFuncR x = voidElemNS "feFuncR" svgNS x
        let inline feGaussianBlur x = voidElemNS "feGaussianBlur" svgNS x
        let inline feImage x = voidElemNS "feImage" svgNS x
        let inline feMerge x = voidElemNS "feMerge" svgNS x
        let inline feMergeNode x = voidElemNS "feMergeNode" svgNS x
        let inline feMorphology x = voidElemNS "feMorphology" svgNS x
        let inline feOffset x = voidElemNS "feOffset" svgNS x
        let inline fePointLight x = voidElemNS "fePointLight" svgNS x
        let inline feSpecularLighting x = voidElemNS "feSpecularLighting" svgNS x
        let inline feSpotLight x = voidElemNS "feSpotLight" svgNS x
        let inline feTile x = voidElemNS "feTile" svgNS x
        let inline feTurbulence x = voidElemNS "feTurbulence" svgNS x

        let inline stop x = voidElemNS "stop" svgNS x
        let inline circle x = voidElemNS "circle" svgNS x 
        let inline ellipse x = voidElemNS "ellipse" svgNS x 
        let inline rect x = voidElemNS "rect" svgNS x 
        let inline line x = voidElemNS "line" svgNS x
        let inline path x = voidElemNS "path" svgNS x
        let inline polygon x = voidElemNS "polygon" svgNS x
        let inline polyline x = voidElemNS "polyline" svgNS x
        let inline tspan x = voidElemNS "tspan" svgNS x

        let inline width x = attribute "width" x
        let inline height x = attribute "height" x
        let inline viewBox x = attribute "viewBox" x
        let inline cx x = attribute "cx" x
        let inline cy x = attribute "cy" x
        let inline r x = attribute "r" x
        let inline stroke x = attribute "stroke" x
        let inline strokeWidth x = attribute "stroke-width" x
        let inline strokeLinecap x = attribute "stroke-linecap" x
        let inline strokeDasharray x = attribute "stroke-dasharray" x
        let inline fill x = attribute "fill" x

[<AutoOpen>]
module Static =

    let subApp (app : App<'model,'mmodel,'innermsg>) : DomNode<'msg> =
        DomNode.SubApp
            { new IApp<'model,'innermsg,'msg> with
                member x.Start() = app.start()
                member x.ToInner (_,_)= Seq.empty
                member x.ToOuter (_,_) = Seq.empty
            }

    let subApp' (mapOut : 'model -> 'innermsg -> seq<'msg>) (mapIn : 'model -> 'msg -> seq<'innermsg>) (att : list<string * AttributeValue<'msg>>) (app : App<'model,'mmodel,'innermsg>) : DomNode<'msg> =
        DomNode.SubApp
            { new IApp<'model,'innermsg,'msg> with
                member x.Start() = app.start()
                member x.ToInner (model, msg) = mapIn model msg
                member x.ToOuter (model, msg) = mapOut model msg
            }


    let renderControl (cam : IMod<Camera>) (attributes : list<string * AttributeValue<'msg>>) (sg : ISg<'msg>) =
        DomNode.RenderControl(AttributeMap.ofList attributes, cam, sg, RenderControlConfig.standard, None)

    let renderControl' (cam : IMod<Camera>) (attributes : list<string * AttributeValue<'msg>>) (config : RenderControlConfig) (sg : ISg<'msg>) =
        DomNode.RenderControl(AttributeMap.ofList attributes, cam, sg, config, None)

    let page (createPage : Request -> DomNode<'msg>) =
        DomNode.Page createPage

    let inline elem (tagName : string) (attrs : list<string * AttributeValue<'msg>>) (children : list<DomNode<'msg>>) =
        DomNode.Node(tagName, AttributeMap.ofList attrs, AList.ofList children)

    let inline voidElem (tagName : string) (attrs : list<string * AttributeValue<'msg>>) = 
        DomNode.Void(tagName, AttributeMap.ofList attrs)

    let inline elemNS (tagName : string) (ns : string) (attrs : list<string * AttributeValue<'msg>>) (children : list<DomNode<'msg>>) =
        DomNode.Node(tagName, ns, AttributeMap.ofList attrs, AList.ofList children)

    let inline voidElemNS (tagName : string) (ns : string) (attrs : list<string * AttributeValue<'msg>>) = 
        DomNode.Void(tagName, ns, AttributeMap.ofList attrs)
   
    let inline text (content : string) =
        DomNode.Text(Mod.constant content)

    // Elements - list of elements here: https://developer.mozilla.org/en-US/docs/Web/HTML/Element
    // Void elements
    let inline br x = voidElem "br" x
    let inline area x = voidElem "area" x
    let inline baseHtml x = voidElem "base" x
    let inline col x = voidElem "col" x
    let inline embed x = voidElem "embed" x
    let inline hr x = voidElem "hr" x
    let inline img x = voidElem "img" x
    let inline input x = voidElem "input" x
    let inline link x = voidElem "link" x
    let inline meta x = voidElem "meta" x
    let inline param x = voidElem "param" x
    let inline source x = voidElem "source" x
    let inline track x = voidElem "track" x
    let inline wbr x = voidElem "wbr" x

    // Metadata
    let inline head x = elem "head" x
    let inline title x = elem "title" x

    // Content sectioning
    let inline address x = elem "address" x
    let inline article x = elem "article" x
    let inline aside x = elem "aside" x
    let inline footer x = elem "footer" x
    let inline header x = elem "header" x
    let inline h1 x = elem "h1" x
    let inline h2 x = elem "h2" x
    let inline h3 x = elem "h3" x
    let inline h4 x = elem "h4" x
    let inline h5 x = elem "h5" x
    let inline h6 x = elem "h6" x
    let inline hgroup x = elem "hgroup" x
    let inline nav x = elem "nav" x

    // page content
    let inline body x = elem "body" x

    // Text content
    let inline dd x = elem "dd" x
    let inline div x = elem "div" x
    let inline dl x = elem "dl" x
    let inline dt x = elem "dt" x
    let inline figcaption x = elem "figcaption" x
    let inline figure x = elem "figure" x
    let inline li x = elem "li" x
    let inline main x = elem "main" x
    let inline ol x = elem "ol" x
    let inline p x = elem "p" x
    let inline pre a x = DomNode.Text("pre", None, AttributeMap.ofList a, Mod.constant x)
    let inline section x = elem "section" x
    let inline ul x = elem "ul" x

    // Inline text semantics
    let inline a x = elem "a" x
    let inline abbr x = elem "abbr" x
    let inline b x = elem "b" x
    let inline bdi x = elem "bdi" x
    let inline bdo x = elem "bdo" x
    let inline cite x = elem "cite" x
    let inline code x = elem "code" x
    let inline data x = elem "data" x
    let inline dfn x = elem "dfn" x
    let inline em x = elem "em" x
    let inline i x = elem "i" x
    let inline kbd x = elem "kbd" x
    let inline mark x = elem "mark" x
    let inline q x = elem "q" x
    let inline rp x = elem "rp" x
    let inline rt x = elem "rt" x
    let inline rtc x = elem "rtc" x
    let inline ruby x = elem "ruby" x
    let inline s x = elem "s" x
    let inline samp x = elem "samp" x
    let inline small x = elem "small" x
    let inline span x = elem "span" x
    let inline strong x = elem "strong" x
    let inline sub x = elem "sub" x
    let inline sup x = elem "sup" x
    let inline time x = elem "time" x
    let inline u x = elem "u" x
    let inline var x = elem "var" x

    // Image and multimedia
    let inline audio x = elem "audio" x
    let inline map x = elem "map" x
    let inline video x = elem "video" x

    // Embedded content
    let inline objectHtml x = elem "object" x

    // Demarcasting edits
    let inline del x = elem "del" x
    let inline ins x = elem "ins" x

    // Table content
    let inline caption x = elem "caption" x
    let inline colgroup x = elem "colgroup" x
    let inline table x = elem "table" x
    let inline tbody x = elem "tbody" x
    let inline td x = elem "td" x
    let inline tfoot x = elem "tfoot" x
    let inline th x = elem "th" x
    let inline thead x = elem "thead" x
    let inline tr x = elem "tr" x

    // Forms
    let inline button x = elem "button" x
    let inline datalist x = elem "datalist" x
    let inline fieldset x = elem "fieldset" x
    let inline form x = elem "form" x
    let inline label x = elem "label" x
    let inline legend x = elem "legend" x
    let inline meter x = elem "meter" x
    let inline optgroup x = elem "optgroup" x
    let inline option x = elem "option" x
    let inline output x = elem "output" x
    let inline progress x = elem "progress" x
    let inline select x = elem "select" x
    let inline textarea a x = DomNode.Text("textarea", None, AttributeMap.ofList a, Mod.constant x)

    // Interactive elements
    let inline details x = elem "details" x
    let inline dialog x = elem "dialog" x
    let inline menu x = elem "menu" x
    let inline menuitem x = elem "menuitem" x
    let inline summary x = elem "summary" x

    module Svg =
        [<Literal>]
        let svgNS = "http://www.w3.org/2000/svg"
        let inline attribute k v = k,v

        let inline svg x = elemNS "svg" svgNS x
        let inline defs x = elemNS "defs" svgNS x
        let inline linearGradient  x = elemNS "linearGradient" svgNS x
        let inline radialGradient  x = elemNS "radialGradient" svgNS x
        let inline text x c = elemNS "text" svgNS x [DomNode.SvgText(Mod.constant c)]
        let inline filter x = elemNS "filter" svgNS x

        let inline feBlend x = voidElemNS "feBlend" svgNS x
        let inline feColorMatrix x = voidElemNS "feColorMatrix" svgNS x
        let inline feComponentTransfer x = voidElemNS "feComponentTransfer" svgNS x
        let inline feComposite x = voidElemNS "feComposite" svgNS x
        let inline feConvolveMatrix x = voidElemNS "feConvolveMatrix" svgNS x
        let inline feDiffuseLighting x = voidElemNS "feDiffuseLighting" svgNS x
        let inline feDisplacementMap x = voidElemNS "feDisplacementMap" svgNS x
        let inline feDistantLight x = voidElemNS "feDistantLight" svgNS x
        let inline feFlood x = voidElemNS "feFlood" svgNS x
        let inline feFuncA x = voidElemNS "feFuncA" svgNS x
        let inline feFuncB x = voidElemNS "feFuncB" svgNS x
        let inline feFuncG x = voidElemNS "feFuncG" svgNS x
        let inline feFuncR x = voidElemNS "feFuncR" svgNS x
        let inline feGaussianBlur x = voidElemNS "feGaussianBlur" svgNS x
        let inline feImage x = voidElemNS "feImage" svgNS x
        let inline image x = voidElemNS "image" svgNS x
        let inline feMerge x = voidElemNS "feMerge" svgNS x
        let inline feMergeNode x = voidElemNS "feMergeNode" svgNS x
        let inline feMorphology x = voidElemNS "feMorphology" svgNS x
        let inline feOffset x = voidElemNS "feOffset" svgNS x
        let inline fePointLight x = voidElemNS "fePointLight" svgNS x
        let inline feSpecularLighting x = voidElemNS "feSpecularLighting" svgNS x
        let inline feSpotLight x = voidElemNS "feSpotLight" svgNS x
        let inline feTile x = voidElemNS "feTile" svgNS x
        let inline feTurbulence x = voidElemNS "feTurbulence" svgNS x

        let inline stop x = voidElemNS "stop" svgNS x
        let inline circle x = voidElemNS "circle" svgNS x 
        let inline ellipse x = voidElemNS "ellipse" svgNS x 
        let inline rect x = voidElemNS "rect" svgNS x 
        let inline line x = voidElemNS "line" svgNS x
        let inline path x = voidElemNS "path" svgNS x
        let inline polygon x = voidElemNS "polygon" svgNS x
        let inline polyline x = voidElemNS "polyline" svgNS x
        let inline tspan x = voidElemNS "tspan" svgNS x

        let inline width x = attribute "width" x
        let inline height x = attribute "height" x
        let inline viewBox x = attribute "viewBox" x
        let inline cx x = attribute "cx" x
        let inline cy x = attribute "cy" x
        let inline r x = attribute "r" x
        let inline stroke x = attribute "stroke" x
        let inline strokeWidth x = attribute "stroke-width" x
        let inline strokeLinecap x = attribute "stroke-linecap" x
        let inline strokeDasharray x = attribute "stroke-dasharray" x
        let inline fill x = attribute "fill" x


[<AutoOpen>]
module HigherOrderTags =
    
    let require (refs : list<Reference>) (node : DomNode<'msg>) =
        node.WithRequired(refs @ node.Required)
    
    let onBoot (code : string) (node : DomNode<'msg>) =
        let boot id = code.Replace("__ID__", id)
        match node.Boot with
            | None -> node.WithBoot (Some boot)
            | Some o -> node.WithBoot (Some (fun id -> boot id + "; " + o id))
    
    let onBoot' (channels : list<string * Channel>) (code : string) (node : DomNode<'msg>) =
        let boot id = code.Replace("__ID__", id)
        let n = Map.union node.Channels (Map.ofList channels)

        match node.Boot with
            | None -> node.WithBoot(Some boot).WithChannels n
            | Some o -> node.WithBoot(Some (fun id -> boot id + "; " + o id)).WithChannels n

module Generic =

    type Creator =
        // Children and dynamic attributes
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) =
            DomNode.Node(tag, attributes, children)
            
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, children : IMod<list<DomNode<'msg>>>) =
            DomNode.Node(tag, attributes, children |> Mod.map PList.ofList |> AList.ofMod)
            
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, children : IMod<plist<DomNode<'msg>>>) =
            DomNode.Node(tag, attributes, children |> AList.ofMod)

        static member inline Node(tag : string, attributes : AttributeMap<'msg>, children : list<DomNode<'msg>>) =
            DomNode.Node(tag, attributes, AList.ofList children)
 
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, children : plist<DomNode<'msg>>) =
            DomNode.Node(tag, attributes, AList.ofPList children)
            
            
        // Children and static attributes
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, children : alist<DomNode<'msg>>) =
            DomNode.Node(tag, AttributeMap.ofList attributes, children)
            
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, children : IMod<list<DomNode<'msg>>>) =
            DomNode.Node(tag, AttributeMap.ofList attributes, children |> Mod.map PList.ofList |> AList.ofMod)
            
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, children : IMod<plist<DomNode<'msg>>>) =
            DomNode.Node(tag, AttributeMap.ofList attributes, children |> AList.ofMod)

        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, children : list<DomNode<'msg>>) =
            DomNode.Node(tag, AttributeMap.ofList attributes, AList.ofList children)
 
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, children : plist<DomNode<'msg>>) =
            DomNode.Node(tag, AttributeMap.ofList attributes, AList.ofPList children)
            

        // Content and dynamic attributes
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, content : IMod<string>) =
            DomNode.Text(tag, None, attributes, content)
            
        static member inline Node(tag : string, attributes : AttributeMap<'msg>, content : string) =
            DomNode.Text(tag, None, attributes, Mod.constant content)

        // Content and static attributes
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, content : IMod<string>) =
            DomNode.Text(tag, None, AttributeMap.ofList attributes, content)
            
        static member inline Node(tag : string, attributes : list<string * AttributeValue<'msg>>, content : string) =
            DomNode.Text(tag, None, AttributeMap.ofList attributes, Mod.constant content)

        static member inline Void(tag : string, attributes : AttributeMap<'msg>) =
            DomNode.Void(tag, attributes)

        static member inline Void(tag : string, attributes : list<string * AttributeValue<'msg>>) =
            DomNode.Void(tag, AttributeMap.ofList attributes)

    type CreatorNamespace =
        // Children and dynamic attributes
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, children : alist<DomNode<'msg>>) =
            DomNode.Node(tag, ns, attributes, children)
            
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, children : IMod<list<DomNode<'msg>>>) =
            DomNode.Node(tag, ns, attributes, children |> Mod.map PList.ofList |> AList.ofMod)
            
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, children : IMod<plist<DomNode<'msg>>>) =
            DomNode.Node(tag, ns, attributes, children |> AList.ofMod)

        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, children : list<DomNode<'msg>>) =
            DomNode.Node(tag, ns, attributes, AList.ofList children)
 
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, children : plist<DomNode<'msg>>) =
            DomNode.Node(tag, ns, attributes, AList.ofPList children)
            
            
        // Children and static attributes
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, children : alist<DomNode<'msg>>) =
            DomNode.Node(tag, ns, AttributeMap.ofList attributes, children)
            
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, children : IMod<list<DomNode<'msg>>>) =
            DomNode.Node(tag, ns, AttributeMap.ofList attributes, children |> Mod.map PList.ofList |> AList.ofMod)
            
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, children : IMod<plist<DomNode<'msg>>>) =
            DomNode.Node(tag, ns, AttributeMap.ofList attributes, children |> AList.ofMod)

        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, children : list<DomNode<'msg>>) =
            DomNode.Node(tag, ns, AttributeMap.ofList attributes, AList.ofList children)
 
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, children : plist<DomNode<'msg>>) =
            DomNode.Node(tag, ns, AttributeMap.ofList attributes, AList.ofPList children)
            

        // Content and dynamic attributes
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, content : IMod<string>) =
            DomNode.Text(tag, Some ns, attributes, content)
            
        static member inline Node(tag : string, ns : string, attributes : AttributeMap<'msg>, content : string) =
            DomNode.Text(tag, Some ns, attributes, Mod.constant content)

        // Content and static attributes
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, content : IMod<string>) =
            DomNode.Text(tag, Some ns, AttributeMap.ofList attributes, content)
            
        static member inline Node(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>, content : string) =
            DomNode.Text(tag, Some ns, AttributeMap.ofList attributes, Mod.constant content)

        static member inline Void(tag : string, ns : string, attributes : AttributeMap<'msg>) =
            DomNode.Void(tag, ns, attributes)

        static member inline Void(tag : string, ns : string, attributes : list<string * AttributeValue<'msg>>) =
            DomNode.Void(tag, ns, AttributeMap.ofList attributes)


    let inline private node (dummy : ^a) (tagName : ^b) (attrs : ^c) (children : ^d) =
        ((^a or ^b or ^c or ^d) : (static member Node : ^b * ^c * ^d -> DomNode<'msg>) (tagName, attrs, children))
        
    let inline private leaf (dummy : ^a) (tagName : ^b) (attrs : ^c)=
        ((^a or ^b or ^c or ^d) : (static member Void : ^b * ^c -> DomNode<'msg>) (tagName, attrs))
        
    let inline private nodeNS (dummy : ^a) (tagName : ^b) (ns : ^c) (attrs : ^d) (children : ^e) =
        ((^a or ^b or ^c or ^d or ^e) : (static member Node : ^b * ^c * ^d * ^e -> DomNode<'msg>) (tagName, ns, attrs, children))
        
    let inline private leafNS (dummy : ^a) (tagName : ^b) (ns : ^c) (attrs : ^d)=
        ((^a or ^b or ^c or ^d) : (static member Node : ^b * ^c * ^d -> DomNode<'msg>) (tagName, ns, attrs))
        
    let inline elem tag atts children = node Unchecked.defaultof<Creator> tag atts children
    let inline voidElem tag atts = leaf Unchecked.defaultof<Creator> tag atts
    
    let inline elemNS tag ns atts children = nodeNS Unchecked.defaultof<CreatorNamespace> tag ns atts children
    let inline voidElemNS tag ns atts = leafNS Unchecked.defaultof<CreatorNamespace> tag ns atts



    let inline text content = elem "span" [] content
    

    // Elements - list of elements here: https://developer.mozilla.org/en-US/docs/Web/HTML/Element
    // Void elements
    let inline br x = voidElem "br" x
    let inline area x = voidElem "area" x
    let inline baseHtml x = voidElem "base" x
    let inline col x = voidElem "col" x
    let inline embed x = voidElem "embed" x
    let inline hr x = voidElem "hr" x
    let inline img x = voidElem "img" x
    let inline input x = voidElem "input" x
    let inline link x = voidElem "link" x
    let inline meta x = voidElem "meta" x
    let inline param x = voidElem "param" x
    let inline source x = voidElem "source" x
    let inline track x = voidElem "track" x
    let inline wbr x = voidElem "wbr" x

    // Metadata
    let inline head a c = elem "head" a c
    let inline title a c = elem "title" a c

    // Content sectioning
    let inline address a c = elem "address" a c
    let inline article a c = elem "article" a c
    let inline aside a c = elem "aside" a c
    let inline footer a c = elem "footer" a c
    let inline header a c = elem "header" a c
    let inline h1 a c = elem "h1" a c
    let inline h2 a c = elem "h2" a c
    let inline h3 a c = elem "h3" a c
    let inline h4 a c = elem "h4" a c
    let inline h5 a c = elem "h5" a c
    let inline h6 a c = elem "h6" a c
    let inline hgroup a c = elem "hgroup" a c
    let inline nav a c = elem "nav" a c

    // page content
    let inline body a c = elem "body" a c

    // Text content
    let inline dd a c = elem "dd" a c
    let inline div a c = elem "div" a c
    let inline dl a c = elem "dl" a c
    let inline dt a c = elem "dt" a c
    let inline figcaption a c = elem "figcaption" a c
    let inline figure a c = elem "figure" a c
    let inline li a c = elem "li" a c
    let inline main a c = elem "main" a c
    let inline ol a c = elem "ol" a c
    let inline p a c = elem "p" a c
    let inline pre a c = elem "pre" a c
    let inline section a c = elem "section" a c
    let inline ul a c = elem "ul" a c

    // Inline text semantics
    let inline a a c = elem "a" a c
    let inline abbr a c = elem "abbr" a c
    let inline b a c = elem "b" a c
    let inline bdi a c = elem "bdi" a c
    let inline bdo a c = elem "bdo" a c
    let inline cite a c = elem "cite" a c
    let inline code a c = elem "code" a c
    let inline data a c = elem "data" a c
    let inline dfn a c = elem "dfn" a c
    let inline em a c = elem "em" a c
    let inline i a c = elem "i" a c
    let inline kbd a c = elem "kbd" a c
    let inline mark a c = elem "mark" a c
    let inline q a c = elem "q" a c
    let inline rp a c = elem "rp" a c
    let inline rt a c = elem "rt" a c
    let inline rtc a c = elem "rtc" a c
    let inline ruby a c = elem "ruby" a c
    let inline s a c = elem "s" a c
    let inline samp a c = elem "samp" a c
    let inline small a c = elem "small" a c
    let inline span a c = elem "span" a c
    let inline strong a c = elem "strong" a c
    let inline sub a c = elem "sub" a c
    let inline sup a c = elem "sup" a c
    let inline time a c = elem "time" a c
    let inline u a c = elem "u" a c
    let inline var a c = elem "var" a c

    // Image and multimedia
    let inline audio a c = elem "audio" a c
    let inline map a c = elem "map" a c
    let inline video a c = elem "video" a c

    // Embedded content
    let inline objectHtml a c = elem "object" a c

    // Demarcasting edits
    let inline del a c = elem "del" a c
    let inline ins a c = elem "ins" a c

    // Table content
    let inline caption a c = elem "caption" a c
    let inline colgroup a c = elem "colgroup" a c
    let inline table a c = elem "table" a c
    let inline tbody a c = elem "tbody" a c
    let inline td a c = elem "td" a c
    let inline tfoot a c = elem "tfoot" a c
    let inline th a c = elem "th" a c
    let inline thead a c = elem "thead" a c
    let inline tr a c = elem "tr" a c

    // Forms
    let inline button a c = elem "button" a c
    let inline datalist a c = elem "datalist" a c
    let inline fieldset a c = elem "fieldset" a c
    let inline form a c = elem "form" a c
    let inline label a c = elem "label" a c
    let inline legend a c = elem "legend" a c
    let inline meter a c = elem "meter" a c
    let inline optgroup a c = elem "optgroup" a c
    let inline option a c = elem "option" a c
    let inline output a c = elem "output" a c
    let inline progress a c = elem "progress" a c
    let inline select a c = elem "select" a c
    let inline textarea a c = elem "textarea" a c

    // Interactive elements
    let inline details a c = elem "details" a c
    let inline dialog a c = elem "dialog" a c
    let inline menu a c = elem "menu" a c
    let inline menuitem a c = elem "menuitem" a c
    let inline summary a c = elem "summary" a c

    module Svg =
        [<Literal>]
        let svgNS = "http://www.w3.org/2000/svg"
        let inline attribute k v = k,v

        let inline svg a c = elemNS "svg" svgNS a c
        let inline defs a c = elemNS "defs" svgNS a c
        let inline linearGradient  a c = elemNS "linearGradient" svgNS a c
        let inline radialGradient  a c = elemNS "radialGradient" svgNS a c
        let inline text a c = elemNS "text" svgNS a [DomNode.SvgText(Mod.constant c)]
        let inline filter a c = elemNS "filter" svgNS a c

        let inline feBlend x = voidElemNS "feBlend" svgNS x
        let inline feColorMatrix x = voidElemNS "feColorMatrix" svgNS x
        let inline feComponentTransfer x = voidElemNS "feComponentTransfer" svgNS x
        let inline feComposite x = voidElemNS "feComposite" svgNS x
        let inline feConvolveMatrix x = voidElemNS "feConvolveMatrix" svgNS x
        let inline feDiffuseLighting x = voidElemNS "feDiffuseLighting" svgNS x
        let inline feDisplacementMap x = voidElemNS "feDisplacementMap" svgNS x
        let inline feDistantLight x = voidElemNS "feDistantLight" svgNS x
        let inline feFlood x = voidElemNS "feFlood" svgNS x
        let inline feFuncA x = voidElemNS "feFuncA" svgNS x
        let inline feFuncB x = voidElemNS "feFuncB" svgNS x
        let inline feFuncG x = voidElemNS "feFuncG" svgNS x
        let inline feFuncR x = voidElemNS "feFuncR" svgNS x
        let inline feGaussianBlur x = voidElemNS "feGaussianBlur" svgNS x
        let inline feImage x = voidElemNS "feImage" svgNS x
        let inline image x = voidElemNS "image" svgNS x
        let inline feMerge x = voidElemNS "feMerge" svgNS x
        let inline feMergeNode x = voidElemNS "feMergeNode" svgNS x
        let inline feMorphology x = voidElemNS "feMorphology" svgNS x
        let inline feOffset x = voidElemNS "feOffset" svgNS x
        let inline fePointLight x = voidElemNS "fePointLight" svgNS x
        let inline feSpecularLighting x = voidElemNS "feSpecularLighting" svgNS x
        let inline feSpotLight x = voidElemNS "feSpotLight" svgNS x
        let inline feTile x = voidElemNS "feTile" svgNS x
        let inline feTurbulence x = voidElemNS "feTurbulence" svgNS x

        let inline stop x = voidElemNS "stop" svgNS x
        let inline circle x = voidElemNS "circle" svgNS x 
        let inline ellipse x = voidElemNS "ellipse" svgNS x 
        let inline rect x = voidElemNS "rect" svgNS x 
        let inline line x = voidElemNS "line" svgNS x
        let inline path x = voidElemNS "path" svgNS x
        let inline polygon x = voidElemNS "polygon" svgNS x
        let inline polyline x = voidElemNS "polyline" svgNS x
        let inline tspan x = voidElemNS "tspan" svgNS x

        let inline width x = attribute "width" x
        let inline height x = attribute "height" x
        let inline viewBox x = attribute "viewBox" x
        let inline cx x = attribute "cx" x
        let inline cy x = attribute "cy" x
        let inline r x = attribute "r" x
        let inline stroke x = attribute "stroke" x
        let inline strokeWidth x = attribute "stroke-width" x
        let inline strokeLinecap x = attribute "stroke-linecap" x
        let inline strokeDasharray x = attribute "stroke-dasharray" x
        let inline fill x = attribute "fill" x

module private GenericTest =
    open Generic

    let private test() = 
        let a : DomNode<unit> = div [] [text "asdasdas"]
        let a : DomNode<unit> = div AttributeMap.empty [text "asdasdas"]
        let a : DomNode<unit> = div [] AList.empty
        let a : DomNode<unit> = div AttributeMap.empty AList.empty
        let a : DomNode<unit> = div [] (Mod.constant "hi")
        let a : DomNode<unit> = div AttributeMap.empty (Mod.constant "hi")
        let a : DomNode<unit> = div AttributeMap.empty "hi"
        let a : DomNode<unit> = div [] "hi"
        let a : DomNode<unit> = div [] [ div [] "hi"; div [] "hugo"; br []; br AttributeMap.empty ]


        let a : DomNode<unit> =
            div [] [
                text (Mod.init "asdasda")
                text "asdasda"

                div [] (
                    alist {
                        yield text "hi there"
                        yield br []
                    }
                )

                button [] (Mod.init "test")
                button [] "click me"
            ]

        ()