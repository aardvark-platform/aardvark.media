namespace Scratch.DomainTypes2

open System
open Aardvark.Base
open Aardvark.Base.Incremental

module CameraTest =
    
    open Aardvark.Base
    open Aardvark.Base.Rendering
    
    type NavigationMode = FreeFly | Orbital

    [<DomainType>]
    type Model = { 
        camera : CameraView
        frustum : Frustum 
        lookingAround : Option<PixelPosition>
        center : Option<V3d>
        navigationMode : NavigationMode

        forward : V2d
        forwardSpeed : float
    }