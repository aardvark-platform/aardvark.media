namespace Scratch.DomainTypes2

open System
open Aardvark.Base
open Aardvark.Base.Incremental

module ComposedTest =
    
    open Scratch.DomainTypes

    type InteractionMode = ExplorePick | MeasurePick | Disabled

    [<DomainType>]
    type Model = {
        ViewerState      : Camera.Model
        Drawing          : SimpleDrawingApp.Model
        InteractionState : InteractionMode
    }