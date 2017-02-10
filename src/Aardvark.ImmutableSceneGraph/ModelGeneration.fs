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
        //frustum : Frustum 
        lookingAround : Option<PixelPosition>
        panning : Option<PixelPosition>
        zooming : Option<PixelPosition>
        picking : Option<int>
        center : Option<V3d>
        navigationMode : NavigationMode

        forward : V2d
        forwardSpeed : float
    }

module ComposedTest =
    
    open Scratch.DomainTypes

    type InteractionMode = ExplorePick | MeasurePick | Disabled

    [<DomainType>]
    type Model = {
        ViewerState      : CameraTest.Model
        Drawing          : SimpleDrawingApp.Model
        InteractionState : InteractionMode
    }