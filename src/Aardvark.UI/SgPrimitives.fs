namespace Aardvark.UI


open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

open Aardvark.UI

[<AutoOpen>]
module SgPrimitives =
    module Sg =

        // creates a quad on the z-Plane, ranging -1,-1,0 to 1,1,0
        let quad<'msg> : ISg<'msg> =
            Sg.quad |> Sg.noEvents

        let fullScreenQuad<'msg> : ISg<'msg> =
            Sg.fullScreenQuad |> Sg.noEvents

        let farPlaneQuad<'msg> : ISg<'msg> =
            Sg.farPlaneQuad |> Sg.noEvents

        let coordinateCross (size : aval<float>) =
            Sg.coordinateCross size |> Sg.noEvents

        let coordinateCross' (size : float) =
            Sg.coordinateCross' size |> Sg.noEvents

        let box (color : aval<C4b>) (bounds : aval<Box3d>) =
            Sg.box color bounds |> Sg.noEvents

        let box' (color : C4b) (bounds : Box3d) =
            Sg.box' color bounds |> Sg.noEvents

        let wireBox (color : aval<C4b>) (bounds : aval<Box3d>) =
            Sg.wireBox color bounds |> Sg.noEvents

        let wireBox' (color : C4b) (bounds : Box3d) =
            Sg.wireBox' color bounds |> Sg.noEvents

        let frustum (color : aval<C4b>) (view : aval<CameraView>) (proj : aval<Frustum>) =
            Sg.frustum color view proj |> Sg.noEvents

        let lines (color : aval<C4b>) (lines : aval<Line3d[]>) =
            Sg.lines color lines |> Sg.noEvents

        let lines' (color : C4b) (lines : Line3d[]) =
            Sg.lines' color lines |> Sg.noEvents

        let triangles (color : aval<C4b>) (triangles : aval<Triangle3d[]>) =
            Sg.triangles color triangles |> Sg.noEvents

        let triangles' (color : C4b) (triangles : Triangle3d[]) =
            Sg.triangles' color triangles |> Sg.noEvents

        /// creates a subdivision sphere, where level is the subdivision level
        let unitSphere (level : int) (color : aval<C4b>) =
            Sg.unitSphere level color |> Sg.noEvents

        /// creates a subdivision sphere, where level is the subdivision level
        let unitSphere' (level : int) (color : C4b) =
            Sg.unitSphere' level color |> Sg.noEvents

        /// creates a subdivision sphere, where level is the subdivision level
        let sphere (level : int) (color : aval<C4b>) (radius : aval<float>) =
            Sg.sphere level color radius |> Sg.noEvents

        /// creates a subdivision sphere, where level is the subdivision level
        let sphere' (level : int) (color : C4b) (radius : float) =
            Sg.sphere' level color radius |> Sg.noEvents

        let cylinder (tess : int) (color : aval<C4b>) (radius : aval<float>) (height : aval<float>) =
            Sg.cylinder tess color radius height |> Sg.noEvents

        let cylinder' (tess : int) (color : C4b) (radius : float) (height : float) =
            Sg.cylinder' tess color radius height |> Sg.noEvents

        let cone (tess : int) (color : aval<C4b>) (radius : aval<float>) (height : aval<float>) =
            Sg.cone tess color radius height |> Sg.noEvents

        let cone' (tess : int) (color : C4b) (radius : float) (height : float) =
            Sg.cone' tess color radius height |> Sg.noEvents