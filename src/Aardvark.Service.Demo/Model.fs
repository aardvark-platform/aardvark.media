namespace Demo.TestApp

open Aardvark.Base
open Aardvark.Base.Incremental

type ClientLocalAttribute() = inherit System.Attribute()

[<DomainType>]
type Model =
    {
        lastName    : Option<string>
        elements    : plist<string>
        hasD3Hate   : bool
        boxScale    : float
        boxHovered  : bool
        dragging    : bool
    }





[<DomainType>]
type CameraControllerState =
    {
        view : CameraView

        dragStart : V2i
        look : bool
        zoom : bool
        pan : bool

    }

type CameraControllerStateThing =
    {
        cview : CameraView

        cdragStart : Map<int, V2i>
        clook : Map<int, bool>
        czoom : Map<int, bool>
        cpan : Map<int, bool>

    }

    member x.ClientState (i : int, initial : CameraControllerState) =
        {
            view = x.cview
            dragStart = Map.tryFind i x.cdragStart |> Option.defaultValue initial.dragStart
            look = Map.tryFind i x.clook |> Option.defaultValue initial.look
            zoom = Map.tryFind i x.czoom |> Option.defaultValue initial.zoom
            pan = Map.tryFind i x.cpan |> Option.defaultValue initial.pan
        }

    member x.WithClientState (i : int, state : CameraControllerState) =
        {
            cview = state.view
            cdragStart = Map.add i state.dragStart x.cdragStart
            clook = Map.add i state.look x.clook
            czoom = Map.add i state.zoom x.czoom
            cpan = Map.add i state.pan x.cpan
        }
        

type MCameraControllerStateThing(initial) =
    member x.cview : IMod<CameraView> = failwith ""
