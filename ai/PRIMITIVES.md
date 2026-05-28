# Aardvark.UI.Primitives Reference

High-level reusable UI components and interactive controllers for Aardvark.UI applications.

## Overview

| Component Category | Purpose |
|-------------------|---------|
| **Camera Controllers** | Interactive 3D camera navigation (FreeFly, ArcBall, Orbit, Legacy) |
| **Transformation Controls** | Interactive object manipulation (Translate, Rotate, Scale) |
| **Animation System** | Time-based animations with easing, state management, and callbacks |
| **UI Components** | ColorPicker, GoldenLayout docking, Notifications, Accordion, Dropdown |
| **Dialogs** | File/folder pickers (OpenDialog, SaveDialog) |

---

## Camera Controllers

### CameraModel

Core type for all camera controllers. Marked with `[<ModelType>]` for Adaptify code generation.

```fsharp
[<ModelType>]
type CameraControllerState = {
    view          : CameraView
    freeFlyConfig : FreeFlyConfig
    orbitCenter   : Option<V3d>
    // ... additional state fields
}
```

**Key Fields:**
- `view: CameraView` – Camera position, forward, up, right vectors
- `freeFlyConfig: FreeFlyConfig` – Movement/damping parameters
- `orbitCenter: Option<V3d>` – Pivot point for ArcBall/Orbit
- `targetJump: Option<CameraView>` – Smooth camera transitions

### FreeFlyController

WASD keyboard + mouse camera with momentum-based movement.

**Location:** `src/Aardvark.UI.Primitives/Controllers/FreeFlyController.fs`

**Controls:**
- **W/S** – Forward/Backward
- **A/D** – Strafe Left/Right
- **Shift+W/S** – Up/Down
- **Left Mouse Drag** – Look around
- **Middle Mouse Drag** – Pan
- **Right Mouse Drag** – Dolly (move along view direction)
- **Mouse Wheel** – Zoom

**Message Type:**
```fsharp
type Message =
    | Down of button: MouseButtons * pos: V2i
    | Move of V2i
    | Wheel of V2d
    | KeyDown of KeyModifiers * Keys
    | Rendered  // Integration step for momentum
    | JumpTo of CameraView  // Smooth transition
```

**Usage:**
```fsharp
let model = FreeFlyController.initial

let update model msg =
    FreeFlyController.update model msg

let view model =
    FreeFlyController.controlledControl
        (Mod.constant model)
        CameraMessage
        (AVal.constant frustum)
        AttributeMap.empty
        sceneGraph
```

### FreeFlyConfig

Configures sensitivity, damping, and movement speed.

```fsharp
[<ModelType>]
type FreeFlyConfig = {
    lookAtMouseSensitivity    : float
    lookAtDamping             : float
    panMouseSensitivity       : float
    dollyMouseSensitivity     : float
    zoomMouseWheelSensitivity : float
    moveSensitivity           : float
    touchScalesExponentially  : bool
}

module FreeFlyConfig =
    let initial = {
        lookAtMouseSensitivity = 0.01
        lookAtDamping = 30.0
        panMouseSensitivity = 0.05
        dollyMouseSensitivity = 0.175
        zoomMouseWheelSensitivity = 1.5
        moveSensitivity = 1.0
        touchScalesExponentially = true
    }
```

### ArcBallController

Orbits around a fixed center point (e.g., for object inspection).

**Location:** `src/Aardvark.UI.Primitives/Controllers/ArcBallController.fs`

**Controls:**
- **Left Mouse Drag** – Rotate around center
- **Right Mouse Drag** – Zoom in/out
- **Middle Mouse Drag** – Pan (moves center)
- **WASD** – Move camera (center follows on A/D)

**Key Difference from FreeFly:** Always maintains a pivot point (`orbitCenter`).

### OrbitController

Advanced orbit camera with configurable pan button and angle constraints.

**Location:** `src/Aardvark.UI.Primitives/Controllers/OrbitController.fs`

