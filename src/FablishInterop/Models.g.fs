namespace Scratch.DomainTypes

[<AutoOpen>]
module Generated = 
    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    module TranslateController = 
        type Axis = 
            | X
            | Y
            | Z
        
        [<DomainType>]
        type Model = 
            { mutable _id : Id
              hovered : Option<Axis>
              activeTranslation : Option<Plane3d * V3d>
              trafo : Trafo3d }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mhovered = Mod.init (x.hovered)
                  mactiveTranslation = Mod.init (x.activeTranslation)
                  mtrafo = Mod.init (x.trafo) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mhovered : ModRef<Option<Axis>>
              mactiveTranslation : ModRef<Option<Plane3d * V3d>>
              mtrafo : ModRef<Trafo3d> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mhovered.Value <- arg0.hovered
                    x.mactiveTranslation.Value <- arg0.activeTranslation
                    x.mtrafo.Value <- arg0.trafo
        
        [<DomainType>]
        type Scene = 
            { mutable _id : Id
              camera : Camera
              scene : Model }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mcamera = Mod.init (x.camera)
                  mscene = x.scene.ToMod(reuseCache) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MScene = 
            { mutable _original : Scene
              mcamera : ModRef<Camera>
              mscene : MModel }
            member x.Apply(arg0 : Scene, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mcamera.Value <- arg0.camera
                    x.mscene.Apply(arg0.scene, reuseCache)
    
    module SimpleDrawingApp = 
        type Polygon = list<V3d>
        
        type OpenPolygon = 
            { cursor : Option<V3d>
              finishedPoints : list<V3d> }
        
        [<DomainType>]
        type Model = 
            { mutable _id : Id
              finished : list<Polygon>
              working : Option<OpenPolygon> }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mfinished = Mod.init (x.finished)
                  mworking = Mod.init (x.working) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mfinished : ModRef<list<Polygon>>
              mworking : ModRef<Option<OpenPolygon>> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mfinished.Value <- arg0.finished
                    x.mworking.Value <- arg0.working
    
    module PlaceTransformObjects = 
        [<DomainType>]
        type Model = 
            { mutable _id : Id
              objects : list<Trafo3d>
              hoveredObj : Option<int>
              selectedObj : Option<int * TranslateController.Model> }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mobjects = Mod.init (x.objects)
                  mhoveredObj = Mod.init (x.hoveredObj)
                  mselectedObj = Mod.init (x.selectedObj) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mobjects : ModRef<list<Trafo3d>>
              mhoveredObj : ModRef<Option<int>>
              mselectedObj : ModRef<Option<int * TranslateController.Model>> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mobjects.Value <- arg0.objects
                    x.mhoveredObj.Value <- arg0.hoveredObj
                    x.mselectedObj.Value <- arg0.selectedObj
    
    module Interop = 
        type Active = 
            | RenderControl
            | Gui
        
        type Scene = 
            { camera : Camera
              obj : V3d }
        
        type Model = 
            { currentlyActive : Active
              scene : Scene }
