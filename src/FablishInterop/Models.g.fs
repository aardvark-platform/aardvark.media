namespace Scratch.DomainTypes

[<AutoOpen>]
module Generated = 
    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    type Axis = 
        | X
        | Y
        | Z
    
    module TranslateController = 
        [<DomainType>]
        type TModel = 
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
        
        and [<DomainType>] MTModel = 
            { mutable _original : TModel
              mhovered : ModRef<Option<Axis>>
              mactiveTranslation : ModRef<Option<Plane3d * V3d>>
              mtrafo : ModRef<Trafo3d> }
            member x.Apply(arg0 : TModel, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mhovered.Value <- arg0.hovered
                    x.mactiveTranslation.Value <- arg0.activeTranslation
                    x.mtrafo.Value <- arg0.trafo
        
        [<DomainType>]
        type Scene = 
            { mutable _id : Id
              camera : Camera
              scene : TModel }
            
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
              mscene : MTModel }
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
              finished : pset<Polygon>
              working : Option<OpenPolygon> }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mfinished = ResetSet(x.finished)
                  mworking = Mod.init (x.working) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mfinished : ResetSet<Polygon>
              mworking : ModRef<Option<OpenPolygon>> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mfinished.Update(arg0.finished)
                    x.mworking.Value <- arg0.working
    
    module PlaceTransformObjects = 
        open TranslateController
        
        [<DomainType>]
        type Selected = 
            { mutable _id : Id
              id : int
              tmodel : TModel }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mid = Mod.init (x.id)
                  mtmodel = x.tmodel.ToMod(reuseCache) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MSelected = 
            { mutable _original : Selected
              mid : ModRef<int>
              mtmodel : MTModel }
            member x.Apply(arg0 : Selected, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mid.Value <- arg0.id
                    x.mtmodel.Apply(arg0.tmodel, reuseCache)
        
        [<DomainType>]
        type Object = 
            { mutable _id : Id
              id : int
              t : Trafo3d }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mid = Mod.init (x.id)
                  mt = Mod.init (x.t) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MObject = 
            { mutable _original : Object
              mid : ModRef<int>
              mt : ModRef<Trafo3d> }
            member x.Apply(arg0 : Object, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mid.Value <- arg0.id
                    x.mt.Value <- arg0.t
        
        [<DomainType>]
        type Model = 
            { mutable _id : Id
              objects : pset<Object>
              hoveredObj : Option<int>
              selectedObj : Option<Selected> }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mobjects = 
                      MapSet
                          ((reuseCache.GetCache()), x.objects, 
                           (fun (a : Object) -> a.ToMod(reuseCache)), 
                           (fun (m : MObject, a : Object) -> 
                           m.Apply(a, reuseCache)))
                  mhoveredObj = Mod.init (x.hoveredObj)
                  mselectedObj = Mod.init (x.selectedObj) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mobjects : MapSet<Object, MObject>
              mhoveredObj : ModRef<Option<int>>
              mselectedObj : ModRef<Option<Selected>> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mobjects.Update(arg0.objects)
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
    
    module SharedModel = 
        open TranslateController
        
        [<DomainType>]
        type Ui = 
            { mutable _id : Id
              cnt : int
              info : string }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mcnt = Mod.init (x.cnt)
                  minfo = Mod.init (x.info) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MUi = 
            { mutable _original : Ui
              mcnt : ModRef<int>
              minfo : ModRef<string> }
            member x.Apply(arg0 : Ui, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mcnt.Value <- arg0.cnt
                    x.minfo.Value <- arg0.info
        
        [<DomainType>]
        type Model = 
            { mutable _id : Id
              ui : Ui
              scene : Scene }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mui = x.ui.ToMod(reuseCache)
                  mscene = x.scene.ToMod(reuseCache) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mui : MUi
              mscene : MScene }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mui.Apply(arg0.ui, reuseCache)
                    x.mscene.Apply(arg0.scene, reuseCache)
