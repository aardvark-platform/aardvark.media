# Aardvark.UI Reference

Core UI framework for building reactive, incremental web applications with integrated 3D rendering.

---

## DomNode<'msg>

Abstract base type representing UI elements in the virtual DOM. All HTML elements, text nodes, and render controls are represented as `DomNode<'msg>` where `'msg` is the message type returned by event handlers.

### DomNode Variants

| Variant | Description | Created By |
|---------|-------------|------------|
| `EmptyNode<'msg>` | Empty placeholder node | `DomNode.Empty()` |
| `InnerNode<'msg>` | HTML element with children | `DomNode.Element(tag, ns, attributes, children)` |
| `VoidNode<'msg>` | Self-closing HTML element | `DomNode.Element(tag, ns, attributes)` |
| `TextNode<'msg>` | Text content node | `DomNode.Text(tag, ns, attributes, text)` |
| `SceneNode<'msg>` | 3D render control container | `DomNode.Scene(attributes, scene, getClientState)` |
| `PageNode<'msg>` | Request-dependent content | `DomNode.Page(content)` |
| `MapNode<'inner, 'outer>` | Message mapping wrapper | `DomNode.Map(mapping, node)` |
| `SubAppNode<'model, 'inner, 'outer>` | Embedded sub-application | `DomNode.SubApp(app)` |

### Static Constructors

```fsharp
// Static DSL (constant strings)
div [] [
    text "Hello"
    button [onClick (fun () -> Increment)] [text "Click"]
]

// Incremental DSL (adaptive values)
Incremental.div (AttributeMap.empty) (
    alist {
        yield Incremental.text (model.value |> AVal.map string)
    }
)
```

---

## HTML DSL - Tags Module

Two primary DSL styles: **Static** (default) and **Incremental** (explicit module).

### Static Tags (Constant Content)

```fsharp
// Content sectioning
div : list<Attribute<'msg>> -> list<DomNode<'msg>> -> DomNode<'msg>
section, article, aside, nav, header, footer, h1..h6

// Text content
p, ul, ol, li, dl, dt, dd, pre, figure, figcaption, main

// Inline semantics
span, a, strong, em, code, kbd, abbr, cite, mark, small, sub, sup

// Forms
button, input, textarea, select, option, label, fieldset, form

// Tables
table, thead, tbody, tfoot, tr, th, td, caption, colgroup

// Media
img, audio, video, iframe

// Interactive
details, summary, dialog

// Special
text : string -> DomNode<'msg>  // Wraps text in span
br, hr : list<Attribute<'msg>> -> DomNode<'msg>  // Void elements
```

### Incremental Tags (Adaptive Content)

```fsharp
module Incremental =
    // Core constructors
    elem : string -> AttributeMap<'msg> -> alist<DomNode<'msg>> -> DomNode<'msg>
    voidElem : string -> AttributeMap<'msg> -> DomNode<'msg>
    text : aval<string> -> DomNode<'msg>

    // All standard HTML tags available with incremental signatures
    div : AttributeMap<'msg> -> alist<DomNode<'msg>> -> DomNode<'msg>
    input : AttributeMap<'msg> -> DomNode<'msg>
    textarea : AttributeMap<'msg> -> aval<string> -> DomNode<'msg>

    // Helper for mixed static/dynamic children
    div' : AttributeMap<'msg> -> list<DomNode<'msg>> -> DomNode<'msg>
```

### SVG Support

Both Static and Incremental modules contain `Svg` submodules:

```fsharp
module Svg =
    let svgNS = "http://www.w3.org/2000/svg"

    // Containers
    svg, g, defs, marker, foreignObject

    // Shapes
    circle, ellipse, rect, line, path, polygon, polyline

    // Text
    text : AttributeMap<'msg> -> aval<string> -> DomNode<'msg>  // Incremental
    text : list<Attribute<'msg>> -> string -> DomNode<'msg>     // Static
    tspan

    // Gradients & Filters
    linearGradient, radialGradient, stop, filter
    feBlend, feGaussianBlur, feColorMatrix, feOffset, feMerge, etc.

    // Attribute helpers
    width, height, viewBox, cx, cy, r, stroke, strokeWidth, fill
```

