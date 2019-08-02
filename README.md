[![Build status](https://ci.appveyor.com/api/projects/status/qgpb8dtjjxdwqjv2/branch/master?svg=true)](https://ci.appveyor.com/project/haraldsteinlechner/aardvark-media/branch/master)
[![Build Status](https://travis-ci.org/aardvark-platform/aardvark.media.svg?branch=master)](https://travis-ci.org/aardvark-platform/aardvark.media)
[![Join the chat at https://gitter.im/aardvark-platform/Lobby](https://img.shields.io/badge/gitter-join%20chat-blue.svg)](https://gitter.im/aardvark-platform/Lobby)
[![license](https://img.shields.io/github/license/aardvark-platform/aardvark.media.svg)](https://github.com/aardvark-platform/aardvark.media/blob/master/LICENSE)

[The Aardvark Platform](https://aardvarkians.com/) |
[Platform Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki) | 
[The Platform Walkthrough Repository](https://github.com/aardvark-platform/walkthrough) |
[Media Examples](https://github.com/aardvark-platform/aardvark.media/tree/master/src/Examples%20(dotnetcore)) |
[Gallery](https://github.com/aardvarkplatform/aardvark.docs/wiki/Gallery) | 
[Quickstart](https://github.com/aardvarkplatform/aardvark.docs/wiki/Quickstart-Windows) | 
[Status](https://github.com/aardvarkplatform/aardvark.docs/wiki/Status)

Aardvark.Media is part of the open-source [Aardvark platform](https://github.com/aardvark-platform/aardvark.docs/wiki) for visual computing, real-time graphics and visualization.

3D graphics, user interfaces and complex interactions on top are challenging and time consuming in classical programming models. High-level abstraction, immutable data and functional programming concepts on the other hand boost productivity. aardvark.media brings together high-performance applications and purely functional application programming.
The ELM architecture has become popular in web-development and for user interfaces (e.g. [ELM](https://elm-lang.org/), [Fabulous](https://fsprojects.github.io/Fabulous/), [Elmish](https://elmish.github.io/elmish/),...) and makes declarative, reliable UI and app development easier.
aardvark.media follows the same concept but has some distinct features: 
 - Just like buttons in elm or elmish, aardvark.media supports interactions with objects in 3D renderings. Rendering is done by the higly efficient [aardvark.rendering](https://github.com/aardvark-platform/aardvark.rendering) engine.
 - Other systems execute the view function after each modification to the model. In aardvark.media the changes are computed on the model (which is typicall much smaller than the generated UI) itself. [aardvark.base](https://github.com/aardvark-platform/aardvark.base)'s *incremental system* takes care of reexecuting specific parts of the view function. This takes the burdon of diffing from the HTML engine, but more importantly allows to incrementally update optimized scene representations for 3D rendering. The rational behind this is that the model is typicall much smaller than the generated UI, or more severly much easier to diff than 3D geometry.
 - aardvark.media currently runs on netcore and serves a website using [suave](https://suave.io/). The rendering part is done in *fullblown OpenGL/Vulkan* in the aardvark rendering engine. Applications typcially use the [electron](https://electronjs.org/) based [aardium](https://github.com/aardvark-community/aardium) for client side application deployment or full server side application deployment. We work on client-side deployment using [fable](https://fable.io/) as well, but aardvark.media's server side approach has also advantages and particular usecases.

More info can be found in the [Platform Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki), and more particularly [here](https://github.com/aardvark-platform/aardvark.docs/wiki/Learning-Aardvark.Media-%231), [here](https://github.com/aardvark-platform/aardvark.docs/wiki/Learning-Aardvark.Media-%232), and [here](https://github.com/aardvark-platform/aardvark.docs/wiki/Learning-Aardvark.Media-%233).
