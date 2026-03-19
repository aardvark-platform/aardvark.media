namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive

type ISg<'msg> =
    inherit ISg

type IApplicator<'msg> =
    inherit ISg<'msg>
    abstract member Child : ISg<'msg>

type IGroup<'msg> =
    inherit ISg<'msg>
    abstract member Children : aset<ISg<'msg>>

type SceneEventKind =
    | Enter
    | Leave
    | Move
    | Click
    | DoubleClick
    | Down
    | Up

type SceneEvent =
    {
        evtKind     : SceneEventKind
        evtRay      : RayPart
        evtPixel    : V2i
        evtView     : Trafo3d
        evtProj     : Trafo3d
        evtViewport : V2i
        evtButtons  : MouseButtons
        evtAlt      : bool
        evtShift    : bool
        evtCtrl     : bool
        evtTrafo    : aval<Trafo3d>
    }

    member inline this.kind      = this.evtKind
    member inline this.localRay  = this.evtRay.Transformed(this.evtTrafo.GetValue().Backward)
    member inline this.globalRay = this.evtRay
    member inline this.buttons   = this.evtButtons

type SceneHit =
    {
        event : SceneEvent
        rayT  : float
    }

    member inline this.kind           = this.event.kind
    member inline this.localRay       = this.event.localRay
    member inline this.globalRay      = this.event.globalRay
    member inline this.buttons        = this.event.buttons
    member inline this.globalPosition = this.globalRay.Ray.Ray.GetPointOnRay this.rayT
    member inline this.localPosition  = this.localRay.Ray.Ray.GetPointOnRay this.rayT

module Sg =

    type AbstractApplicator<'msg>(child : ISg<'msg>) =
        interface IApplicator with
            member x.Child = child |> unbox |> AVal.constant

        interface IApplicator<'msg> with
            member x.Child = child

        member x.Child = child

    type MapApplicator<'inner, 'outer>(mapping : 'inner -> seq<'outer>, child : ISg<'inner>) =
        interface IApplicator with
            member x.Child = child |> unbox |> AVal.constant

        interface ISg<'outer>

        member x.Child = child
        member x.Mapping = mapping

    type EventApplicator<'msg>(events : amap<SceneEventKind, SceneHit -> bool * seq<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type GlobalEvent<'msg>(events : amap<SceneEventKind, SceneEvent -> seq<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type Adapter<'msg>(inner : ISg) =
        inherit Aardvark.SceneGraph.Sg.AbstractApplicator(inner)
        interface ISg<'msg>

    type Set<'msg>(children : aset<ISg<'msg>>) =
        interface ISg
        interface IGroup<'msg> with
            member x.Children = children