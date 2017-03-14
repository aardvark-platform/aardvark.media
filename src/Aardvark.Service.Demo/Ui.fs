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

type ReferenceKind =
    | Script 
    | Stylesheet

type Reference = { kind : ReferenceKind; name : string; url : string }


type ChannelMessage = { targetId : string; channel : string; data : string }
[<AbstractClass>]
type Channel(name : string) =
    inherit AdaptiveObject()
    abstract member Compute : AdaptiveToken -> string
    abstract member Dispose : unit -> unit

    member x.Name = name

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    member x.GetMessage(token : AdaptiveToken, targetId : string) =
        x.EvaluateIfNeeded token None (fun token ->
            Some { targetId = targetId; channel = name; data = x.Compute(token) }
        )

type ModChannel<'a>(name : string, m : IMod<'a>) =
    inherit Channel(name)

    override x.Dispose() =
        ()

    override x.Compute(token) =
        let v = m.GetValue token
        Pickler.json.PickleToString v


[<RequireQualifiedAccess>]
type private AListOp<'a> =
    | Set of index : int * value : 'a
    | Remove of index : int

type AListChannel<'a>(name : string, m : alist<'a>) =
    inherit Channel(name)

    let reader = m.GetReader()
    
    override x.Dispose() =
        reader.Dispose()

    override x.Compute(token) =
        let mutable state = reader.State.Content
        let ops = reader.GetOperations token
        
        ops 
            |> PDeltaList.toList
            |> List.map (fun (i, op) ->
                // TODO: maybe improve
                let (l,s,r) = MapExt.split i state
                let index = l.Count

                match op with
                    | Set v -> 
                        state <- MapExt.add i v state
                        AListOp.Set(index, v)
                    | Remove -> 
                        state <- MapExt.remove i state
                        AListOp.Remove(index)
            )
            |> Pickler.json.PickleToString

 
[<RequireQualifiedAccess>]
type private ASetOp<'a> =
    | Add of value : 'a
    | Remove of value : 'a

type ASetChannel<'a>(name : string, m : aset<'a>) =
    inherit Channel(name)

    let reader = m.GetReader()
        
    override x.Dispose() =
        reader.Dispose()

    override x.Compute(token) =
        let ops = reader.GetOperations token

        ops |> HDeltaSet.toList
            |> List.map (fun op ->
                match op with
                    | Add(_,v) -> ASetOp.Add(v)
                    | Rem(_,v) -> ASetOp.Remove(v)
            )
            |> Pickler.json.PickleToString

[<RequireQualifiedAccess>]
type private AMapOp<'a, 'b> =
    | Set of key : 'a * value : 'b
    | Remove of key : 'a

type AMapChannel<'a, 'b>(name : string, m : amap<'a, 'b>) =
    inherit Channel(name)

    let reader = m.GetReader()

    override x.Dispose() =
        reader.Dispose()

    override x.Compute(token) =
        let ops = reader.GetOperations token
 
        ops |> HMap.toList
            |> List.map (fun (key, op) ->
                match op with
                    | Set v -> AMapOp.Set(key, v)
                    | Remove -> AMapOp.Remove(key)
            )
            |> Pickler.json.PickleToString
 

type AttributeValue<'msg> =
    | Event of list<string> * (list<string> -> 'msg)
    | Value of string
    | ClientEvent of (string -> string)

type Ui<'msg>(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : UiContent<'msg>) =
    let mutable initialAttributes : Map<string, AttributeValue<'msg>> = Map.empty
    let mutable required : list<Reference> = []
    let mutable boot : Option<string -> string> = None
    let mutable shutdown : Option<string -> string> = None
    let mutable callbacks : Map<string, (list<string> -> 'msg)> = Map.empty
    let mutable channels : list<Channel> = []

    member x.Tag = tag
    member x.Attributes = attributes
    member x.Content = content

    member x.Required
        with get() = required
        and set r = required <- r

    member x.Boot
        with get() = boot
        and set code = boot <- code

    member x.Shutdown
        with get() = shutdown
        and set code = shutdown <- code

    member x.Callbacks
        with get() = callbacks
        and set cbs = callbacks <- cbs

    member x.InitialAttributes
        with get() = initialAttributes
        and set v = initialAttributes <- v

    member x.Channels
        with get() = channels
        and set v = channels <- v

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : alist<Ui<'msg>>) =
        Ui(tag, attributes, Children content)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : IMod<string>) =
        Ui(tag, attributes, Text content)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, sg : IRenderControl -> IRenderTask) =
        Ui(tag, attributes, Scene sg)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>) =
        Ui(tag, attributes, Empty)

