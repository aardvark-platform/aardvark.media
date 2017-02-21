namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application


open Aardvark.ImmutableSceneGraph
open Aardvark.Elmish
open Primitives

module Models =

    open Aardvark.Base
    open Aardvark.Base.Incremental
    
    type Model = 
        { fileName : string
          bounds : Box3d }
    
    [<DomainType>]
    type Object = 
        { mutable _id : Id
          name : string
          trafo : Trafo3d
          model : Model }
        
        member x.ToMod(reuseCache : ReuseCache) = 
            { _original = x
              mname = Mod.init (x.name)
              mtrafo = Mod.init (x.trafo)
              mmodel = Mod.init (x.model) }
        
        interface IUnique with
            
            member x.Id 
                with get () = x._id
                and set v = x._id <- v
    
    and [<DomainType>] MObject = 
        { mutable _original : Object
          mname : ModRef<string>
          mtrafo : ModRef<Trafo3d>
          mmodel : ModRef<Model> }
        member x.Apply(arg0 : Object, reuseCache : ReuseCache) = 
            if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                x._original <- arg0
                x.mname.Value <- arg0.name
                x.mtrafo.Value <- arg0.trafo
                x.mmodel.Value <- arg0.model
    
    [<DomainType>]
    type State = 
        { mutable _id : Id
          primary : Object
          viewTrafo : Trafo3d
          objects : pset<Object>
          test : array<Object> }
        
        member x.ToMod(reuseCache : ReuseCache) = 
            { _original = x
              mprimary = x.primary.ToMod(reuseCache)
              mviewTrafo = Mod.init (x.viewTrafo)
              mobjects = 
                  MapSet
                      ((reuseCache.GetCache()), x.objects, 
                       (fun (a : Object) -> a.ToMod(reuseCache)), 
                       (fun (m : MObject, a : Object) -> m.Apply(a, reuseCache)))
              mtest = Mod.init (x.test) }
        
        interface IUnique with
            
            member x.Id 
                with get () = x._id
                and set v = x._id <- v
    
    and [<DomainType>] MState = 
        { mutable _original : State
          mprimary : MObject
          mviewTrafo : ModRef<Trafo3d>
          mobjects : MapSet<Object, MObject>
          mtest : ModRef<array<Object>> }
        member x.Apply(arg0 : State, reuseCache : ReuseCache) = 
            if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                x._original <- arg0
                x.mprimary.Apply(arg0.primary, reuseCache)
                x.mviewTrafo.Value <- arg0.viewTrafo
                x.mobjects.Update(arg0.objects)
                x.mtest.Value <- arg0.test