---

## Attributes Module

### Core Attribute Constructors

```fsharp
type Attribute<'msg> = string * AttributeValue<'msg>

attribute : string -> string -> Attribute<'msg>
clazz : string -> Attribute<'msg>  // class attribute
style : string -> Attribute<'msg>
js : string -> string -> Attribute<'msg>  // Client-side only JS

// Convenience operator (requires open Operators)
(=>) : string -> string -> Attribute<'msg>
// Example: "width" => "100%"
```

### AttributeMap Operations

```fsharp
module AttributeMap =
    empty : AttributeMap<'msg>
    single : string -> AttributeValue<'msg> -> AttributeMap<'msg>
    ofList : list<Attribute<'msg>> -> AttributeMap<'msg>
    ofSeq : seq<Attribute<'msg>> -> AttributeMap<'msg>
    ofAList : alist<Attribute<'msg>> -> AttributeMap<'msg>

    // Conditional attributes
    ofListCond : list<string * aval<Option<AttributeValue<'msg>>>> -> AttributeMap<'msg>

    union : AttributeMap<'msg> -> AttributeMap<'msg> -> AttributeMap<'msg>
    unionMany : list<AttributeMap<'msg>> -> AttributeMap<'msg>

    map : (string -> AttributeValue<'a> -> AttributeValue<'b>) -> AttributeMap<'a> -> AttributeMap<'b>
    mapAttributes : (AttributeValue<'a> -> AttributeValue<'b>) -> AttributeMap<'a> -> AttributeMap<'b>

    addClass : string -> AttributeMap<'msg> -> AttributeMap<'msg>
    removeClass : string -> AttributeMap<'msg> -> AttributeMap<'msg>
```

### AttributeValue Combining Rules

When attributes conflict during merge:

| Attribute Name | Combine Strategy |
|----------------|------------------|
| `class` | Concatenate with space: `"btn" + "btn-primary"` → `"btn btn-primary"` |
| `style` | Concatenate with semicolon: `"width:100%" + "height:100%"` → `"width:100%; height:100%"` |
| Event attributes | Chain handlers (both execute) |
| Others | Right-side wins (replaces) |

---

## Event Handling

### Event<'msg> Type

```fsharp
type Event<'msg> = {
    clientSide : (string -> list<string> -> string) -> string -> string
    serverSide : Guid -> string -> list<string> -> seq<'msg>
}
```

- **clientSide**: Generates JavaScript code for the browser
- **serverSide**: Processes event on server, returns messages

**Critical**: Event handlers return `seq<'msg>`, not single messages. Use `Seq.singleton` for single results.

### Event Constructors

```fsharp
module Event =
    empty : Event<'msg>
    ofTrigger : (unit -> 'msg) -> Event<'msg>
    create1 : string -> ('a -> 'msg) -> Event<'msg>
    create2 : string -> string -> ('a -> 'b -> 'msg) -> Event<'msg>
    create3 : string -> string -> string -> ('a -> 'b -> 'c -> 'msg) -> Event<'msg>

    ofDynamicArgs : list<string> -> (list<string> -> seq<'msg>) -> Event<'msg>
    combine : Event<'msg> -> Event<'msg> -> Event<'msg>
    map : ('a -> 'b) -> Event<'a> -> Event<'b>
```

### Common Event Handlers