**Configuration:**
```fsharp
type OrbitControllerConfig = {
    isPan : MouseButtons -> bool
}

type OrbitState = {
    center       : V3d
    phi          : float  // Azimuth
    theta        : float  // Elevation
    _radius      : float
    radiusRange  : Range1d
    thetaRange   : Range1d  // Clamp vertical rotation
}
```

### LegacyCameraController

Deprecated. Use FreeFlyController or ArcBallController instead.

---

## Transformation Controls

Interactive 3D gizmos for object manipulation.

### TrafoModel

```fsharp
[<ModelType>]
type Transformation = {
    workingPose  : Pose
    pose         : Pose
    previewTrafo : Trafo3d
    scale        : float
    mode         : TrafoMode  // Local | Global
    hovered      : Option<Axis>
    grabbed      : Option<PickPoint>
}

type Pose = {
    position : V3d
    rotation : Rot3d
    scale    : V3d
}
```

### TrafoController

Unified interface for translation/rotation/scale gizmos.

**Location:** `src/Aardvark.UI.Primitives/TrafoControls/TrafoController.fs`

**Usage:**
```fsharp
// Render gizmo scene graph
let gizmoSg = TrafoController.createGizmo model kind
// Update on interaction
let model' = TrafoController.update model msg
```

### Specialized Controllers

| Controller | Gizmo Type | Location |
|-----------|-----------|----------|
| `TranslationController` | 3-axis arrows | `TrafoControls/TranslationController.fs` |
| `RotationController` | 3-axis circles | `TrafoControls/RotationController.fs` |
| `ScaleController` | 3-axis boxes | `TrafoControls/ScaleController.fs` |

---

## Animation System

Declarative time-based animations with state machine, easing, and callbacks.

### Core Types (Time.fs, Interface.fs)

```fsharp
// Strongly-typed time values
type Duration   = struct val MicroTime : MicroTime end
type GlobalTime = struct val MicroTime : MicroTime end
type LocalTime  = struct val MicroTime : MicroTime end

// Animation interface
type IAnimation<'Model, 'Value> =
    abstract Duration : Duration
    abstract Create   : Symbol -> IAnimationInstance<'Model, 'Value>
    abstract Scale    : Duration -> IAnimation<'Model, 'Value>
    abstract Ease     : (float -> float) * compose:bool -> IAnimation<'Model, 'Value>
    abstract Loop     : iterations:int * LoopMode -> IAnimation<'Model, 'Value>
```

### Timing

#### Easing Functions (`Animation/Timing/Easing.fs`)

```fsharp
module Easing =
    let linear : float -> float
    let quadraticIn : float -> float
    let cubicInOut : float -> float
    let bounceOut : float -> float
    // ... many more
```

#### DistanceTimeFunction

Maps normalized time → distance along spline (for non-uniform motion).

```fsharp
type DistanceTimeFunction =
    | Linear
    | Custom of (float -> float)
```

### State Management

#### StateMachine (`Animation/State/StateMachine.fs`)

```fsharp
type State =
    | Running of startTime: GlobalTime
    | Stopped
    | Finished
    | Paused of startTime: GlobalTime * pauseTime: GlobalTime

type Action =
    | Start of LocalTime
    | Stop
    | Pause
    | Resume
    | Update of time: LocalTime * finalize: bool
```

#### Observable & Callbacks (`Animation/State/Observable.fs`, `Callbacks.fs`)

```fsharp
type EventType =
    | Start | Resume | Progress | Pause | Stop | Finalize

let subscribe event callback animation
```

### Animation Types

#### Basic Animation (`Animation/Types/Animation.fs`)

```fsharp
let animation duration interpolate startValue endValue =
    Animation.create<'Model, 'Value>(duration, fun t ->
        interpolate startValue endValue t
    )
```

#### Groups (`Animation/Types/Groups.fs`)

```fsharp
let concurrent : IAnimation<'Model> list -> IAnimation<'Model>
let sequential : IAnimation<'Model> list -> IAnimation<'Model>
```

#### Adapter (`Animation/Types/Adapter.fs`)

Adapts animations to work with lenses/prisms for nested models.

```fsharp
let adapt lens animation
```

### Primitives

#### Camera Animations (`Animation/Primitives/Camera.fs`)

