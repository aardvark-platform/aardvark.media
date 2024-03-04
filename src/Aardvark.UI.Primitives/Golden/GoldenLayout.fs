namespace Aardvark.UI.Primitives.Golden

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open System

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Theme =
    let private getTheme (name : string) =
        Theme <| sprintf "resources/golden-layout/css/themes/goldenlayout-%s-theme.css" name

    let BorderlessDark = getTheme "borderless-dark"
    let Dark           = getTheme "dark"
    let Light          = getTheme "light"
    let Soda           = getTheme "soda"
    let Translucent    = getTheme "translucent"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LayoutConfig =

    let Default =
        { Theme              = Theme.BorderlessDark
          PopInOnClose       = true
          PopOutWholeStack   = true
          DragBetweenWindows = true
          DragToNewWindow    = true
          HeaderButtons      = Buttons.All
          SetPopoutTitle     = true
          MinItemWidth       = 20
          MinItemHeight      = 20
          DragProxyWidth     = 300
          DragProxyHeight    = 200
          LabelMinimize      = "Minimize"
          LabelMaximize      = "Maximize"
          LabelPopOut        = "Open in new window"
          LabelPopIn         = "Dock"
          LabelClose         = "Close"
          LabelTabDropdown   = "Additional tabs" }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Layout =

    [<Sealed; AbstractClass>]
    type Converter =
        static member inline ToLayout(layout : Layout) = layout
        static member inline ToLayout(root : Element) = Layout.Element root
        static member inline ToLayout(root : Stack) = Layout.Stack root
        static member inline ToLayout(root : RowOrColumn) = Layout.RowOrColumn root

    let inline private ofRootAux (_ : ^Z) (item : ^T) =
        ((^Z or ^T) : (static member ToLayout : ^T -> Layout) (item))

    let inline ofRoot (item : ^T) =
        ofRootAux Unchecked.defaultof<Converter> item