```fsharp
module Events =
    // Mouse events
    onClick : (unit -> 'msg) -> Attribute<'msg>
    onMouseDown : (MouseButtons -> V2i -> 'msg) -> Attribute<'msg>
    onMouseUp : (MouseButtons -> V2i -> 'msg) -> Attribute<'msg>
    onMouseMove : (V2i -> 'msg) -> Attribute<'msg>
    onMouseEnter, onMouseLeave, onMouseOver, onMouseOut : (V2i -> 'msg) -> Attribute<'msg>

    // Relative position events (normalized 0-1)
    onMouseDownRel : (MouseButtons -> V2d -> 'msg) -> Attribute<'msg>
    onMouseUpRel : (MouseButtons -> V2d -> 'msg) -> Attribute<'msg>
    onMouseMoveRel : (V2d -> 'msg) -> Attribute<'msg>
    onMouseClickRel : (MouseButtons -> V2d -> 'msg) -> Attribute<'msg>

    // Absolute position events (pixel coords + element size)
    onMouseDownAbs : (MouseButtons -> V2d -> V2d -> 'msg) -> Attribute<'msg>
    onMouseUpAbs : (MouseButtons -> V2d -> V2d -> 'msg) -> Attribute<'msg>
    onMouseMoveAbs : (V2d -> V2d -> 'msg) -> Attribute<'msg>

    // Pointer events (touch/pen/mouse)
    onCapturedPointerDown : Option<int> -> (PointerType -> MouseButtons -> V2i -> 'msg) -> Attribute<'msg>
    onCapturedPointerUp : Option<int> -> (PointerType -> MouseButtons -> V2i -> 'msg) -> Attribute<'msg>
    onCapturedPointerMove : Option<int> -> (PointerType -> V2i -> 'msg) -> Attribute<'msg>

    // Keyboard events
    onKeyDown : (Keys -> 'msg) -> Attribute<'msg>
    onKeyUp : (Keys -> 'msg) -> Attribute<'msg>
    onKeyDownModifiers : (KeyModifiers -> Keys -> 'msg) -> Attribute<'msg>
    onKeyUpModifiers : (KeyModifiers -> Keys -> 'msg) -> Attribute<'msg>

    // Form events
    onChange : (string -> 'msg) -> Attribute<'msg>
    onInput : (string -> 'msg) -> Attribute<'msg>  // Continuous updates
    onFocus : (unit -> 'msg) -> Attribute<'msg>
    onBlur : (unit -> 'msg) -> Attribute<'msg>

    // Mouse wheel
    onWheel : (V2d -> 'msg) -> Attribute<'msg>
    onWheel' : (V2d -> V2d -> 'msg) -> Attribute<'msg>  // delta, normalized position

    // Conditional attributes
    always : Attribute<'msg> -> string * aval<Option<AttributeValue<'msg>>>
    onlyWhen : aval<bool> -> Attribute<'msg> -> string * aval<Option<AttributeValue<'msg>>>

    // Raw event construction
    onEvent : string -> list<string> -> (list<string> -> 'msg) -> Attribute<'msg>
    onEvent' : string -> list<string> -> (list<string> -> seq<'msg>) -> Attribute<'msg>
```

### KeyModifiers Record

```fsharp
type KeyModifiers = {
    shift : bool
    alt : bool
    ctrl : bool
}
```

---

## Incremental Module

For adaptive/reactive content that updates automatically when dependencies change.

```fsharp
module Incremental =
    // Text
    text : aval<string> -> DomNode<'msg>

    // Generic elements
    elem : string -> AttributeMap<'msg> -> alist<DomNode<'msg>> -> DomNode<'msg>
    voidElem : string -> AttributeMap<'msg> -> DomNode<'msg>

    // RenderControl variants
    renderControl :
        aval<Camera> -> AttributeMap<'msg> -> ISg<'msg> -> DomNode<'msg>

    renderControl' :
        aval<Camera> -> AttributeMap<'msg> -> RenderControlConfig -> ISg<'msg> -> DomNode<'msg>

    renderControlWithClientValues :
        aval<Camera> -> AttributeMap<'msg> -> (ClientValues -> ISg<'msg>) -> DomNode<'msg>

    // All standard HTML elements available with incremental signatures
    div, span, button, input, textarea, table, tr, td, etc.
```

---

## RenderControl - 3D Rendering

### RenderControlConfig

