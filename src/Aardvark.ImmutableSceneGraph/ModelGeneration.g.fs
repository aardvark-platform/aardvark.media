namespace Scratch.DomainTypes2

[<AutoOpen>]
module Generated = 
    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    module CameraTest = 
        open Aardvark.Base
        open Aardvark.Base.Rendering
        
        type NavigationMode = FreeFly | Orbital

        [<DomainType>]
        type Model = 
            { mutable _id : Id
              camera : CameraView
              //frustum : Frustum
              lookingAround : Option<PixelPosition>
              panning : Option<PixelPosition>
              zooming : Option<PixelPosition>
              picking : Option<int>
              center : Option<V3d>
              navigationMode : NavigationMode
              forward : V2d
              forwardSpeed : float }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mcamera = Mod.init (x.camera)
                  //mfrustum = Mod.init (x.frustum)
                  mlookingAround = Mod.init (x.lookingAround)
                  mpanning = Mod.init (x.panning)
                  mzooming = Mod.init (x.zooming)
                  mpicking = Mod.init (x.picking)
                  mcenter = Mod.init (x.center)
                  mnavigationMode = Mod.init (x.navigationMode)
                  mforward = Mod.init (x.forward)
                  mforwardSpeed = Mod.init (x.forwardSpeed) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mcamera : ModRef<CameraView>
              //mfrustum : ModRef<Frustum>
              mlookingAround : ModRef<Option<PixelPosition>>
              mpanning : ModRef<Option<PixelPosition>>
              mzooming : ModRef<Option<PixelPosition>>
              mpicking : ModRef<Option<int>>
              mcenter : ModRef<Option<V3d>>
              mnavigationMode : ModRef<NavigationMode>
              mforward : ModRef<V2d>
              mforwardSpeed : ModRef<float> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mcamera.Value <- arg0.camera
                    //x.mfrustum.Value <- arg0.frustum
                    x.mlookingAround.Value <- arg0.lookingAround
                    x.mpanning.Value <- arg0.panning
                    x.mzooming.Value <- arg0.zooming
                    x.mcenter.Value <- arg0.center
                    x.mnavigationMode.Value <- arg0.navigationMode
                    x.mforward.Value <- arg0.forward
                    x.mforwardSpeed.Value <- arg0.forwardSpeed

    module ComposedTest = 
        open Aardvark.Base
        open Aardvark.Base.Rendering

        open Scratch.DomainTypes

        type InteractionMode = TrafoPick | ExplorePick | MeasurePick | Disabled

        [<DomainType>]
        type Model = 
            { mutable _id : Id
              ViewerState      : CameraTest.Model
              Translation      : TranslateController.TModel
              Drawing          : SimpleDrawingApp.Model
              InteractionState : InteractionMode
              }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mViewerState = x.ViewerState.ToMod reuseCache
                  mTranslation = x.Translation.ToMod reuseCache
                  mDrawing = x.Drawing.ToMod reuseCache
                  mInteractionState = Mod.init (x.InteractionState)
                  }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mViewerState : CameraTest.MModel
              mTranslation : TranslateController.MTModel
              mDrawing : SimpleDrawingApp.MModel
              mInteractionState : ModRef<InteractionMode>
              }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mViewerState.Apply (arg0.ViewerState, reuseCache)
                    x.mTranslation.Apply (arg0.Translation, reuseCache)
                    x.mDrawing.Apply (arg0.Drawing, reuseCache)
                    x.mInteractionState.Value <- arg0.InteractionState
                   
