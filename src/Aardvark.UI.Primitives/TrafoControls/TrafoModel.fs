namespace Aardvark.UI.Trafos

open FSharp.Data.Adaptive
open Aardvark.Base
open Adaptify

type Axis = X | Y | Z

type PickPoint =
    {
        offset : float
        axis   : Axis
        hit : V3d
    }

type TrafoKind =
  | Translate = 0
  | Rotate    = 1
  | Scale     = 2


type TrafoMode =
    | Local  = 0
    | Global = 1

type Pose =
    {
        position : V3d
        rotation : Rot3d
        scale    : V3d
    } with
        member x.Trafo = 
            let rot = Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse)
            Trafo3d.Scale x.scale * rot *  Trafo3d.Translation x.position

[<ModelType>]
type Transformation = { 
    workingPose   : Pose
    pose          : Pose
    previewTrafo  : Trafo3d
    scale         : float

    mode          : TrafoMode
    //pivotLocation    : V3d
    hovered       : Option<Axis>
    grabbed       : Option<PickPoint>
} 