```fsharp
let cameraAnimation (start: CameraView) (target: CameraView) duration =
    // Interpolates position, forward, up vectors
```

**Example:**
```fsharp
// Smooth flyto from bookmark system
let flyTo (bookmark: CameraView) =
    let anim =
        Camera.animation model.camera.view bookmark (Duration.ofSeconds 2.0)
        |> Animation.ease Easing.cubicInOut true
    AnimatorMessage.Set("flyto", anim, fun inst -> inst.Start())
```

#### Splines (`Animation/Primitives/Splines.fs`)

```fsharp
type Spline<'T> =
    | Linear of 'T list
    | CatmullRom of 'T list
    | Bezier of 'T list

let splineAnimation spline duration
```

### Animator (AnimatorModel.fs, AnimatorApp.fs, AnimatorSlot.fs)

Manages multiple concurrent animations with named slots.

```fsharp
type Animator<'Model> = {
    Slots       : HashMap<Symbol, AnimatorSlot<'Model>>
    TickRate    : int
    CurrentTick : GlobalTime
}

type AnimatorMessage<'Model> =
    | Tick of GlobalTime
    | Set     of name:Symbol * IAnimation<'Model> * (IAnimationInstance<'Model> -> unit)
    | Enqueue of name:Symbol * ('Model -> IAnimation<'Model>) * action
    | Perform of name:Symbol * action
    | Remove  of Symbol
```

**Usage:**
```fsharp
// In update function
| AnimateCamera target ->
    let anim = Camera.animation model.camera target (Duration.ofSeconds 1.5)
    let animMsg = AnimatorMessage.Set("camera", anim, fun inst -> inst.Start())
    { model with animator = Animator.update animMsg model.animator }

// Thread for ticking
let animThread =
    proclist {
        while true do
            do! Proc.Sleep 16  // ~60 fps
            yield AnimatorMessage.RealTimeTick
    }
```

---

## UI Components

### ColorPicker

Spectrum.js-based color picker with palette support.

**Location:** `src/Aardvark.UI.Primitives/Color/ColorPicker.fs`

```fsharp
type Config = {
    palette                : Palette option
    maxSelectionSize       : int
    displayMode            : DisplayMode  // Disabled | Dropdown | Inline
    pickerStyle            : PickerStyle option
    showAlpha              : bool
}

module Config =
    let Default           // Full picker + palette
    let PaletteOnly       // No picker UI
    let PickerOnly        // No palette
    let Toggle            // Collapsible picker

// Usage
let view model =
    ColorPicker.view
        ColorPicker.Config.Default
        SetColor
        (AVal.constant model.color)
```

### GoldenLayout

Advanced docking layout system (panels, tabs, popouts).

**Location:** `src/Aardvark.UI.Primitives/Golden/`

```fsharp
type Layout =
    | Element of Element
    | Stack of Stack
    | RowOrColumn of RowOrColumn

type GoldenLayout = {
    DefaultLayout : WindowLayout
    Config        : LayoutConfig
    SetLayout     : (WindowLayout * int) option
}

// Usage
let layout =
    GoldenLayout.create config (
        row [
            column [ panel "Scene" sceneView; panel "Props" propsView ]
            panel "Console" consoleView
        ]
    )

let view model =
    GoldenLayout.view [onLayoutChanged LayoutChanged] model.layout
```

**Key Features:**
- Drag-drop panel reorganization
- Popout windows (separate browser windows)
- Persist/restore layouts via localStorage
- Themes: BorderlessDark, Dark, Light, Soda, Translucent

### Notifications

Toast-style notifications.

**Location:** `src/Aardvark.UI.Primitives/Notifications/`

```fsharp
type Notification = {
    Title   : string
    Message : string
    Level   : NotificationLevel  // Success | Info | Warning | Error
}

let show notification model
let clear notificationId model
```

### Accordion, Dropdown, SimplePrimitives

**Location:** `src/Aardvark.UI.Primitives/Primitives/`

