namespace Aardvark.UI.Golden

open Aardvark.Base
open Aardvark.UI
open System

[<RequireQualifiedAccess>]
type Size =
    | Weight     of int
    | Percentage of int

    override x.ToString() =
        match x with
        | Weight v -> $"{v}fr"
        | Percentage v -> $"{v}%%"

type Header =
    | Top    = 0
    | Left   = 1
    | Right  = 2
    | Bottom = 3

[<Flags>]
type Buttons =
    | None     = 0
    | Close    = 1
    | Popout   = 2
    | Maximize = 4
    | All      = 7

type Element =
    { Id       : string
      Title    : string
      Closable : bool
      Header   : Header option
      Buttons  : Buttons
      Size     : Size }

type Stack =
    { Header  : Header
      Buttons : Buttons
      Size    : Size
      Content : Element list }

type RowOrColumn =
    { IsRow   : bool
      Size    : Size
      Content : Layout list }

and [<RequireQualifiedAccess>] Layout =
    | Element     of Element
    | Stack       of Stack
    | RowOrColumn of RowOrColumn

type Labels =
    { Minimize    : string
      Maximize    : string
      PopOut      : string
      PopIn       : string
      Close       : string
      TabDropdown : string }

module Labels =

    let Default =
        { Minimize    = "Minimize"
          Maximize    = "Maximize"
          PopOut      = "Open in new window"
          PopIn       = "Dock"
          Close       = "Close"
          TabDropdown = "Additional tabs" }

type Config =
    { PopOutWholeStack : bool
      PopInOnClose     : bool
      Labels           : Labels }

module Config =

    let Default =
        { PopOutWholeStack = false
          PopInOnClose     = true
          Labels           = Labels.Default }

module Layout =

    [<Sealed; AbstractClass>]
    type Converter =
        static member inline ToLayout(root : Element) = Layout.Element root
        static member inline ToLayout(root : Stack) = Layout.Stack root
        static member inline ToLayout(root : RowOrColumn) = Layout.RowOrColumn root

    let inline private ofRootAux (_ : ^Z) (item : ^T) =
        ((^Z or ^T) : (static member ToLayout : ^T -> Layout) (item))

    let inline ofRoot (item : ^T) =
        ofRootAux Unchecked.defaultof<Converter> item

[<AutoOpen>]
module Builders =

    type IdMustBeSpecified = IdMustBeSpecified

    type ElementBuilder() =
        member inline x.Yield(()) = IdMustBeSpecified

        [<CustomOperation("id")>]
        member inline x.Id(_ : IdMustBeSpecified, id : string) =
            { Id       = id
              Title    = "Untitled"
              Closable = true
              Header   = Some Header.Top
              Buttons  = Buttons.All
              Size     = Size.Weight 1 }

        [<CustomOperation("title")>]
        member inline x.Title(e : Element, title : string) =
            { e with Title = title }

        [<CustomOperation("closable")>]
        member inline x.Closable(e : Element, closable : bool) =
            { e with Closable = closable }

        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header option) =
            { e with Header = header }

        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header) =
            { e with Header = Some header }

        [<CustomOperation("buttons")>]
        member inline x.Buttons(e : Element, buttons : Buttons) =
            { e with Buttons = buttons }

        [<CustomOperation("size")>]
        member inline x.Size(e : Element, size : Size) =
            { e with Size = size }

        [<CustomOperation("size")>]
        member inline x.Size(e : Element, sizeInPercent : int) =
            { e with Size = Size.Percentage sizeInPercent }

        [<CustomOperation("weight")>]
        member inline x.Weight(e : Element, weight : int) =
            { e with Size = Size.Weight weight }

    type StackBuilder() =
        static let empty =
            { Header  = Header.Top
              Buttons = Buttons.All
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(e : Element) = { empty with Content = [e] }

        member x.Delay(f : unit -> Stack) = f()
        member x.Combine(a : Stack, b : Stack) = { b with Content = a.Content @ b.Content }
        member x.For(s: Stack, f: unit -> Stack) = x.Combine(s, f())

        [<CustomOperation("header")>]
        member inline x.Header(s : Stack, header : Header) =
            { s with Header = header }

        [<CustomOperation("buttons")>]
        member inline x.Buttons(s : Stack, buttons : Buttons) =
            { s with Buttons = buttons }

        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, size : Size) =
            { s with Size = size }

        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, sizeInPercent : int) =
            { s with Size = Size.Percentage sizeInPercent }

        [<CustomOperation("weight")>]
        member inline x.Weight(s : Stack, weight : int) =
            { s with Size = Size.Weight weight }

    type RowOrColumnBuilder(isRow : bool) =
        let empty =
            { IsRow   = isRow
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(l : Layout)          = { empty with Content = [l] }
        member x.Yield(e : Element)         = x.Yield(Layout.Element e)
        member x.Yield(s : Stack)           = x.Yield(Layout.Stack s)
        member x.Yield(rc : RowOrColumn)    = x.Yield(Layout.RowOrColumn rc)

        member x.Delay(f : unit -> RowOrColumn) = f()
        member x.Combine(a : RowOrColumn, b : RowOrColumn) = { b with Content = a.Content @ b.Content }
        member x.For(rc: RowOrColumn, f: unit -> RowOrColumn) = x.Combine(rc, f())

        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, size : Size) =
            { rc with Size = size }

        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, sizeInPercent : int) =
            { rc with Size = Size.Percentage sizeInPercent }

        [<CustomOperation("weight")>]
        member inline x.Weight(rc : RowOrColumn, weight : int) =
            { rc with Size = Size.Weight weight }

    let element = ElementBuilder()
    let stack = StackBuilder()
    let row = RowOrColumnBuilder true
    let column = RowOrColumnBuilder false

