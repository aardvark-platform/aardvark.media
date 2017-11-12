namespace DragNDrop

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI
open Aardvark.UI.Primitives

type Drag = { PickPoint : V3d; Offset : V3d }

[<DomainType>]
type Model = { 
    trafo       : Trafo3d 
    dragging    : Option<Drag>
    camera      : CameraControllerState
}

type Axis = X | Y | Z

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Axis = 
    let toV3d axis =
      match axis with 
        | X -> V3d.XAxis
        | Y -> V3d.YAxis
        | Z -> V3d.ZAxis

    let toCircle r axis =        
        Circle3d(V3d.Zero, (axis |> toV3d), r)

type PickPoint =
    {
        offset : float
        axis   : Axis
        hit : V3d
    }


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

     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]      
module Pose =
    let identity = { position = V3d.Zero; rotation = Rot3d.Identity; scale = V3d.III }
    let inline trafo (p : Pose) = p.Trafo
    let translate p = { position = p; rotation = Rot3d.Identity; scale = V3d.III }
    let toRotTrafo x = 
        Trafo3d(Rot3d.op_Explicit x.rotation, Rot3d.op_Explicit x.rotation.Inverse)
    let toTrafo x = 
        let rot = x |> toRotTrafo
        Trafo3d.Scale x.scale * rot * Trafo3d.Translation x.position
    let trafoWoScale x = 
        (x |> toRotTrafo) * Trafo3d.Translation x.position
    let toTranslateTrafo x =
        Trafo3d.Translation x.position

    let transform (p : Pose) (t : Trafo3d) = 
        let newRot = Rot3d.FromFrame(t.Forward.C0.XYZ,t.Forward.C1.XYZ,t.Forward.C2.XYZ)
        { position = t.Forward.TransformPosProj p.position; rotation = newRot; scale = t.Forward.TransformPos V3d.III } // wrong



[<DomainType>]
type Transformation = { 
    workingPose   : Pose
    pose          : Pose
    previewTrafo  : Trafo3d

    mode          : TrafoMode
    //pivotLocation    : V3d
    hovered       : Option<Axis>
    grabbed       : Option<PickPoint>
} 

[<DomainType>]
type Scene = {
    transformation : Transformation
    camera         : CameraControllerState
}