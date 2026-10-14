# Aardvark.UI

Backend-independent DOM construction, attributes, and events. See
[RENDERING.md](RENDERING.md) for hosting and scenes, and
[ARCHITECTURE.md](ARCHITECTURE.md) for adaptive models.

## DOM Construction

`DomNode<'msg>` represents an element, text node, scene, request-dependent page,
message mapping, sub-app, or empty placeholder.

The auto-open `Static` DSL accepts ordinary attribute and child lists. Its content can
include adaptive nodes. `Incremental` accepts `AttributeMap<'msg>` and adaptive children:

```fsharp
module DynamicContent

open Aardvark.UI
open FSharp.Data.Adaptive

let view (count : aval<int>) (items : alist<string>) : DomNode<unit> =
    div [] [
        text "Count:"
        Incremental.text (count |> AVal.map string)
        Incremental.ul AttributeMap.empty (
            items |> AList.map (fun item -> li [] [text item])
        )
    ]
```

| Constructor | Inputs |
|-------------|--------|
| `div`, `span`, `button`, etc. | `Attribute<'msg> list`, `DomNode<'msg> list` |
| `input`, `img`, `br`, etc. | Attribute list only; void elements |
| `text` | Constant `string`; creates a `span` text node |
| `Incremental.text` | `aval<string>` |
| `Incremental.div` | `AttributeMap<'msg>`, `alist<DomNode<'msg>>` |
| `Incremental.div'` | `AttributeMap<'msg>`, ordinary child list |
| `Incremental.input` | `AttributeMap<'msg>` |
| `Incremental.textarea` | `AttributeMap<'msg>`, `aval<string>` |
| `Incremental.elem` / `voidElem` | Explicit tag name and adaptive attributes/content |
| `DomNode.Empty()` | Empty placeholder |

`Static.Svg` and `Incremental.Svg` use the SVG namespace. `Generic` provides overloaded
constructors for mixed content; it must be opened explicitly if desired. Prefer an
explicit DSL when explaining a function's inputs.

Sources: [Tags.fs](../src/Aardvark.UI/Frontend/Tags.fs),
[DomNode.fs](../src/Aardvark.UI/Dom/DomNode.fs).

## Attributes

`Attribute<'msg>` is `string * AttributeValue<'msg>`. Use `attribute key value`,
`clazz value`, and `style value` for strings. The `=>` operator requires
`open Aardvark.UI.Operators`; it is just a string-attribute constructor.

`AttributeValue` has `String`, `Event`, and `RenderEvent` cases. Dynamic values must be
assembled into an adaptive map/list, not passed to `AttributeMap.ofList` as `aval`s:

```fsharp
module DynamicAttributes

open Aardvark.UI
open FSharp.Data.Adaptive

let view (active : aval<bool>) (disabled : aval<bool>) : DomNode<unit> =
    let attributes =
        AttributeMap.ofAList (alist {
            let! isActive = active
            let! isDisabled = disabled
            yield clazz (if isActive then "ui primary button" else "ui button")
            if isDisabled then
                yield attribute "disabled" "disabled"
        })
    Incremental.button attributes (AList.ofList [text "Submit"])
```

- `ofList`, `ofSeq`, `ofArray`: ordinary attributes, merging duplicate names.
- `ofAList`: adaptive attribute list; merges duplicate names in list order.
- `ofAMap`: an `amap<string, AttributeValue<'msg>>`.
- `ofListCond`: `(string * aval<AttributeValue<'msg> option>) list`; `None` omits a key.
  Use `always attribute` and `onlyWhen condition attribute` to construct entries.
- `union` / `unionMany`: merge maps. Strings for `class` concatenate with spaces, `style`
  with semicolons; ordinary string conflicts prefer the right side. Two `Event`s or two
  `RenderEvent`s combine handlers.
- `map`, `choose`, `filter`, `addClass`, `removeClass`: map transformations.

Sources: [AttributeMap.fs](../src/Aardvark.UI/Dom/AttributeMap.fs),
[AttributeValue.fs](../src/Aardvark.UI/Dom/AttributeValue.fs).

## Events

Convenience callbacks such as `onClick`, `onInput`, and `onChange` return **one message**.
Sequence-returning variants such as `onEvent'` and `onInput'` allow zero or multiple
messages. Only the low-level `Event.serverSide` always returns `seq<'msg>`.

```fsharp
module FormEvents

open System
open Aardvark.UI

type Message = Submit | SetValue of string

let view : DomNode<Message> =
    div [] [
        input [onInput' (fun value ->
            if String.IsNullOrWhiteSpace value then Seq.empty
            else Seq.singleton (SetValue value))]
        button [onClick (fun () -> Submit)] [text "Submit"]
    ]
```

