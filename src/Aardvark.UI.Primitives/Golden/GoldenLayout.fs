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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module WindowLayout =

    [<Sealed; AbstractClass>]
    type Converter =
        static member inline ToWindowLayout(layout : WindowLayout) = layout
        static member inline ToWindowLayout(layout : Layout) = { Root = Some layout; PopoutWindows = [] }
        static member inline ToWindowLayout(root : Element) = Converter.ToWindowLayout(Layout.Element root)
        static member inline ToWindowLayout(root : Stack) = Converter.ToWindowLayout(Layout.Stack root)
        static member inline ToWindowLayout(root : RowOrColumn) = Converter.ToWindowLayout(Layout.RowOrColumn root)

    let inline private ofRootAux (_ : ^Z) (item : ^T) =
        ((^Z or ^T) : (static member ToWindowLayout : ^T -> WindowLayout) (item))

    let inline ofRoot (item : ^T) =
        ofRootAux Unchecked.defaultof<Converter> item

[<AutoOpen>]
module Events =

    /// Fired whenever the layout changes. Does not serialize and pass the layout to the callback.
    let inline onLayoutChanged' (callback : unit -> 'msg) =
        onEvent "onLayoutChanged" [] (ignore >> callback)

    /// Fired whenever the layout changes. The first parameter of the callback contains the serialized layout.
    let inline onLayoutChangedRaw (callback : string -> 'msg) =
        onEvent "onSerializedLayoutChanged" [] (List.head >> callback)

    /// Fired whenever the layout changes. The first parameter of the callback contains the new layout.
    let inline onLayoutChanged (callback : WindowLayout -> 'msg) =
        onLayoutChangedRaw (GoldenLayout.Json.deserialize >> callback)

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

    module GoldenLayout =
        open Suave
        open Suave.Filters
        open Suave.Operators
        open Suave.Successful
        open System.IO

        let inline create (config : LayoutConfig) (root : ^LayoutRoot) =
            let layout = WindowLayout.ofRoot root

            { DefaultLayout = layout
              Config        = config
              SetLayout     = None
              SaveLayout    = None
              LoadLayout    = None }

        let rec update (message : GoldenLayout.Message) (model : GoldenLayout) =
            match message with
            | GoldenLayout.Message.ResetLayout ->
                model |> update (GoldenLayout.Message.SetWindowLayout model.DefaultLayout)

            | GoldenLayout.Message.SetLayout layout ->
                let layout = WindowLayout.ofRoot layout
                model |> update (GoldenLayout.Message.SetWindowLayout layout)

            | GoldenLayout.Message.SetWindowLayout layout ->
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

            let serializeLayout =
                attributes |> List.exists (function
                    | "onSerializedLayoutChanged", AttributeValue.Event _ -> true
                    | _ -> false
                )
                |> fun r -> if r then "true" else "false"

            let channels : (string * Channel) list = [
                "channelSet",  TaggedChannel (model.SetLayout, GoldenLayout.Json.serialize model.Config)
                "channelSave", TaggedChannel model.SaveLayout
                "channelLoad", TaggedChannel model.LoadLayout
            ]

            let boot =
                let configJson = GoldenLayout.Json.serialize model.Config model.DefaultLayout
                String.concat "" [
                    "const self = $('#__ID__')[0];"
                    $"aardvark.golden.createLayout(self, {configJson}, {serializeLayout});"
                    "channelSet.onmessage = (layout) => aardvark.golden.setLayout(self, layout);"
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