module GoldenLayout =

    module Json =
        open Newtonsoft.Json
        open Newtonsoft.Json.Linq

        module private JObject =

            let private ofHeader (buttons : Buttons) (header : Header option) =
                let o = JObject()

                let show =
                    match header with
                    | Some Header.Top    -> JToken.op_Implicit "top"
                    | Some Header.Left   -> JToken.op_Implicit "left"
                    | Some Header.Right  -> JToken.op_Implicit "right"
                    | Some Header.Bottom -> JToken.op_Implicit "bottom"
                    | _                  -> JToken.op_Implicit false

                let button (property : string) (flag : Buttons) =
                    if not <| buttons.HasFlag flag then
                        o.[property] <- JToken.op_Implicit false

                o.["show"] <- show
                button "close" Buttons.Close
                button "popout" Buttons.Popout
                button "maximise" Buttons.Maximize
                o

            let rec ofLayout (layout : Layout) : JObject =
                let o = JObject()

                match layout with
                | Layout.Element e ->
                    o.["type"] <- JToken.op_Implicit "component"
                    o.["title"] <- JToken.op_Implicit e.Title
                    o.["componentType"] <- JToken.op_Implicit e.Id
                    o.["isClosable"] <- JToken.op_Implicit e.Closable
                    o.["header"] <- ofHeader e.Buttons e.Header
                    o.["size"] <- JToken.op_Implicit (string e.Size)

                | Layout.Stack s ->
                    let content = s.Content |> List.map (Layout.Element >> ofLayout >> box)
                    o.["type"] <- JToken.op_Implicit "stack"
                    o.["header"] <- ofHeader s.Buttons (Some s.Header)
                    o.["size"] <- JToken.op_Implicit (string s.Size)
                    o.["content"] <- JArray(List.toArray content)

                | Layout.RowOrColumn rc ->
                    let content = rc.Content |> List.map (ofLayout >> box)
                    o.["type"] <- JToken.op_Implicit (if rc.IsRow then "row" else "column")
                    o.["size"] <- JToken.op_Implicit (string rc.Size)
                    o.["content"] <- JArray(List.toArray content)

                o

            let ofConfigLabels (config : Config) =
                let o = JObject()
                o.["close"] <- JToken.op_Implicit config.Labels.Close
                o.["maximise"] <- JToken.op_Implicit config.Labels.Maximize
                o.["minimise"] <- JToken.op_Implicit config.Labels.Minimize
                o.["popout"] <- JToken.op_Implicit config.Labels.PopOut
                o.["popin"] <- JToken.op_Implicit config.Labels.PopIn
                o.["tabDropdown"] <- JToken.op_Implicit config.Labels.TabDropdown
                o

            let ofConfigSettings (config : Config) =
                let o = JObject()
                o.["popoutWholeStack"] <- JToken.op_Implicit config.PopOutWholeStack
                o.["popInOnClose"] <- JToken.op_Implicit config.PopInOnClose
                o

        let ofLayoutConfig (config : Config) (layout : Layout) =
            let o = JObject()
            o.["root"] <- JObject.ofLayout layout
            o.["settings"] <- JObject.ofConfigSettings config
            o.["header"] <- JObject.ofConfigLabels config
            o.ToString Formatting.None

    let inline layout (attributes : Attribute<'msg> list) (config : Config) (root : ^LayoutRoot)
                      (getElement : string -> DomNode<'msg>) =
        let attributes =
            attributes @ [
                clazz "gl-aard-container"
            ]

        let boot =
            let layout = Layout.ofRoot root
            let configJson = Json.ofLayoutConfig config layout
            sprintf "aardvark.golden.createLayout($('#__ID__')[0], %s)" configJson

        let shutdown =
            sprintf "aardvark.golden.destroyLayout($('#__ID__')[0])"

        let dependencies =
            [ { name = "golden-layout";      url = "resources/golden-layout/bundle/umd/golden-layout.min.js";        kind = Script }
              { name = "golden-layout";      url = "resources/golden-layout/css/goldenlayout-base.css";              kind = Stylesheet }
              { name = "golden-layout-dark"; url = "resources/golden-layout/css/themes/goldenlayout-dark-theme.css"; kind = Stylesheet }
              { name = "golden-layout-aard"; url = "resources/golden-layout/golden-layout-aard.js";                  kind = Script }
              { name = "golden-layout-aard"; url = "resources/golden-layout/golden-layout-aard.css";                 kind = Stylesheet } ]

        page (fun request ->
            match request.queryParams |> Map.tryFind "page" with
            | Some name -> getElement name
            | _ ->
                require dependencies (
                    (onBoot boot >> onShutdown shutdown) (
                        div attributes []
                    )
                )
        )