```fsharp
type RenderControlConfig = {
    adjustAspect : V2i -> Frustum -> Frustum
}

module RenderControlConfig =
    standard : RenderControlConfig        // Fills height (most common)
    fillHeight : RenderControlConfig      // Same as standard
    fillWidth : RenderControlConfig       // Fills width
    noScaling : RenderControlConfig       // No aspect adjustment
```

**Gotcha**: `standard` and `fillHeight` adjust frustum to fill height, maintaining aspect by width. For square viewports or width-fill behavior, use `fillWidth` or `noScaling`.

### Creating RenderControls

#### Static Camera, Static SceneGraph

```fsharp
renderControl
    (AVal.constant camera)
    [attribute "style" "width: 100%; height: 100%"]
    sceneGraph
```

#### Adaptive Camera

```fsharp
Incremental.renderControl
    model.camera
    (AttributeMap.ofList [
        attribute "showFPS" "true"
        attribute "data-samples" "4"
    ])
    sg
```

#### With Custom Config

```fsharp
renderControl'
    camera
    [style "width: 100%; height: 100%"]
    RenderControlConfig.fillWidth
    sg
```

#### With ClientValues (server-side adaptive)

```fsharp
renderControlWithClientValues camera attributes (fun clientValues ->
    sg
    |> Sg.uniform "ViewportSize" clientValues.size
    |> Sg.viewTrafo clientValues.viewTrafo
)
```

### RenderControl Attributes

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `showFPS` | bool | false | Display frame rate overlay |
| `showLoader` | bool | true | Show loading spinner during render |
| `data-renderalways` | int | 0 | 1 = continuous render, 0 = incremental |
| `data-samples` | int | 1 | MSAA sample count (1, 2, 4, 8) |
| `style` | string | - | CSS styling (width/height typically needed) |

### Scene Event Handling

RenderControl automatically handles 3D picking and events when using `FreeFlyController` or similar:

```fsharp
FreeFlyController.controlledControl
    model.cameraState
    Camera
    (Frustum.perspective 60.0 0.1 100.0 1.0 |> AVal.constant)
    attributes
    sceneGraph
```

---

## require Function - JS/CSS Dependencies

```fsharp
let require (refs : list<Reference>) (node : DomNode<'msg>) : DomNode<'msg>

type Reference = {
    kind : ReferenceKind
    name : string
    url : string
}

type ReferenceKind =
    | Script
    | Stylesheet
    | Module
```

### Usage Example

```fsharp
let view model =
    require [
        { kind = Script; name = "d3"; url = "https://d3js.org/d3.v7.min.js" }
        { kind = Stylesheet; name = "custom"; url = "/custom.css" }
    ] (
        div [] [
            div [onBoot "d3.select('#__ID__').text('Hello from D3')"] []
        ]
    )
```

Dependencies are loaded once globally, tracked by `name`. Multiple `require` calls with same name are idempotent.

---

## onBoot and onShutdown

### onBoot

Executes JavaScript when element is added to DOM.

```fsharp
onBoot : string -> DomNode<'msg> -> DomNode<'msg>
```

- `__ID__` placeholder replaced with element's actual ID
- Use for initializing JS libraries, jQuery plugins, custom behavior

```fsharp
div [
    clazz "ui dropdown"
    onBoot "$('#__ID__').dropdown();"  // Initialize Fomantic-UI dropdown
] [
    text "Select..."
]
```

### onBoot' with Channels

```fsharp
onBoot' : list<string * Channel> -> string -> DomNode<'msg> -> DomNode<'msg>
```

Channels push adaptive values from server to client:

```fsharp
let boot = """
    myChannel.onmessage = function(value) {
        $('#__ID__').text(value);
    };
"""

div [
    onBoot' ["myChannel", model.text.Channel] boot
] []
```

### onShutdown

Executes JavaScript when element is removed from DOM.

```fsharp
onShutdown : string -> DomNode<'msg> -> DomNode<'msg>

div [
    onBoot "$('#__ID__').modal('show');"
    onShutdown "$('#__ID__').modal('dispose');"
] [...]
```

