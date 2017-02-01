namespace Scratch.DomainTypes2

open System
open Aardvark.Base
open Aardvark.Base.Incremental

module CameraTest =
    
    open Aardvark.Base
    open Aardvark.Base.Rendering


    [<DomainType>]
    type Model = { 
        camera : CameraView
        frustum : Frustum 
        lookingAround : Option<PixelPosition>
        center : Option<V3d>

        forward : V2d
        forwardSpeed : float
    }