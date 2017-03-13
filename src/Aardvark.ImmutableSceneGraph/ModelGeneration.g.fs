namespace Scratch.DomainTypes2

[<AutoOpen>]
module Generated = 
    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
       
    module ComposedTest = 
        open Aardvark.Base
        open Aardvark.Base.Rendering

        open Scratch.DomainTypes

        type InteractionMode = TrafoPick | ExplorePick | MeasurePick | Disabled

        [<DomainType>]
        type Model = 
            { mutable _id : Id
              ViewerState      : Camera.Model
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
              mViewerState : Camera.MModel
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
                   
