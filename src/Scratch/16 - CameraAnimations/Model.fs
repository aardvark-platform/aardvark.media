namespace CameraAnimations

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open Aardvark.UI.Animation

type Message = 
  | Camera           of FreeFlyController.Message
  | AnimationMessage of AnimationAction
  | KeyUp            of key : Aardvark.Application.Keys
  | KeyDown          of key : Aardvark.Application.Keys
  | UpdateDockConfig of DockConfig
  | FlyTo            
  | Reset

[<DomainType>]
type Model = 
  {
      cameraState          : CameraControllerState          
      threads              : ThreadPool<Message>
      dockConfig           : DockConfig
      animations           : AnimationModel
  }

module Model =
  let initialCamera = { FreeFlyController.initial with view = CameraView.lookAt (V3d.III * 10000.0) V3d.OOO V3d.OOI; }

module AnimationModel =
  let initial = 
    { 
      animations = PList.empty
      animation  = Animate.On
      cam        = CameraController.initial.view
    }


module Lenses = 
  let get    (lens : Lens<'s,'a>) (s:'s) : 'a              = lens.Get(s)
  let set    (lens : Lens<'s,'a>) (v : 'a) (s:'s) : 's     = lens.Set(s,v)
  let set'   (lens : Lens<'s,'a>) (s:'s) (v : 'a)  : 's    = lens.Set(s,v)
  let update (lens : Lens<'s,'a>) (f : 'a->'a) (s:'s) : 's = lens.Update(s,f)