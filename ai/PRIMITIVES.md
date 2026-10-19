# UI Primitives

Reusable controllers and components from `Aardvark.UI.Primitives`. Animation APIs use
`Aardvark.UI.Animation`; transformations use `Aardvark.UI.Trafos`; docking uses
`Aardvark.UI.Primitives.Golden`; notifications use `Aardvark.UI.Primitives.Notifications`.

Serve the primitives assembly with the selected backend's
`WebPart.ofType<Aardvark.UI.Primitives.EmbeddedResources>` when using its bundled frontend
resources. Components attaching `require` still need those HTTP resource routes.
See [embedded resources](RENDERING.md#embedded-resources).

## Camera Controllers

| Controller | Immutable / adaptive state | Purpose |
|------------|----------------------------|---------|
| `FreeFlyController` | `CameraControllerState` / `AdaptiveCameraControllerState` | WASD, pointer, wheel, touch, and gamepad navigation with damping |
| `ArcBallController` | Same state types | Orbit around an `orbitCenter` |
| `OrbitController` | `OrbitState` / `AdaptiveOrbitState` | Orbit with radius/angle constraints and configurable pan button |
| `CameraController` | `CameraControllerState` / adaptive counterpart | Older implementation in `LegacyCameraController.fs`; prefer the controllers above for new code |

Integrate a controller in all three parts of the app:

1. Store its immutable state in a `[<ModelType>]` source model, initialized from the
   controller's `initial` value.
2. Lift controller messages into the application's message union and call the controller's
   `update state message` from the application's update function.
3. Pass the generated adaptive state, message mapping, frustum, attribute map, and typed
   scene to `controlledControl`. Map the controller's `threads` into app messages when applicable.

The adaptive state is not `aval<CameraControllerState>`; wrapping an immutable state in
`AVal.constant` does not satisfy `controlledControl`.
[The rendering example](RENDERING.md#3d-rendering) shows the view/message wiring.

`FreeFlyController.controlledControlWithClientValues` and the Orbit equivalent additionally
accept a `RenderControlConfig` before the `RenderClientValues -> ISg<'msg>` factory.
The ArcBall equivalent has no config argument. Check signatures before switching controllers.
FreeFly's supplied attributes already include blur cleanup and `onAfterRender` integration;
do not add a legacy render event to drive it.

`FreeFlyConfig.initial` supplies the complete default configuration. Update only desired
fields instead of constructing a partial record. `FreeFlyHeuristics.DefaultSpeedHeuristic`
can adjust sensitivity for scene units. Initialize ArcBall from `ArcBallController.initial`
(which sets `orbitCenter`) rather than copying an unrelated FreeFly state.
Construct Orbit state with `OrbitState.create center phi theta radius`, not its internal record.

Sources: [camera types](../src/Aardvark.UI.Primitives/Controllers/CameraModel.fs),
[FreeFly](../src/Aardvark.UI.Primitives/Controllers/FreeFlyController.fs),
[ArcBall](../src/Aardvark.UI.Primitives/Controllers/ArcBallController.fs),
[Orbit](../src/Aardvark.UI.Primitives/Controllers/OrbitController.fs).
Example: [render control](../src/Examples/02%20-%20RenderControl/App.fs).

## Transformation Controls

Open `Aardvark.UI.Trafos`. `Transformation` / `AdaptiveTransformation` carry the working
pose, committed pose, preview transform, mode, hover, and grab state.
`TrafoController.initial` creates the state and `TrafoController.Action` defines messages.
The specialized implementations are:

| Module | Update / view |
|--------|---------------|
| `TranslateController` | `updateController state action`, `viewController lift camera adaptiveState` |
| `RotationController` | Same pattern |
| `ScaleController` | Same pattern |

`viewController` returns a typed scene graph; `camera` is `aval<CameraView>`.
Use the appropriate implementation for each operation rather than a nonexistent unified
`createGizmo` API. Preserve picking and coordinate-transform wiring when embedding the gizmo.

Sources: [transformation types](../src/Aardvark.UI.Primitives/TrafoControls/TrafoModel.fs),
[controller state/actions](../src/Aardvark.UI.Primitives/TrafoControls/TrafoController.fs),
[translation](../src/Aardvark.UI.Primitives/TrafoControls/TranslationController.fs),
[rotation](../src/Aardvark.UI.Primitives/TrafoControls/RotationController.fs),
[scale](../src/Aardvark.UI.Primitives/TrafoControls/ScaleController.fs).
Example: [object transformations](../src/Examples/20%20-%20PlaceTransformObjects/App.fs).

## Animation System

Open `Aardvark.UI.Animation`. `IAnimation<'Model, 'Value>` describes a time-varying value;
`IAnimationInstance` is its running instance. `Animator<'Model>` manages named slots.
`Duration`, `GlobalTime`, and `LocalTime` distinguish timing concepts.

A standalone animation definition with a model-updating callback:

```fsharp
module ValueAnimation

open Aardvark.UI.Animation

type Model = { value : float }

let animation : IAnimation<Model, float> =
    Animation.create id
    |> Animation.seconds 1.0
    |> Animation.ease (Easing.InOut EasingFunction.Cubic)
    |> Animation.onProgress (fun _ value model -> { model with value = value })
```

Creating a definition alone does not run it. To integrate with an app:

- Store `[<NonAdaptive>] animator : Animator<Model>` in the immutable model.
- Initialize it with `Animator.initial` and a lens to that field. The
  [animation example](../src/Examples/14%20-%20Animation/App.fs) uses `Model.animator_`;
  generated lenses require the project's `GenerateLenses` setting.
- Pass the **whole application model** to `Animator.update message model` (or to
  `Animator.createAndStart name animation model`), not just its `animator` field.
- Map `Animator.threads model.animator` into app messages and wire
  `onAfterRender (fun _ -> AnimationMessage AnimatorMessage.RealTimeTick)` as appropriate.
  The background ticks also progress animations when the scene is otherwise static.

| Task | API |
|------|-----|
| Define interpolation | `Animation.create (float -> 'Value)`; `create'` also receives the model |
| Set duration | `Animation.duration`, `seconds`, `milliseconds`, etc. |
| Easing | `Animation.ease (Easing.InOut EasingFunction.Cubic)`; `easeCustom compose function` for custom curves |
| Loop | `Animation.loop mode`, `loopN mode count`; `Repeat`, `Mirror`, or `Continue` |
| Model callbacks | `Animation.onProgress`, `onFinalize`, `onStart`, etc. |
| Bind to model field | `Animation.link lens` / `linkMap lens mapping` |
| Camera interpolation | `Animation.Camera.interpolate src dst`; `interpolateTo lens dst model` links it to state |
| Parallel / serial groups | `Animation.concurrent`, `sequential`, `sequential'` |
| Manage slots | `Animator.createAndStart`, `createAndStartDelayed`, `perform`, `remove`; `AnimatorMessage.Enqueue` queues a definition |

Use helpers accepting strings or `Symbol`s for slot names; raw `AnimatorMessage.Set` takes
a `Symbol`. Avoid copying the old `Camera.animation` or `Easing.cubicInOut` examples.

Sources: [animation creation](../src/Aardvark.UI.Primitives/Animation/Types/Animation.fs),
[interfaces](../src/Aardvark.UI.Primitives/Animation/Core/Interface.fs),
[animator](../src/Aardvark.UI.Primitives/Animation/Animator/AnimatorApp.fs),
[callbacks](../src/Aardvark.UI.Primitives/Animation/State/Callbacks.fs),
[camera animations](../src/Aardvark.UI.Primitives/Animation/Primitives/Camera.fs).
Example: [animation app/model](../src/Examples/14%20-%20Animation/Model.fs).

## UI Components

| Component | Current usage / source |
|-----------|------------------------|
| Color picker | `ColorPicker.view config callback color`, where `color : aval<C4b>` and `callback : C4b -> 'msg`; [ColorPicker.fs](../src/Aardvark.UI.Primitives/Color/ColorPicker.fs) |
| Docking | Open `Aardvark.UI.Primitives.Golden`; `GoldenLayout.create config layout`, `update message model`, `view attributes adaptiveModel`; [GoldenLayout.fs](../src/Aardvark.UI.Primitives/Golden/GoldenLayout.fs) |
| Notifications | Open `Aardvark.UI.Primitives.Notifications`; `notification { ... }`, `Notifications.update message state`, `Notifications.container mapping createContainer adaptiveState`; [Notifications.fs](../src/Aardvark.UI.Primitives/Notifications/Notifications.fs) |
| Accordion / dropdown | Use the supplied builders and configuration types; [Accordion.fs](../src/Aardvark.UI.Primitives/Primitives/Accordion.fs), [Dropdown.fs](../src/Aardvark.UI.Primitives/Primitives/Dropdown.fs) |
| Inputs / numeric controls | [UI.Primitives.fs](../src/Aardvark.UI.Primitives/Primitives/UI.Primitives.fs), [SimplePrimitives.fs](../src/Aardvark.UI.Primitives/Primitives/SimplePrimitives.fs) |

`ColorPicker.Config.Default`, `PaletteOnly`, `PickerOnly`, and `Toggle` provide starting
configurations; its view attaches dependencies and initialization itself.

GoldenLayout panel IDs select documents via `pages` / `Pages.Page`. Use its `layout`,
`row`, `column`, `stack`, and `element` builders, not a builder embedding arbitrary DOM
nodes directly as panels. For popouts, add `GoldenLayout.toWebPart http` using the chosen
backend instance in addition to app and resource handlers. `GoldenLayout.save` / `load`
use browser local storage. See the [layout app](../src/Examples/13%20-%20GoldenLayout/App.fs)
and its [server setup](../src/Examples/13%20-%20GoldenLayout/Program.fs).

Notifications use `Notifications.Send`, `Remove`, and `Clear` messages; there is no
`NotificationLevel` record or standalone `Notification.show` API. Follow the
[notifications example](../src/Examples/05%20-%20Notifications/App.fs) for container placement
and message mapping.

## Dialogs

Use the `Dialog` module in `Aardvark.UI.Primitives`. Native dialogs require Aardium or the
CEF bridge; an ordinary browser does not provide them. They select paths on the **client**
machine, not automatically on a remote rendering server.

```fsharp
module FileDialogs

open Aardvark.UI
open Aardvark.UI.Primitives

type Message = OpenFile of string | SaveFile of string

let view : DomNode<Message> =
    let config =
        { DialogConfig.Default with
            Title = "Choose JSON file"
            Filters = ["JSON", ["json"]] }
    div [] [
        Dialog.openFileButton OpenFile config [clazz "ui button"] [text "Open"]
        Dialog.saveFileButton SaveFile config [clazz "ui button"] [text "Save"]
    ]
```

`DialogConfig` fields are `Title`, `Filters : (string * string list) list`, `Multiselect`,
`Modal`, and `DefaultPath`. Start from `DialogConfig.Default`; filters are named extension
lists, not arrays of glob patterns.

| Helper | Callback |
|--------|----------|
| `Dialog.openFileButton`, `saveFileButton`, `openFolderButton` | `string -> 'msg` |
| `Dialog.openFilesButton`, `openFoldersButton` | `string list -> 'msg` |
| `Dialog.button` | Mode (`OpenFile`, `SaveFile`, `OpenFolder`), `string list -> 'msg`, config, attributes, content |

Callbacks precede config, attributes, and content. `Dialog.Incremental` equivalents accept
`aval<DialogConfig>` and adaptive/generic attributes/content. Cancellation or an empty
selection emits no message. Returned paths are converted with `Path.ofUnixStyle`, available
through `open Aardvark.UI`.

Sources: [Dialogs.fs](../src/Aardvark.UI.Primitives/Primitives/Dialogs.fs),
[frontend bridge](../src/Aardvark.UI/resources/aardvark.js),
[CEF dialog handler](../src/Aardvark.Cef.WinForms/DialogHandler.fs).

## See Also

- [ARCHITECTURE.md](ARCHITECTURE.md) — generated models and thread pools
- [UI.md](UI.md) — DOM events and dependency loading
- [RENDERING.md](RENDERING.md) — render controls and resource handlers
- [ADVANCED.md](ADVANCED.md) — channels and request-dependent panels