---

## Common Patterns

### Conditional Attributes

```fsharp
let attributes =
    AttributeMap.ofListCond [
        always <| style "width: 100%"
        "disabled", model.isDisabled |> AVal.map (fun disabled ->
            if disabled then Some (AttributeValue.String "disabled") else None
        )
    ]
```

### Dynamic Class Names

```fsharp
let btnClass =
    model.isActive |> AVal.map (fun active ->
        if active then "ui primary button" else "ui button"
    )

Incremental.button
    (AttributeMap.ofList [
        "class", btnClass |> AVal.map (AttributeValue.String >> Some)
    ])
    (AList.ofList [text "Click"])
```

### Combining Static and Incremental

```fsharp
div [] [
    text "Static header"
    Incremental.div (AttributeMap.empty) (
        alist {
            let! items = model.items
            for item in items do
                yield div [] [text item]
        }
    )
]
```

### Table Generation

```fsharp
let viewTable (data : alist<Row>) =
    Incremental.table (AttributeMap.ofList [clazz "ui table"]) (
        alist {
            yield thead [] [
                tr [] [
                    th [] [text "Name"]
                    th [] [text "Value"]
                ]
            ]
            yield Incremental.tbody (AttributeMap.empty) (
                data |> AList.map (fun row ->
                    tr [] [
                        td [] [text row.name]
                        Incremental.td (AttributeMap.empty) (
                            AList.ofList [Incremental.text (row.value |> AVal.map string)]
                        )
                    ]
                )
            )
        }
    )
```

### Form with Validation

```fsharp
let view (model : AdaptiveModel) =
    let inputClass =
        model.isValid |> AVal.map (fun valid ->
            if valid then "ui input" else "ui input error"
        )

    div [] [
        Incremental.div (
            AttributeMap.ofList [
                "class", inputClass |> AVal.map (AttributeValue.String >> Some)
            ]
        ) (AList.ofList [
            input [
                attribute "type" "text"
                attribute "placeholder" "Enter value..."
                onInput SetValue
            ]
        ])
        button [onClick (fun () -> Submit)] [text "Submit"]
    ]
```

### Multi-Event Handler

```fsharp
button [
    onClick (fun () -> ButtonClicked)
    onMouseEnter (fun pos -> MouseEntered pos)
    onMouseLeave (fun pos -> MouseLeft pos)
] [text "Hover me"]
```

Events combine automatically - all handlers will execute.

---

## Working with Fomantic-UI Components

Fomantic-UI (Semantic-UI) components require manual jQuery initialization via `onBoot`:

```fsharp
// Dropdown
div [
    clazz "ui dropdown"
    onBoot "$('#__ID__').dropdown();"
] [
    text "Options"
    i [clazz "dropdown icon"] []
    div [clazz "menu"] [
        div [clazz "item"] [text "Option 1"]
        div [clazz "item"] [text "Option 2"]
    ]
]

// Modal
div [
    clazz "ui modal"
    onBoot "$('#__ID__').modal('show');"
    onShutdown "$('#__ID__').modal('dispose');"
] [
    div [clazz "header"] [text "Modal Title"]
    div [clazz "content"] [text "Modal content..."]
]

// Checkbox with callback
div [
    clazz "ui checkbox"
    onBoot "$('#__ID__').checkbox({ onChange: function() { aardvark.processEvent('__ID__', 'onchange', $(this).checkbox('is checked').toString()); } });"
] [
    input [attribute "type" "checkbox"]
    label [] [text "Check me"]
]
```

**Gotcha**: Fomantic-UI components don't auto-initialize. Always use `onBoot` for dropdowns, modals, checkboxes, accordions, etc.

---

## Message Mapping (SubApps)

### UI.map

```fsharp
UI.map : ('T1 -> 'T2) -> DomNode<'T1> -> DomNode<'T2>

let childView =
    div [] [
        button [onClick (fun () -> ChildMsg.Increment)] [text "+"]
    ]

let parentView =
    UI.map ParentMsg.Child childView
    // ChildMsg.Increment becomes ParentMsg.Child ChildMsg.Increment
```

