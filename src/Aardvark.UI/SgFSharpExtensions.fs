namespace Aardvark.UI


open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

open Aardvark.UI

[<AutoOpen>]
module Extensions = 
    module Sg =
       let translate' (t : aval<V3d>) sg = 
            Sg.trafo (t |> AVal.map Trafo3d.Translation) sg

       let sphere level color radius = Sg.sphere level color radius |> Sg.noEvents
       let box color box = Sg.box color box |> Sg.noEvents
       let empty<'msg> : ISg<'msg> = Sg.ofList [] |> Sg.noEvents