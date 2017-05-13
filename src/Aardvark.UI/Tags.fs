namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Incremental

module Incremental =

    let renderControl (cam : IMod<Camera>) (attributes : AttributeMap<'msg>) (sg : ISg<'msg>) =
        DomNode.RenderControl(attributes, cam, sg)

    let inline elem (tagName : string) (attrs : AttributeMap<'msg>) (children : alist<DomNode<'msg>>) =
        DomNode.Node(tagName, attrs, children)

    let inline voidElem (tagName : string) (attrs : AttributeMap<'msg>) = 
        DomNode.Void(tagName, attrs)
   
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
    let inline pre x = elem "pre" x
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
    let inline textarea x = elem "textarea" x

    // Interactive elements
    let inline details x = elem "details" x
    let inline dialog x = elem "dialog" x
    let inline menu x = elem "menu" x
    let inline menuitem x = elem "menuitem" x
    let inline summary x = elem "summary" x


[<AutoOpen>]
module Static =

    let renderControl (cam : IMod<Camera>) (attributes : list<string * AttributeValue<'msg>>) (sg : ISg<'msg>) =
        DomNode.RenderControl(AttributeMap.ofList attributes, cam, sg)

    let inline elem (tagName : string) (attrs : list<string * AttributeValue<'msg>>) (children : list<DomNode<'msg>>) =
        DomNode.Node(tagName, AttributeMap.ofList attrs, AList.ofList children)

    let inline voidElem (tagName : string) (attrs : list<string * AttributeValue<'msg>>) = 
        DomNode.Void(tagName, AttributeMap.ofList attrs)
   
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
    let inline pre x = elem "pre" x
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
    let inline textarea x = elem "textarea" x

    // Interactive elements
    let inline details x = elem "details" x
    let inline dialog x = elem "dialog" x
    let inline menu x = elem "menu" x
    let inline menuitem x = elem "menuitem" x
    let inline summary x = elem "summary" x

[<AutoOpen>]
module HigherOrderTags =
    
    let require (refs : list<Reference>) (node : DomNode<'msg>) =
        node.WithRequired(refs @ node.Required)
    
    let onBoot (code : string) (node : DomNode<'msg>) =
        let boot id = code.Replace("__ID__", id)
        match node.Boot with
            | None -> node.WithBoot (Some boot)
            | Some o -> node.WithBoot (Some (fun id -> boot id + "; " + o id))
