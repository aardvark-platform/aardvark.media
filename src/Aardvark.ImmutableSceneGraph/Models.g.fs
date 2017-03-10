namespace Scratch.DomainTypes

[<AutoOpen>]
module Generated = 
    open System
    open Fablish
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
              activeTranslation : Option<Axis * Plane3d * V3d>
              trafo : Trafo3d
              editTrafo : Trafo3d }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mhovered = Mod.init (x.hovered)
                  mactiveTranslation = Mod.init (x.activeTranslation)
                  mtrafo = Mod.init (x.trafo)
                  meditTrafo = Mod.init (x.editTrafo) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MTModel = 
            { mutable _original : TModel
              mhovered : ModRef<Option<Axis>>
              mactiveTranslation : ModRef<Option<Axis * Plane3d * V3d>>
              mtrafo : ModRef<Trafo3d>
              meditTrafo : ModRef<Trafo3d> }
            member x.Apply(arg0 : TModel, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mhovered.Value <- arg0.hovered
                    x.mactiveTranslation.Value <- arg0.activeTranslation
                    x.mtrafo.Value <- arg0.trafo
                    x.meditTrafo.Value <- arg0.editTrafo
        
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
    
    module RotateController = 
        [<DomainType>]
        type RModel = 
            { mutable _id : Id
              hovered : Option<Axis>
              activeRotation : Option<Axis * Plane3d * V3d>
              trafo : Trafo3d
              editTrafo : Trafo3d }
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
                  mhovered = Mod.init (x.hovered)
                  mactiveRotation = Mod.init (x.activeRotation)
                  mtrafo = Mod.init (x.trafo)
                  meditTrafo = Mod.init (x.editTrafo) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MRModel = 
            { mutable _original : RModel
              mhovered : ModRef<Option<Axis>>
              mactiveRotation : ModRef<Option<Axis * Plane3d * V3d>>
              mtrafo : ModRef<Trafo3d>
              meditTrafo : ModRef<Trafo3d> }
            member x.Apply(arg0 : RModel, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mhovered.Value <- arg0.hovered
                    x.mactiveRotation.Value <- arg0.activeRotation
                    x.mtrafo.Value <- arg0.trafo
                    x.meditTrafo.Value <- arg0.editTrafo
        
        [<DomainType>]
        type Scene = 
            { mutable _id : Id
              camera : Camera
              scene : RModel }
            
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
              mscene : MRModel }
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

    module DrawingApp = 
        type Polygon = list<V3d>

        type Style = {
            color : C4b
            thickness : Numeric.Model
        }
        
        type Annotation = {
            seqNumber : int
            annType : string
            geometry : Polygon // think of record type
            style : Style            
        }

        type OpenPolygon = 
            { cursor : Option<V3d>
              finishedPoints : list<V3d>
               }
        


        [<DomainType>]
        type Drawing = 
            { mutable _id : Id
              history  : EqualOf<Option<Drawing>>
              future   : EqualOf<Option<Drawing>>
              picking  : Option<int>
              filename : string
              finished : pset<Annotation>
              working  : Option<OpenPolygon>
              style    : Style
              measureType : Choice.Model
              styleType   : Choice.Model
              selected : pset<int>
              selectedAnn : Option<Annotation>}
            
            member x.ToMod(reuseCache : ReuseCache) = 
                { _original = x
              //    mhistory = Mod.init (x.history)
                  mpicking = Mod.init(x.picking)
                  mfilename = Mod.init(x.filename)
                  mfinished = ResetSet(x.finished)
                  mworking = Mod.init (x.working)
                  mstyle = Mod.init(x.style)
                  mmeasureType = Mod.init(x.measureType)
                  mstyleType = Mod.init(x.styleType) 
                  mselected = ResetSet(x.selected)}
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MDrawing = 
            { mutable _original : Drawing
     //         mhistory : ModRef<List<Drawing>>
              mfilename : ModRef<string>
              mpicking : ModRef<Option<int>>
              mfinished : ResetSet<Annotation>
              mworking : ModRef<Option<OpenPolygon>>
              mstyle : ModRef<Style>
              mmeasureType : ModRef<Choice.Model>
              mstyleType : ModRef<Choice.Model> 
              mselected : ResetSet<int> }
            member x.Apply(arg0 : Drawing, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
               //     x.mhistory.Value <- arg0.history
                    x.mpicking.Value <- arg0.picking
                    x.mfinished.Update(arg0.finished)
                    x.mworking.Value <- arg0.working
                    x.mstyle.Value <- arg0.style
                    x.mmeasureType.Value <- arg0.measureType
                    x.mstyleType.Value <- arg0.styleType
                    x.mselected.Update(arg0.selected)
    
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


//            type MyOption<'a> = 
//                | MyNone
//                | MySome of 'a 
//
//            type PMyOption<'a> = { mutable _id : Id; value : MyOption<'a> } with
//                member x.ToMod(cache) =
//                    {
//                        _original = x
//                        mvalue = Mod.init x
//                    }
//
//            and MMyOption<'a> = { mutable _original : PMyOption<'a>; mvalue : ModRef<PMyOption<'a>> } with
//                member x.Apply(arg0 : PMyOption<'a>, cache) =
//                    if not (System.Object.ReferenceEquals(arg0, x._original)) then 
//                        x._original <- arg0
//                        match x.mvalue.Value.value,arg0.value with
//                            | MyNone, MyNone -> ()
//                            | MyNone, MySome v -> x.mvalue.Value <- { _id = null; value = MySome <| v }
//                            | MySome _, MySome n -> failwith "apply etc"
                                   
        
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
                  mselectedObj = Mod.init (x.selectedObj |> Option.map (fun o -> o.ToMod(reuseCache))) }
            
            interface IUnique with
                
                member x.Id 
                    with get () = x._id
                    and set v = x._id <- v
        
        and [<DomainType>] MModel = 
            { mutable _original : Model
              mobjects : MapSet<Object, MObject>
              mhoveredObj : ModRef<Option<int>>
              mselectedObj : ModRef<Option<MSelected>> }
            member x.Apply(arg0 : Model, reuseCache : ReuseCache) = 
                if not (System.Object.ReferenceEquals(arg0, x._original)) then 
                    x._original <- arg0
                    x.mobjects.Update(arg0.objects)
                    let content = x.mobjects :> aset<_>
                    for (m,i) in List.zip (content |> ASet.toList) (arg0.objects |> PSet.toList) do
                        m.Apply(i,reuseCache)
                    x.mhoveredObj.Value <- arg0.hoveredObj
                    match x.mselectedObj.Value, arg0.selectedObj with
                        | None, None -> ()
                        | Some v, None -> x.mselectedObj.Value <- None
                        | Some o, Some n -> o.Apply(n,reuseCache)
                        | None, Some v -> x.mselectedObj.Value <- Some <| v.ToMod(reuseCache)

    
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