and UiContent<'msg> =
    | Children of alist<Ui<'msg>>
    | Text of IMod<string>
    | Scene of (IRenderControl -> IRenderTask)
    | Empty

type Attribute<'msg> = string * AttributeValue<'msg>

[<AutoOpen>]
module Tags =
    let inline elem (tagName : string) (attrs : amap<string, AttributeValue<'msg>>) (children : alist<Ui<'msg>>) = Ui(tagName, attrs, children)
    let inline voidElem (tagName : string) (attrs : amap<string, AttributeValue<'msg>>) = Ui(tagName, attrs, Empty)
   
    let inline text (content : IMod<string>) = Ui("span", AMap.empty, content)

    let sg (att : amap<string, AttributeValue<'msg>>) (sg : IRenderControl -> ISg) =
        let create (ctrl : IRenderControl) =
            let runtime = ctrl.Runtime
            runtime.CompileRender(ctrl.FramebufferSignature, sg ctrl)

        Ui("div", att, create, InitialAttributes = Map.ofList ["class", Value "aardvark"])

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
module PersistentTags =
    let inline elem (tagName : string) (attrs : list<Attribute<'msg>>) (children : list<Ui<'msg>>) = Ui(tagName, AMap.ofList attrs, AList.ofList children)
    let inline voidElem (tagName : string) (attrs : list<Attribute<'msg>>) = Ui(tagName, AMap.ofList attrs, Empty)
   
    let inline text' (content : string) = Ui("span", AMap.empty, Mod.constant content)

    let sg' (att : list<Attribute<'msg>>) (sg : IRenderControl -> ISg) =
        let create (ctrl : IRenderControl) =
            let runtime = ctrl.Runtime
            runtime.CompileRender(ctrl.FramebufferSignature, sg ctrl)

        Ui("div", AMap.ofHMap (att |> HMap.ofList |> HMap.add "class" (Value "aardvark")), create)
        


    // Elements - list of elements here: https://developer.mozilla.org/en-US/docs/Web/HTML/Element
    // Void elements
    let inline br' x = voidElem "br" x
    let inline area' x = voidElem "area" x
    let inline baseHtml' x = voidElem "base" x
    let inline col' x = voidElem "col" x
    let inline embed' x = voidElem "embed" x
    let inline hr' x = voidElem "hr" x
    let inline img' x = voidElem "img" x
    let inline input' x = voidElem "input" x
    let inline link' x = voidElem "link" x
    let inline meta' x = voidElem "meta" x
    let inline param' x = voidElem "param" x
    let inline source' x = voidElem "source" x
    let inline track' x = voidElem "track" x
    let inline wbr' x = voidElem "wbr" x

    // Metadata
    let inline head' x = elem "head" x
    let inline style' x = elem "style" x
    let inline title' x = elem "title" x

    // Content sectioning
    let inline address' x = elem "address" x
    let inline article' x = elem "article" x
    let inline aside' x = elem "aside" x
    let inline footer' x = elem "footer" x
    let inline header' x = elem "header" x
    let inline h1' x = elem "h1" x
    let inline h2' x = elem "h2" x
    let inline h3' x = elem "h3" x
    let inline h4' x = elem "h4" x
    let inline h5' x = elem "h5" x
    let inline h6' x = elem "h6" x
    let inline hgroup' x = elem "hgroup" x
    let inline nav' x = elem "nav" x

    // Text content
    let inline dd' x = elem "dd" x
    let inline div' x = elem "div" x
    let inline dl' x = elem "dl" x
    let inline dt' x = elem "dt" x
    let inline figcaption' x = elem "figcaption" x
    let inline figure' x = elem "figure" x
    let inline li' x = elem "li" x
    let inline main' x = elem "main" x
    let inline ol' x = elem "ol" x
    let inline p' x = elem "p" x
    let inline pre' x = elem "pre" x
    let inline section' x = elem "section" x
    let inline ul' x = elem "ul" x

    // Inline text semantics
    let inline a' x = elem "a" x
    let inline abbr' x = elem "abbr" x
    let inline b' x = elem "b" x
    let inline bdi' x = elem "bdi" x
    let inline bdo' x = elem "bdo" x
    let inline cite' x = elem "cite" x
    let inline code' x = elem "code" x
    let inline data' x = elem "data" x
    let inline dfn' x = elem "dfn" x
    let inline em' x = elem "em" x
    let inline i' x = elem "i" x
    let inline kbd' x = elem "kbd" x
    let inline mark' x = elem "mark" x
    let inline q' x = elem "q" x
    let inline rp' x = elem "rp" x
    let inline rt' x = elem "rt" x
    let inline rtc' x = elem "rtc" x
    let inline ruby' x = elem "ruby" x
    let inline s' x = elem "s" x
    let inline samp' x = elem "samp" x
    let inline small' x = elem "small" x
    let inline span' x = elem "span" x
    let inline strong' x = elem "strong" x
    let inline sub' x = elem "sub" x
    let inline sup' x = elem "sup" x
    let inline time' x = elem "time" x
    let inline u' x = elem "u" x
    let inline var' x = elem "var" x

    // Image and multimedia
    let inline audio' x = elem "audio" x
    let inline map' x = elem "map" x
    let inline video' x = elem "video" x

    // Embedded content
    let inline objectHtml' x = elem "object" x

    // Demarcasting edits
    let inline del' x = elem "del" x
    let inline ins' x = elem "ins" x

    // Table content
    let inline caption' x = elem "caption" x
    let inline colgroup' x = elem "colgroup" x
    let inline table' x = elem "table" x
    let inline tbody' x = elem "tbody" x
    let inline td' x = elem "td" x
    let inline tfoot' x = elem "tfoot" x
    let inline th' x = elem "th" x
    let inline thead' x = elem "thead" x
    let inline tr' x = elem "tr" x

    // Forms
    let inline button' x = elem "button" x
    let inline datalist' x = elem "datalist" x
    let inline fieldset' x = elem "fieldset" x
    let inline form' x = elem "form" x
    let inline label' x = elem "label" x
    let inline legend' x = elem "legend" x
    let inline meter' x = elem "meter" x
    let inline optgroup' x = elem "optgroup" x
    let inline option' x = elem "option" x
    let inline output' x = elem "output" x
    let inline progress' x = elem "progress" x
    let inline select' x = elem "select" x
    let inline textarea' x = elem "textarea" x

    // Interactive elements
    let inline details' x = elem "details" x
    let inline dialog' x = elem "dialog" x
    let inline menu' x = elem "menu" x
    let inline menuitem' x = elem "menuitem" x
    let inline summary' x = elem "summary" x


[<AutoOpen>]
module Attributes =
    let inline attribute (key : string) (value : string) : Attribute<'msg> = key,AttributeValue.Value value

    /// Class attribute helper
    let inline Class value = attribute "class" value

    /// Helper to build space separated class
    let inline classList (list: (string*bool) seq) =
        list
            |> Seq.filter (fun (c,cond) -> cond)
            |> Seq.map (fun (c, cond) -> c)
            |> String.concat " "
            |> Class

    let inline boolAttribute name (value: bool) =
        attribute name (string value)

[<AutoOpen>]
module Events =
    let inline onEvent (eventType : string) (args : list<string>) (cb : list<string> -> 'msg) : Attribute<'msg> = eventType, AttributeValue.Event(args, cb)


    let onMouseClick (cb : V2i -> 'msg) = onEvent "onclick" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
    let onContextMenu (cb : unit -> 'msg) = onEvent "oncontextmenu" [] (ignore >> cb)
    let onDblClick (cb : V2i -> 'msg) = onEvent "ondblclick" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
    let onMouseDown (cb : V2i -> 'msg) = onEvent "onmousedown" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)
    let onMouseUp (cb : V2i -> 'msg) = onEvent "onmouseup" ["{ X: event.clientX, Y: event.clientY  }"] (List.head >> Pickler.json.UnPickleOfString >> cb)


    let onClick (cb : unit -> 'msg) = onEvent "onclick" [] (ignore >> cb)
