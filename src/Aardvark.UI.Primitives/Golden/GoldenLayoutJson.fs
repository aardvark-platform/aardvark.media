namespace Aardvark.UI.Primitives.Golden

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Aardvark.Base

[<AutoOpen>]
module GoldenLayoutJson =

    module GoldenLayout =

        module Json =

            [<AutoOpen>]
            module private Extensions =

                let inline private (|JValue|_|) (v : JValue) =
                    if v.Value.GetType() = typeof<'T> then Some <| unbox<'T> v.Value
                    else None

                let private (|JFloat|_|) : JValue -> float option = (|JValue|_|)
                let private (|JFloat32|_|) : JValue -> float32 option = (|JValue|_|)
                let private (|JInt32|_|) : JValue -> int32 option = (|JValue|_|)
                let private (|JUInt32|_|) : JValue -> uint32 option = (|JValue|_|)
                let private (|JInt64|_|) : JValue -> int64 option = (|JValue|_|)
                let private (|JUInt64|_|) : JValue -> uint64 option = (|JValue|_|)

                type JObject with
                    member inline x.TryGetProperty<'T when 'T :> JToken>(key : string) =
                        match x.TryGetValue key with
                        | (true, (:? 'T as value)) -> ValueSome value
                        | _ -> ValueNone

                    member inline x.TryValue<'T>(key : string) : 'T voption =
                        match x.TryGetProperty<JValue> key with
                        | ValueSome (JValue value) -> ValueSome value
                        | _ -> ValueNone

                    member inline x.TryGetNumber(key : string) =
                        match x.TryGetProperty<JValue> key with
                        | ValueSome (JFloat value) -> ValueSome value
                        | ValueSome (JFloat32 value) -> ValueSome (float value)
                        | ValueSome (JInt32 value) -> ValueSome (float value)
                        | ValueSome (JInt64 value) -> ValueSome (float value)
                        | ValueSome (JUInt32 value) -> ValueSome (float value)
                        | ValueSome (JUInt64 value) -> ValueSome (float value)
                        | _ -> ValueNone

            module private Header =

                let serialize (config : LayoutConfig) (buttons : Buttons option) (header : Header option) =
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

                let deserialize (o : JObject) : Header option * Buttons =
                    let header =
                        match o.TryValue "show" with
                        | ValueSome "top" -> Some Header.Top
                        | ValueSome "left" -> Some Header.Left
                        | ValueSome "right" -> Some Header.Right
                        | ValueSome "bottom" -> Some Header.Bottom
                        | _ -> None

                    let mutable buttons = Buttons.All

                    let check (name : string) (value : Buttons) =
                        match o.TryValue name with
                        | ValueSome false -> buttons <- buttons &&& ~~~value
                        | _ -> ()

                    check "close" Buttons.Close
                    check "popout" Buttons.Popout
                    check "maximise" Buttons.Maximize

                    header, buttons

            module private Size =

                let deserialize (o : JObject) : Size =
                    let ctor =
                        match o.TryValue "sizeUnit" with
                        | ValueSome "%" -> ValueSome Size.Percentage
                        | ValueSome "fr" -> ValueSome Size.Weight
                        | _ -> ValueNone

                    ctor |> ValueOption.bind (fun ctor ->
                        match o.TryGetNumber "size" with
                        | ValueSome size -> ValueSome <| ctor (int size)
                        | _ -> ValueNone
                    )
                    |> ValueOption.defaultValue (Size.Weight 1)

            module private Layout =

                let rec serialize (config : LayoutConfig) (layout : Layout) : JObject =
                    let o = JObject()

                    match layout with
                    | Layout.Element e ->
                        o.["type"] <- JToken.op_Implicit "component"
                        o.["title"] <- JToken.op_Implicit e.Title
                        o.["componentType"] <- JToken.op_Implicit e.Id
                        o.["isClosable"] <- JToken.op_Implicit e.Closable
                        o.["header"] <- Header.serialize config e.Buttons e.Header
                        o.["size"] <- JToken.op_Implicit (string e.Size)

                        match e.MinSize with
                        | Some s -> o.["minSize"] <- JToken.op_Implicit $"%d{s}px"
                        | _ -> ()

                        let s = JObject()
                        s.["keepAlive"] <- JToken.op_Implicit e.KeepAlive
                        o.["componentState"] <- s

                    | Layout.Stack s ->
                        let content = s.Content |> List.map (Layout.Element >> serialize config >> box)
                        o.["type"] <- JToken.op_Implicit "stack"
                        o.["header"] <- Header.serialize config s.Buttons (Some s.Header)
                        o.["size"] <- JToken.op_Implicit (string s.Size)
                        o.["content"] <- JArray(List.toArray content)

                    | Layout.RowOrColumn rc ->
                        let content = rc.Content |> List.map (serialize config >> box)
                        o.["type"] <- JToken.op_Implicit (if rc.IsRow then "row" else "column")
                        o.["size"] <- JToken.op_Implicit (string rc.Size)
                        o.["content"] <- JArray(List.toArray content)

                    o

                let rec deserialize (o : JObject) : Layout =
                    match o.TryValue "type" with
                    | ValueSome "component" ->
                        let header =
                            o.TryGetProperty<JObject> "header"
                            |> ValueOption.map Header.deserialize
                            |> ValueOption.toOption

                        let keepAlive =
                            o.TryGetProperty<JObject> "componentState"
                            |> ValueOption.bind (fun s -> s.TryValue "keepAlive")
                            |> ValueOption.defaultValue true

                        Layout.Element {
                            Id        = o.Value "componentType"
                            Title     = o.Value "title"
                            Closable  = o.TryValue "isClosable" |> ValueOption.defaultValue true
                            Header    = header |> Option.bind fst
                            Buttons   = header |> Option.map snd
                            MinSize   = o.TryGetNumber "minSize" |> ValueOption.map int |> ValueOption.toOption
                            Size      = Size.deserialize o
                            KeepAlive = keepAlive
                        }

                    | ValueSome "stack" ->
                        let header =
                            o.TryGetProperty<JObject> "header"
                            |> ValueOption.map Header.deserialize
                            |> ValueOption.toOption

                        let content =
                            o.TryGetProperty<JArray> "content"
                            |> ValueOption.map (List.ofSeq >> List.map (unbox<JObject> >> deserialize >> function
                                | Layout.Element e -> e
                                | l -> failwithf "[Golden] Expected element but got %A" l
                            ))
                            |> ValueOption.defaultValue []

                        Layout.Stack {
                            Header  = header |> Option.bind fst |> Option.defaultValue Header.Top
                            Buttons = header |> Option.map snd
                            Size    = Size.deserialize o
                            Content = content
                        }

                    | ValueSome rowOrColumn when rowOrColumn = "row" || rowOrColumn = "column" ->
                        let content =
                            o.TryGetProperty<JArray> "content"
                            |> ValueOption.map (List.ofSeq >> List.map (unbox<JObject> >> deserialize))
                            |> ValueOption.defaultValue []

                        Layout.RowOrColumn {
                            IsRow   = rowOrColumn = "row"
                            Size    = Size.deserialize o
                            Content = content
                        }

                    | _ ->
                        failwithf "[Golden] Cannot determine type of %A" o

            module private PopoutWindow =

                let serialize (config : LayoutConfig) (popout : PopoutWindow) =
                    let window = JObject()

                    popout.Position |> Option.iter (fun p ->
                        window.["left"] <- JToken.op_Implicit p.X
                        window.["top"] <- JToken.op_Implicit p.Y
                    )

                    popout.Size |> Option.iter (fun s ->
                        window.["width"] <- JToken.op_Implicit s.X
                        window.["height"] <- JToken.op_Implicit s.Y
                    )

                    let o = JObject()
                    o.["root"] <- Layout.serialize config popout.Root
                    o.["window"] <- window
                    o

                let tryDeserialize (o : JObject) : PopoutWindow option =
                    match o.TryGetProperty<JObject> "root" with
                    | ValueSome root ->
                        let getV2i =
                            match o.TryGetProperty<JObject> "window" with
                            | ValueSome w ->
                                fun x y ->
                                    match w.TryGetNumber x, w.TryGetNumber y with
                                    | ValueSome x, ValueSome y -> Some <| V2i(x, y)
                                    | _ -> None

                            | _ -> fun _ _ -> None

                        Some {
                            Root     = Layout.deserialize root
                            Position = getV2i "left" "top"
                            Size     = getV2i "width" "height"
                        }

                    | _ -> None

            let serialize (config : LayoutConfig) (layout : WindowLayout) =
                let labels = JObject()
                labels.["close"] <- JToken.op_Implicit config.LabelClose
                labels.["maximise"] <- JToken.op_Implicit config.LabelMaximize
                labels.["minimise"] <- JToken.op_Implicit config.LabelMinimize
                labels.["popout"] <- JToken.op_Implicit config.LabelPopOut
                labels.["popin"] <- JToken.op_Implicit config.LabelPopIn
                labels.["tabDropdown"] <- JToken.op_Implicit config.LabelTabDropdown

                let settings = JObject()
                settings.["popInOnClose"] <- JToken.op_Implicit config.PopInOnClose
                settings.["popoutWholeStack"] <- JToken.op_Implicit config.PopOutWholeStack
                settings.["dragBetweenWindows"] <- JToken.op_Implicit config.DragBetweenWindows
                settings.["dragToNewWindow"] <- JToken.op_Implicit config.DragToNewWindow
                settings.["setPopoutTitle"] <- JToken.op_Implicit config.SetPopoutTitle

                let dimensions = JObject()
                dimensions.["defaultMinItemWidth"] <- JToken.op_Implicit $"{config.MinItemWidth}px"
                dimensions.["defaultMinItemHeight"] <- JToken.op_Implicit $"{config.MinItemHeight}px"
                dimensions.["dragProxyWidth"] <- JToken.op_Implicit config.DragProxyWidth
                dimensions.["dragProxyHeight"] <- JToken.op_Implicit config.DragProxyHeight

                let popouts =
                    layout.PopoutWindows
                    |> List.map (PopoutWindow.serialize config)
                    |> Array.ofList
                    |> JArray

                let result = JObject()

                layout.Root |> Option.iter (fun root ->
                    result.["root"] <- Layout.serialize config root
                )

                result.["settings"] <- settings
                result.["dimensions"] <- dimensions
                result.["header"] <- labels
                result.["openPopouts"] <- popouts
                result.ToString Formatting.None

            let deserialize (json : string) : WindowLayout =
                let o = JObject.Parse json

                let root =
                    o.TryGetProperty<JObject> "root"
                    |> ValueOption.map Layout.deserialize
                    |> ValueOption.toOption

                let popouts =
                    o.TryGetProperty<JArray> "openPopouts"
                    |> ValueOption.map (List.ofSeq >> List.choose (unbox<JObject> >> PopoutWindow.tryDeserialize))
                    |> ValueOption.defaultValue []

                { Root          = root
                  PopoutWindows = popouts }