| Helper | Callback inputs |
|--------|-----------------|
| `onClick`, `onFocus`, `onBlur` | `unit` |
| `onInput`, `onChange` | `string` |
| `onMouseDown`, `onMouseUp` | `MouseButtons`, `V2i` |
| `onMouseMove` | `V2i` |
| `onMouseDownRel`, `onMouseUpRel`, `onMouseClickRel` | Button, normalized `V2d` position |
| `onMouseMoveRel` | Normalized `V2d` position |
| `onMouseDownAbs`, `onMouseUpAbs`, `onMouseClickAbs` | Button, local `V2d` position, element size |
| `onMouseMoveAbs` | Local position, element size |
| `onKeyDown`, `onKeyUp` | `Aardvark.Application.Keys` |
| `onKeyDownModifiers`, `onKeyUpModifiers` | `KeyModifiers`, key |
| `onWheel` | `V2d` delta |
| `onCapturedPointerDown` / `Up` | Optional prevent-default button, then callback receiving pointer type, button, local `V2i` position |
| `onCapturedPointerMove` | Optional prevent-default button, then callback receiving pointer type and position |
| `onEvent`, `onEvent'` | Event name, JavaScript argument expressions, callback receiving serialized argument strings |

`Event` contains a JavaScript-generating `clientSide` function and a `serverSide` handler.
`Event.create1/2/3` deserialize JSON arguments; `onEvent` passes raw serialized strings.
`js` / `clientEvent` execute client-only JavaScript; they replace `__ID__` with the element ID.

Render lifecycle callbacks use `onBeforeRender` / `onAfterRender` (one message) or their
primed variants (sequences), with `RenderClientInfo` as input. These run on the server.
Do not encode them as ordinary DOM events: legacy render-event names produce runtime warnings.

Sources: [Attributes.fs](../src/Aardvark.UI/Frontend/Attributes.fs),
[Event.fs](../src/Aardvark.UI/Dom/Event.fs).

## Render Controls

| Constructor | Inputs after camera (`aval<Camera>`) |
|-------------|-------------------------------------|
| `renderControl` | Attribute list, `ISg<'msg>` |
| `renderControl'` | Attribute list, config, scene |
| `Incremental.renderControl` | `AttributeMap<'msg>`, scene |
| `Incremental.renderControl'` | Attribute map, config, scene |
| `Incremental.renderControlWithClientValues` | Attribute map, `RenderClientValues -> ISg<'msg>` |
| `Incremental.renderControlWithClientValues'` | Attribute map, config, scene factory |

`RenderControlConfig.standard` / `fillHeight` preserves left/right frustum bounds and
scales top/bottom to the viewport aspect ratio. `fillWidth` preserves top/bottom and
scales left/right. Both retain normalized offsets, near/far planes, and projection type;
`noScaling` leaves the frustum unchanged. Set explicit CSS dimensions for the viewport
and its containing layout.

Use the predefined `RenderAttribute` functions:

| Function | Default / purpose |
|----------|-------------------|
| `samples int` | 1; MSAA sample count |
| `quality int` | 80; JPEG quality, ignored for mapped frames |
| `showFps bool` | false; frame-rate overlay |
| `showLoader bool` | true; loading indicator |
| `renderAlways bool` | false; request another frame immediately after processing |
| `useMapping bool` | true; shared-memory transfer when supported by the client |
| `background string` | Black; CSS background / clear color |
| `customLoaderImage string`, `customLoaderImageSize string` | Loader appearance |

These generate the actual `data-show-fps`, `data-render-always`, etc. attributes; older
spellings such as `showFPS` and `data-renderalways` are not current examples to copy.

Sources: [RenderControlConfig.fs](../src/Aardvark.UI/RenderControl/RenderControlConfig.fs),
[RenderControl.fs](../src/Aardvark.UI/RenderControl/RenderControl.fs),
[RenderAttribute definitions](../src/Aardvark.UI/Frontend/Attributes.fs).

## JavaScript Dependencies and Lifecycle

`require : Reference list -> DomNode<'msg> -> DomNode<'msg>` attaches scripts/stylesheets.
A `Reference` contains `kind` (`Script`, `Stylesheet`, or `Module`), `name`, and `url`.
The frontend tracks loaded references by name and kind. Ensure their URLs are served;
`require` does not create HTTP routes.

`onBoot`, `onBoot'`, and `onShutdown` are **node wrappers**, not attributes in a list.
`onBoot` runs when the node is created; `onShutdown` runs when it is removed. Both replace
`__ID__`. Initialize plugins in boot code and release their resources in shutdown code.
For live server-to-client values use [channels](ADVANCED.md#channels-and-javascript-events).

Sources: [higher-order tags](../src/Aardvark.UI/Frontend/Tags.fs),
[reference loading](../src/Aardvark.UI/resources/aardvark.js).

## Message Mapping and Sub-Apps

`UI.map mapping node` lifts child messages into a parent message type.
`subApp childApp` embeds an app without message forwarding.
`subApp' mapOut mapIn childApp` forwards sequences in either direction; both mapping
functions receive the child's immutable model. There is no extra attribute-list argument.

See [sub-app example](../src/Examples/17%20-%20SubApps/App.fs) and
[interfaces](../src/Aardvark.UI/Core/Interface.fs).