[<AutoOpen>]
module Builders =

    module ElementError =
        type IdMustBeSpecified = IdMustBeSpecified

    type ElementBuilder() =
        member inline x.Yield(()) = ElementError.IdMustBeSpecified

        /// Unique name to identify the element in the view function.
        [<CustomOperation("id")>]
        member inline x.Id(_ : ElementError.IdMustBeSpecified, id : string) =
            { Id        = id
              Title     = "Untitled"
              Closable  = true
              Header    = Some Header.Top
              Buttons   = None
              MinSize   = None
              Size      = Size.Weight 1
              KeepAlive = true }

        /// Title shown in the header.
        [<CustomOperation("title")>]
        member inline x.Title(e : Element, title : string) =
            { e with Title = title }

        /// Determines if the element can be closed via buttons in the header and tab.
        [<CustomOperation("closable")>]
        member inline x.Closable(e : Element, closable : bool) =
            { e with Closable = closable }

        /// Determines the position of the header or if one is shown at all.
        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header option) =
            { e with Header = header }

        /// Determines the position of the header.
        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header) =
            { e with Header = Some header }

        /// Buttons to display in the header.
        [<CustomOperation("buttons")>]
        member inline x.Buttons(e : Element, buttons : Buttons) =
            { e with Buttons = Some buttons }

        /// Minimum size (in pixels) of the element in any dimension.
        [<CustomOperation("minSize")>]
        member inline x.MinSize(e : Element, sizeInPixels : int) =
            { e with MinSize = Some sizeInPixels }

        /// Size of the element in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(e : Element, size : Size) =
            { e with Size = size }

        /// Size of the element in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(e : Element, sizeInPercent : int) =
            { e with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(e : Element, weight : int) =
            { e with Size = Size.Weight weight }

        /// If true the DOM element is hidden rather than destroyed if it is removed from the layout.
        /// This allows for faster restoring of the element but may come with a performance penalty. Default is true.
        [<CustomOperation("keepAlive")>]
        member inline x.KeepAlive(e : Element, keepAlive : bool) =
            { e with KeepAlive = keepAlive }

    type StackBuilder() =
        static let empty =
            { Header  = Header.Top
              Buttons = None
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(e : Element) = { empty with Content = [e] }
        member x.Yield(e : Element seq) = { empty with Content = List.ofSeq e }

        member x.Delay(f : unit -> Stack) = f()
        member x.Combine(a : Stack, b : Stack) = { a with Content = a.Content @ b.Content }
        member x.For(s: Stack, f: unit -> Stack) = x.Combine(s, f())

        /// Determines the position of the header.
        [<CustomOperation("header")>]
        member inline x.Header(s : Stack, header : Header) =
            { s with Header = header }

        /// Buttons to display in the header.
        [<CustomOperation("buttons")>]
        member inline x.Buttons(s : Stack, buttons : Buttons) =
            { s with Buttons = Some buttons }

        /// Size of the stack in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, size : Size) =
            { s with Size = size }

        /// Size of the stack in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, sizeInPercent : int) =
            { s with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(s : Stack, weight : int) =
            { s with Size = Size.Weight weight }

        /// Content of the stack.
        [<CustomOperation("content")>]
        member inline x.Content(s : Stack, c : Element seq) =
            { s with Content = List.ofSeq c }

    type RowOrColumnBuilder(isRow : bool) =
        let empty =
            { IsRow   = isRow
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(l : Layout)           = { empty with Content = [l] }
        member x.Yield(e : Element)          = x.Yield(Layout.Element e)
        member x.Yield(s : Stack)            = x.Yield(Layout.Stack s)
        member x.Yield(rc : RowOrColumn)     = x.Yield(Layout.RowOrColumn rc)
        member x.Yield(l : Layout seq)       = { empty with Content = List.ofSeq l }
        member x.Yield(e : Element seq)      = x.Yield(e |> Seq.map Layout.Element)
        member x.Yield(s : Stack seq)        = x.Yield(s |> Seq.map Layout.Stack)
        member x.Yield(rc : RowOrColumn seq) = x.Yield(rc |> Seq.map Layout.RowOrColumn)

        member x.Delay(f : unit -> RowOrColumn) = f()
        member x.Combine(a : RowOrColumn, b : RowOrColumn) = { a with Content = a.Content @ b.Content }
        member x.For(rc: RowOrColumn, f: unit -> RowOrColumn) = x.Combine(rc, f())

        /// Size of the container in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, size : Size) =
            { rc with Size = size }

        /// Size of the container in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, sizeInPercent : int) =
            { rc with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(rc : RowOrColumn, weight : int) =
            { rc with Size = Size.Weight weight }

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Layout seq) =
            { rc with Content = List.ofSeq c }

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Element seq) =
            x.Content(rc, c |> Seq.map Layout.Element)

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Stack seq) =
            x.Content(rc, c |> Seq.map Layout.Stack)

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : RowOrColumn seq) =
            x.Content(rc, c |> Seq.map Layout.RowOrColumn)

    let element = ElementBuilder()
    let stack = StackBuilder()
    let row = RowOrColumnBuilder true
    let column = RowOrColumnBuilder false


[<AutoOpen>]
module GoldenLayoutApp =

    type private TaggedChannelReader<'T>(data : aval<('T * int) option>, pickle : 'T -> string) =
        inherit ChannelReader()

        let mutable lastVersion = -1

        override x.Release() = ()
        override x.ComputeMessages t =
            match data.GetValue t with
            | Some (value, version) when version <> lastVersion ->
                lastVersion <- version
                [ pickle value ]

            | _ ->
                []

    type private TaggedChannel<'T>(data : aval<('T * int) option>, pickle : 'T -> string) =
        inherit Channel()
        new (data : aval<('T * int) option>) = TaggedChannel(data, Pickler.jsonToString)
        override x.GetReader() = new TaggedChannelReader<_>(data, pickle) :> ChannelReader

    let onLayoutChanged (callback : unit -> 'msg) =
        onEvent "onLayoutChanged" [] (ignore >> callback)

    module GoldenLayout =
        open Suave
        open Suave.Filters
        open Suave.Operators
        open Suave.Successful
        open System.IO

        module Json =
            open Newtonsoft.Json
            open Newtonsoft.Json.Linq

            module private JObject =

                let private ofHeader (config : LayoutConfig) (buttons : Buttons option) (header : Header option) =
                    let o = JObject()

                    let show =
                        match header with
                        | Some Header.Top    -> JToken.op_Implicit "top"
                        | Some Header.Left   -> JToken.op_Implicit "left"
                        | Some Header.Right  -> JToken.op_Implicit "right"
                        | Some Header.Bottom -> JToken.op_Implicit "bottom"
                        | _                  -> JToken.op_Implicit false

                    let buttons = buttons |> Option.defaultValue config.HeaderButtons

                    let button (property : string) (flag : Buttons) =
                        if not <| buttons.HasFlag flag then
                            o.[property] <- JToken.op_Implicit false

                    o.["show"] <- show
                    button "close" Buttons.Close
                    button "popout" Buttons.Popout
                    button "maximise" Buttons.Maximize
                    o

                let rec ofLayout (config : LayoutConfig) (layout : Layout) : JObject =
                    let o = JObject()

                    match layout with
                    | Layout.Element e ->
                        o.["type"] <- JToken.op_Implicit "component"
                        o.["title"] <- JToken.op_Implicit e.Title
                        o.["componentType"] <- JToken.op_Implicit e.Id
                        o.["isClosable"] <- JToken.op_Implicit e.Closable
                        o.["header"] <- ofHeader config e.Buttons e.Header
                        o.["size"] <- JToken.op_Implicit (string e.Size)

                        match e.MinSize with
                        | Some s -> o.["minSize"] <- JToken.op_Implicit $"%d{s}px"
                        | _ -> ()

                        let s = JObject()
                        s.["keepAlive"] <- JToken.op_Implicit e.KeepAlive
                        o.["componentState"] <- s

                    | Layout.Stack s ->
                        let content = s.Content |> List.map (Layout.Element >> ofLayout config >> box)
                        o.["type"] <- JToken.op_Implicit "stack"
                        o.["header"] <- ofHeader config s.Buttons (Some s.Header)
                        o.["size"] <- JToken.op_Implicit (string s.Size)
                        o.["content"] <- JArray(List.toArray content)

                    | Layout.RowOrColumn rc ->
                        let content = rc.Content |> List.map (ofLayout config >> box)
                        o.["type"] <- JToken.op_Implicit (if rc.IsRow then "row" else "column")
                        o.["size"] <- JToken.op_Implicit (string rc.Size)
                        o.["content"] <- JArray(List.toArray content)

                    o

                let ofConfigLabels (config : LayoutConfig) =
                    let o = JObject()
                    o.["close"] <- JToken.op_Implicit config.LabelClose
                    o.["maximise"] <- JToken.op_Implicit config.LabelMaximize
                    o.["minimise"] <- JToken.op_Implicit config.LabelMinimize
                    o.["popout"] <- JToken.op_Implicit config.LabelPopOut
                    o.["popin"] <- JToken.op_Implicit config.LabelPopIn
                    o.["tabDropdown"] <- JToken.op_Implicit config.LabelTabDropdown
                    o

                let ofConfigSettings (config : LayoutConfig) =
                    let o = JObject()
                    o.["popInOnClose"] <- JToken.op_Implicit config.PopInOnClose
                    o.["popoutWholeStack"] <- JToken.op_Implicit config.PopOutWholeStack
                    o.["dragBetweenWindows"] <- JToken.op_Implicit config.DragBetweenWindows
                    o.["dragToNewWindow"] <- JToken.op_Implicit config.DragToNewWindow
                    o.["setPopoutTitle"] <- JToken.op_Implicit config.SetPopoutTitle
                    o

                let ofConfigDimensions (config : LayoutConfig) =
                    let o = JObject()
                    o.["defaultMinItemWidth"] <- JToken.op_Implicit $"{config.MinItemWidth}px"
                    o.["defaultMinItemHeight"] <- JToken.op_Implicit $"{config.MinItemHeight}px"
                    o.["dragProxyWidth"] <- JToken.op_Implicit config.DragProxyWidth
                    o.["dragProxyHeight"] <- JToken.op_Implicit config.DragProxyHeight
                    o

            let ofLayoutConfig (config : LayoutConfig) (layout : Layout) =
                let o = JObject()
                o.["root"] <- JObject.ofLayout config layout
                o.["settings"] <- JObject.ofConfigSettings config
                o.["dimensions"] <- JObject.ofConfigDimensions config
                o.["header"] <- JObject.ofConfigLabels config
                o.ToString Formatting.None

        let inline create (config : LayoutConfig) (root : ^LayoutRoot) =
            let layout = Layout.ofRoot root

            { DefaultLayout = layout
              Config        = config
              SetLayout     = None
              SaveLayout    = None
              LoadLayout    = None }

        let rec update (message : GoldenLayout.Message) (model : GoldenLayout) =
            match message with
            | GoldenLayout.Message.ResetLayout ->
                model |> update (GoldenLayout.Message.SetLayout model.DefaultLayout)

            | GoldenLayout.Message.SetLayout layout ->
                let version = model.SetLayout |> Option.map snd |> Option.defaultValue 0
                { model with SetLayout = Some (layout, version + 1) }

            | GoldenLayout.Message.SaveLayout key ->
                let version = model.SaveLayout |> Option.map snd |> Option.defaultValue 0
                { model with SaveLayout = Some (key, version + 1) }

            | GoldenLayout.Message.LoadLayout key ->
                let version = model.LoadLayout |> Option.map snd |> Option.defaultValue 0
                { model with LoadLayout = Some (key, version + 1) }

        /// Restores the default layout.
        let inline reset (model : GoldenLayout) =
            model |> update GoldenLayout.Message.ResetLayout

        /// Sets the given layout.
        let inline set (root : ^LayoutRoot) (model : GoldenLayout) =
            model |> update (root |> Layout.ofRoot |> GoldenLayout.Message.SetLayout)

        /// Saves the current layout in local storage with the given key.
        let inline save (key : string) (model : GoldenLayout) =
            model |> update (GoldenLayout.Message.SaveLayout key)

        /// Loads the layout from local storage with the given key.
        let inline load (key : string) (model : GoldenLayout) =
            model |> update (GoldenLayout.Message.LoadLayout key)

        /// View for the body of a paged document using Golden Layout.
        let view (attributes : Attribute<'msg> list) (model : AdaptiveGoldenLayout) =
            let attributes =
                attributes @ [
                    clazz "gl-aard-container"
                    attribute "data-theme" model.Config.Theme.Path
                ]

            let channels : (string * Channel) list = [
                "channelSet",  TaggedChannel (model.SetLayout, Json.ofLayoutConfig model.Config >> Pickler.jsonToString)
                "channelSave", TaggedChannel model.SaveLayout
                "channelLoad", TaggedChannel model.LoadLayout
            ]

            let boot =
                let configJson = Json.ofLayoutConfig model.Config model.DefaultLayout
                String.concat "" [
                    "const self = $('#__ID__')[0];"
                    $"aardvark.golden.createLayout(self, {configJson});"
                    "channelSet.onmessage = (layout) => aardvark.golden.setLayout(self, JSON.parse(layout));"
                    "channelSave.onmessage = (key) => aardvark.golden.saveLayout(self, key);"
                    "channelLoad.onmessage = (key) => aardvark.golden.loadLayout(self, key);"
                ]

            let shutdown =
                "aardvark.golden.destroyLayout($('#__ID__')[0])"

            let dependencies =
                [
                    { name = "golden-layout";       url = "resources/golden-layout/bundle/umd/golden-layout.js"; kind = Script }
                    { name = "golden-layout";       url = "resources/golden-layout/css/goldenlayout-base.css";   kind = Stylesheet }
                    { name = "golden-layout-aard";  url = "resources/golden-layout/golden-layout-aard.js";       kind = Script }
                    { name = "golden-layout-aard";  url = "resources/golden-layout/golden-layout-aard.css";      kind = Stylesheet }
                    { name = "golden-layout-theme"; url = model.Config.Theme.Path;                               kind = Stylesheet }
                ]

            require dependencies (
                (onBoot' channels boot >> onShutdown shutdown) (
                    div attributes []
                )
            )

        module WebPart =

            let private template =
                try
                    let asm = typeof<LayoutConfig>.Assembly
                    let path = "resources/golden-layout/popout.html"

                    let resourceName =
                        asm.GetManifestResourceNames()
                        |> Array.tryFind (String.replace "\\" "/" >> (=) path)

                    match resourceName with
                    | Some name ->
                        use stream = asm.GetManifestResourceStream(name)
                        let reader = new StreamReader(stream)
                        reader.ReadToEnd()

                    | _ ->
                        raise <| FileNotFoundException($"Failed to read template HTML from '{path}'.", path)

                with e ->
                    Log.error "[GoldenAard] %s" e.Message
                    $"<!doctype html><html><body><h1>Error</h1>{e.GetType().Name}: {e.Message}</body></html>"

            [<Literal>]
            let route = "/gl-popout"

            let handler (getQueryParam : string -> string option) =
                let id =
                    match getQueryParam "gl-window" with
                    | Some id -> id
                    | _ ->
                        Log.warn "[GoldenAard] Query parameter 'gl-window' missing."
                        Guid.NewGuid().ToString()

                let theme =
                    match getQueryParam "gl-theme" with
                    | Some p -> p
                    | _ ->
                        Log.warn "[GoldenAard] Query parameter 'gl-theme' missing. Falling back to borderless-dark theme for popout."
                        Theme.BorderlessDark.Path

                template
                |> String.replace "__ID__" id
                |> String.replace "__THEME__" theme

            let suave : WebPart =
                path route >=> request (fun r ->
                    let response = handler (r.queryParamOpt >> Option.bind snd)
                    OK response
                )