### SubApp Integration

```fsharp
// Isolate child component
subApp childApp

// With message translation
subApp'
    (fun model innerMsg ->
        // Map child messages to parent
        match innerMsg with
        | ChildMsg.Done result -> Seq.singleton (ParentMsg.GotResult result)
        | _ -> Seq.empty
    )
    (fun model outerMsg ->
        // Map parent messages to child
        Seq.empty
    )
    []
    childApp
```

---

## Performance Tips

1. **Prefer Incremental for dynamic lists**: Use `alist` instead of recreating entire lists
2. **Minimize AttributeMap updates**: Merge stable attributes, keep dynamic ones separate
3. **Use `AVal.map` chains efficiently**: Combine multiple maps into single operation
4. **Avoid unnecessary AVal.force**: Let incremental system propagate changes
5. **RenderControl**: Use `data-renderalways="0"` for incremental rendering (default)

---

## Type Signatures Summary

```fsharp
// Core types
DomNode<'msg>
AttributeMap<'msg>
AttributeValue<'msg> = String of string | Event of Event<'msg> | RenderEvent of (ClientInfo -> seq<'msg>)
Event<'msg> = { clientSide: ...; serverSide: ... }

// Construction
div : list<Attribute<'msg>> -> list<DomNode<'msg>> -> DomNode<'msg>
Incremental.div : AttributeMap<'msg> -> alist<DomNode<'msg>> -> DomNode<'msg>
text : string -> DomNode<'msg>
Incremental.text : aval<string> -> DomNode<'msg>

// Events
onClick : (unit -> 'msg) -> Attribute<'msg>
onChange : (string -> 'msg) -> Attribute<'msg>
onMouseDown : (MouseButtons -> V2i -> 'msg) -> Attribute<'msg>

// Rendering
renderControl : aval<Camera> -> list<Attribute<'msg>> -> ISg<'msg> -> DomNode<'msg>
Incremental.renderControl : aval<Camera> -> AttributeMap<'msg> -> ISg<'msg> -> DomNode<'msg>

// Higher-order
require : list<Reference> -> DomNode<'msg> -> DomNode<'msg>
onBoot : string -> DomNode<'msg> -> DomNode<'msg>
UI.map : ('a -> 'b) -> DomNode<'a> -> DomNode<'b>
```

---

## Gotchas

1. **Event handlers return sequences**: Always use `Seq.singleton msg`, not just `msg`
2. **Fomantic-UI needs manual init**: Use `onBoot "$('#__ID__').dropdown();"` for components
3. **RenderControl aspect ratio**: Default is `fillHeight` (standard), use `fillWidth` if needed
4. **AttributeMap.union order matters**: Right-side wins for conflicting non-special attributes
5. **Channels require explicit variable names**: Match channel name in `onBoot'` with JS code
6. **Static vs Incremental mixing**: Can mix freely, but child adaptive content needs Incremental constructor
7. **__ID__ placeholder**: Only available in `onBoot`/`onShutdown`, not in regular event handlers
8. **AList.ofList wrapping**: When passing list to incremental function expecting alist, wrap explicitly

---

## See Also

- [PRIMITIVES.md](PRIMITIVES.md) - Camera controllers, animations, UI components
- [ARCHITECTURE.md](ARCHITECTURE.md) - App structure, state management, ThreadPool
- [RENDERING.md](RENDERING.md) - Server setup, 3D rendering pipeline
- [ADVANCED.md](ADVANCED.md) - JS interop, custom components, multi-app patterns

## Source Files

- **Core.fs**: DomNode types, Event, AttributeValue, RenderControl constructors
- **Tags.fs**: Static and Incremental HTML element DSL
- **Attributes.fs**: Event handlers, attribute constructors
- **Updater.fs**: Internal DOM diffing and update logic (implementation detail)

---

*Generated reference for Aardvark.UI v5.x*