```fsharp
// Accordion
let accordion (header: DomNode<'msg>) (body: DomNode<'msg>) =
    div [clazz "ui accordion"] [
        div [clazz "title"] [header]
        div [clazz "content"] [body]
    ]

// Dropdown
type DropdownConfig<'T> = {
    items    : 'T list
    render   : 'T -> DomNode<'msg>
    selected : 'T option
}

let dropdown config onSelect

// SimplePrimitives (Semantic UI wrappers)
let button label msg
let checkbox label isChecked onChange
let slider min max value onChange
```

---

## Dialogs

### OpenDialog

Native file/folder picker.

**Location:** `src/Aardvark.UI.Primitives/OpenDialog.fs`

```fsharp
type OpenDialogConfig = {
    mode          : OpenDialogMode  // File | Folder
    title         : string
    startPath     : string
    filters       : string[]        // e.g., [| "*.obj"; "*.dae" |]
    allowMultiple : bool
}

// Usage
let btn =
    openDialogButton
        { OpenDialogConfig.file with filters = [| "*.json" |] }
        [clazz "ui button"]
        [text "Load File"]
    |> onChooseFile FileSelected
```

### SaveDialog

Similar to OpenDialog but for saving files.

**Location:** `src/Aardvark.UI.Primitives/SaveDialog.fs`

```fsharp
type SaveDialogConfig = {
    title     : string
    startPath : string
    filters   : string[]
}

let btn =
    saveDialogButton config [clazz "ui button"] [text "Save"]
    |> onChooseFile SaveTo
```

---

## Integration Examples

### Camera Setup

```fsharp
// Initialize FreeFly with custom speed heuristic based on scene bounds
let initCamera sceneBounds =
    let center = sceneBounds.Center
    let size = sceneBounds.Size.Length
    let dist = size * 2.0

    let cam =
        { FreeFlyController.initial with
            view = CameraView.lookAt (center + V3d(dist, dist, dist)) center V3d.OOI
            freeFlyConfig =
                { FreeFlyConfig.initial with
                    moveSensitivity = log (size * 0.01)  // Scale to scene
                }
        }
    cam
```

### Animation: Flyto Bookmarks

```fsharp
// Smooth camera transitions using animation system
type BookmarkMsg =
    | FlyToBookmark of CameraView

let update model = function
    | FlyToBookmark target ->
        let anim =
            Camera.animation model.camera.view target (Duration.ofSeconds 2.0)
            |> Animation.ease Easing.cubicInOut true
            |> Animation.subscribe EventType.Finalize (fun _ _ m ->
                // Callback when animation completes
                { m with camera = { m.camera with view = target } }
            )

        let animMsg = AnimatorMessage.Set("flyto", anim, fun inst -> inst.Start())
        { model with animator = Animator.update animMsg model.animator }
```

---

## Gotchas

| Issue | Solution |
|-------|----------|
| **Camera `orbitCenter = None`** causes ArcBall crash | Always initialize with `Some V3d.Zero` or call `ArcBallController.Pick` first |
| **FreeFly momentum persists after blur** | Handle `Blur` message to reset movement vectors |
| **Animation not progressing** | Ensure `AnimatorMessage.Tick` or `RealTimeTick` is called regularly via thread |
| **GoldenLayout panels empty after popout** | Use `Incremental.` rendering; panels lose state if re-rendered from scratch |
| **ColorPicker not responding** | Check `require dependencies` is present; Spectrum.js must load |
| **Trafo gizmo not pickable** | Ensure gizmo scene graph is rendered with correct picking semantics (`Sg.pickable`) |
| **Animation callbacks not firing** | Call `instance.Commit(model, tick)` in update loop |
| **`[<ModelType>]` missing** causes Adaptify errors | All mutable models must have `[<ModelType>]` attribute |

---

## Key Files

