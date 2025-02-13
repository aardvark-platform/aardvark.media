### 5.5.3
- Fixed multisampled raw download
- Added pointer event variants with keyboard modifiers
- [ArcBallController] Use captured pointer events
- [FreeFlyController] Fix pointer capture for middle mouse button when using iframes
- [Animation] Improved adaptive sampling scheme for splines to avoid infinite recursion when control points are coinciding
- [Animation] Fixed issue with ticks not being processed when all animations are paused
- [Animation] Adjust action processing to resolve an issue with events when restarting an animation
- [Animation] Fixed broken Animator.get (wrong return type)
- [Animation] Coinciding control points for linear and smooth paths are no longer removed

### 5.5.2
- updated Adaptify.Core to 1.3.0 (using local, new style adaptify)

### 5.5.1
- [Cef.WinForms] Fixed missing dependencies

### 5.5.0
- Updated to NET 8 and Aardvark.Rendering 5.5
- Promoted new color picker from `Aardvark.UI.Primitives.ColorPicker2` to `Aardvark.UI.Primitives`. The old color picker is deleted.
- Renamed `Aardvark.UI.Anewmation` to `Aardvark.UI.Animation`. The old animation system was moved to `Aardvark.UI.Animation.Deprecated`.
- Fixed the namespace of some primitives. E.g. `NumericInput` is now in `Aardvark.UI.Primitives` instead of `Aardvark.UI`.
- Moved `UI.map` from `Aardvark.UI.Primitives` to `Aardvark.UI` namespace and assembly.
- [Primitives] Deleted old dropdown implementation (update to 5.4.5 to see obsolete warnings with replacement suggestions).
- [Primitives] Simplified accordion and moved to `Accordion` module.

### 5.5.0-prerelease0001
- Initial prerelease

### 5.4.5
- [SimplePrimitives] Added accordion variants with custom titles
- [SimplePrimitives] Avoid accordion animation for initially active sections
- [Primitives] Reworked dropdown
- [Animations] Fixed Animator removeAll and removeFinished

### 5.4.4
- [GoldenLayout] Implemented layout deserialization and multi-window layouts (breaking)
- [SimplePrimitives] Optimized checkbox
- [SimplePrimitives] Added accordion
- [Primitives] Added notifications
- [ColorPicker] Improved change message triggers

### 5.4.3
- Added Golden Layout support (https://github.com/aardvark-platform/aardvark.media/wiki/Golden-Layout)
- [Primitives] Added before and after option to numeric input for labels and icons
- [Primitives] Added Html.title to set document.title
- [Giraffe] Added startServer variants and WebPart utilities
- [Suave] Fixed setting default quality if useMapping = false
- [ColorPicker] Fixed issue with positioning and scrolling

### 5.4.2
- Added improved color picker
- Added generic Html.color to convert colors to a CSS color string

### 5.4.1
- Updated Fomantic-UI to 2.9.3
- Added dropdownMultiSelect

### 5.4.0
- Updated Fomantic-UI to 2.8.8
- Fixed and improved resource management
- Updated to Aardvark.Rendering 5.4

### 5.4.0-prerelease0007
- updated to rendering 5.4 prerelease

### 5.4.0-prerelease0006
- updated to rendering 5.3 prerelease

### 5.4.0-prerelease0005
- extend dropdown modes (icon / clearable / nonclearable)

### 5.4.0-prerelease0004
- extend dropdownConfig on event (allow open menu by hover or click)

### 5.4.0-prerelease0003
- prevent dropdown to fire init message

### 5.4.0-prerelease0002
- prevent dropdowns onChange messages (not set be GUI)

### 5.4.0-prerelease0001
- Updated Fomantic-UI to 2.8.8
- Fixed and improved resource management

### 5.3.6
- Fixed memory leak in event handler management
- Updated to Xilium.CefGlue 0.4.0

### 5.3.5
- Fixed assembly version of packages due to missing Aardvark.Build reference (was 1.0.0.0)
- Fixed CEF packages

### 5.3.4
- Updated to Aardvark.Rendering 5.3

### 5.3.3
- exception handling for screenshots

### 5.3.2
- Fixed CEF processes nuget packages
- [Screenshot] Save image with alpha when using PNG

### 5.3.1
- OrbitState: exposed view : CameraView as extension

### 5.3.0
- OrbitState now private, better functions for converting between freefly/orbit modes.

### 5.3.0-prerelease0007
- temporarily reactivate simple media-specific render commands

### 5.3.0-prerelease0006 
- proper shutdown for giraffe app binding 

### 5.3.0-prerelease0005
- proper shutdown for giraffe app binding 

### 5.3.0-prerelease0004
- proper shutdown for giraffe app binding 

### 5.3.0-prerelease0003
- added giraffe backend nupkg 

### 5.3.0-prerelease0002
- experimental giraffe backend

### 5.2.1
- added Mac/ARM64 turbojpeg

### 5.2.0
https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-5.2-changelog

### 5.2.0-prerelease0008
- update rendering

### 5.2.0-prerelease0007
- Initial prerelease for 5.2

### 5.1.14
- [SimplePrimitives.fs] added textarea; js typo fix

### 5.1.13
- added orbit camera controller helpers

### 5.1.12
- switched to f# 5.0 (version bump for base/fshade/suave)

### 5.1.11
- fixed arcball problem on mac

### 5.1.10
- fixed arcball problem on mac

### 5.1.9
- fixed DPI scaling for CEF control
- Remove PBOs and read to host memory directly instead for compatibility

### 5.1.8
- workaround for blocking startup caused by slow network drives (#39)

### 5.1.7
- added animateForwardAndLocation to deprecated animation code (used in Dibit and PRo3D) with corrected final interation. All other deprecated animations do not reach their target.

### 5.1.6
- fixed cursor change problem on netcoreapp

### 5.1.5
- color picker can optionally save previous picks

### 5.1.4
- added Adaptify.Core to paket.template of aardvark.ui to fix missing references in applications

### 5.1.3
- updated dependencies

### 5.1.2
- updated Aardvark.Base
- updated Aardvark.Rendering (breaking changes)

### 5.1.1
- updated packages

### 5.1.0
- updated to FSharp.Data.Adaptive 1.1 and base 5.1 track