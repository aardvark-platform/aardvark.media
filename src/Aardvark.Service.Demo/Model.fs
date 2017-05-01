namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Mutable
open Aardvark.UI
open FShade.Primitives
open Aardvark.Application

type ClientLocalAttribute() = inherit System.Attribute()

[<DomainType>]
type Urdar = { urdar : int }

[<DomainType>]
type Model =
    {
        boxHovered      : bool
        dragging        : bool
        lastName        : Option<string>
        elements        : plist<string>
        hasD3Hate       : bool
        boxScale        : float
        objects         : hmap<string,Urdar>
        lastTime        : MicroTime
    }



type CameraControllerMessage = 
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Move of V2i
        | StepTime
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Blur

[<DomainType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        look : bool
        zoom : bool
        pan : bool

        forward : bool
        backward : bool
        left : bool
        right : bool
        moveVec : V3i
        orbitCenter : Option<V3d>
        lastTime : Option<float>

        [<TreatAsValue>]
        stash : Option<CameraControllerState> 
    }