```
src/Aardvark.UI.Primitives/
├── Controllers/
│   ├── CameraModel.fs              # Core camera state type
│   ├── FreeFlyController.fs        # WASD + mouse camera
│   ├── ArcBallController.fs        # Orbit around point
│   ├── OrbitController.fs          # Advanced orbit
│   └── LegacyCameraController.fs   # (Deprecated)
├── TrafoControls/
│   ├── TrafoModel.fs               # Transformation state
│   ├── TrafoController.fs          # Main gizmo controller
│   ├── TranslationController.fs    # Translate gizmo
│   ├── RotationController.fs       # Rotate gizmo
│   └── ScaleController.fs          # Scale gizmo
├── Animation/
│   ├── Core/
│   │   ├── Time.fs                 # Duration, GlobalTime, LocalTime
│   │   └── Interface.fs            # IAnimation, EventType, Action, State
│   ├── Timing/
│   │   ├── Easing.fs               # Easing functions
│   │   └── DistanceTimeFunction.fs # Spline parameterization
│   ├── State/
│   │   ├── StateMachine.fs         # Animation state transitions
│   │   ├── Observable.fs           # Event subscription
│   │   └── Callbacks.fs            # Event callbacks
│   ├── Types/
│   │   ├── Animation.fs            # Core animation type
│   │   ├── Adapter.fs              # Lens-based composition
│   │   ├── Groups.fs               # Concurrent/Sequential
│   │   ├── Concurrent.fs           # Parallel animations
│   │   └── Sequential.fs           # Chained animations
│   ├── Primitives/
│   │   ├── Camera.fs               # CameraView interpolation
│   │   ├── Primitives.fs           # Basic value animations
│   │   └── Splines.fs              # Spline animations
│   └── Animator/
│       ├── AnimatorModel.fs        # Animator state + messages
│       ├── AnimatorApp.fs          # Update/view logic
│       └── AnimatorSlot.fs         # Per-animation slot
├── Color/
│   └── ColorPicker.fs              # Spectrum.js wrapper
├── Golden/
│   ├── GoldenLayout.fs             # Main docking API
│   ├── GoldenLayoutModel.fs        # Layout state types
│   ├── GoldenLayoutJson.fs         # Serialization
│   └── GoldenLayoutBuilders.fs     # Layout DSL
├── Notifications/
│   ├── Notifications.fs            # Toast API
│   └── NotificationsModel.fs       # Notification state
├── Primitives/
│   ├── Accordion.fs                # Collapsible panels
│   ├── Dropdown.fs                 # Select dropdown
│   └── SimplePrimitives.fs         # Buttons, sliders, checkboxes
├── OpenDialog.fs                   # Native file picker
└── SaveDialog.fs                   # Native save dialog
```

---

## Quick Reference Table

| Task | Type/Function |
|------|--------------|
| WASD camera | `FreeFlyController.initial`, `FreeFlyController.update` |
| Orbit camera | `ArcBallController.initial`, `ArcBallController.update` |
| Smooth camera transition | `JumpTo of CameraView` message, or `Camera.animation` |
| Adjust camera speed | `freeFlyConfig.moveSensitivity`, `FreeFlyHeuristics.DefaultSpeedHeuristic` |
| 3D gizmo | `TrafoController.createGizmo`, `TrafoController.update` |
| Animate value | `Animation.create duration interpolate start end` |
| Easing | `Animation.ease Easing.cubicInOut compose` |
| Loop animation | `Animation.loop iterations LoopMode.Repeat` |
| Camera animation | `Camera.animation startView endView duration` |
| Concurrent animations | `Animation.concurrent [anim1; anim2]` |
| Sequential animations | `Animation.sequential [anim1; anim2]` |
| Manage animations | `Animator.update`, `AnimatorMessage.Set/Enqueue/Perform` |
| Color picker | `ColorPicker.view config SetColor colorAVal` |
| Docking layout | `GoldenLayout.create config layout`, `GoldenLayout.view` |
| Toast notification | `Notification.show notification model` |
| File picker | `openDialogButton config attrs content`, `onChooseFile` |
| Save dialog | `saveDialogButton config attrs content`, `onChooseFile` |

---

## See Also

- [UI.md](UI.md) - DomNode, events, attributes, RenderControl
- [ARCHITECTURE.md](ARCHITECTURE.md) - App structure, ThreadPool for animations
- [RENDERING.md](RENDERING.md) - 3D rendering, camera integration
- [ADVANCED.md](ADVANCED.md) - Custom scene graphs, performance patterns

---

*Document Size: ~20 KB*
*Last Updated: 2025-12-22*
