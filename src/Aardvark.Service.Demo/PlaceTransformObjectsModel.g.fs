namespace PlaceTransformObjects

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open PlaceTransformObjects

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MObject(__initial : PlaceTransformObjects.Object) = 
        let mutable __current = __initial
        let _name = ResetMod.Create(__initial.name)
        let _objectType = ResetMod.Create(__initial.objectType)
        let _transformation = DragNDrop.Mutable.MTransformation.Create(__initial.transformation)
        
        member x.name = _name :> IMod<_>
        member x.objectType = _objectType :> IMod<_>
        member x.transformation = _transformation
        
        member x.Update(v : PlaceTransformObjects.Object) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_name,v.name)
                ResetMod.Update(_objectType,v.objectType)
                DragNDrop.Mutable.MTransformation.Update(_transformation, v.transformation)
        
        static member Create(v : PlaceTransformObjects.Object) = MObject(v)
        static member Update(m : MObject, v : PlaceTransformObjects.Object) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Object =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let name =
                { new Lens<PlaceTransformObjects.Object, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.name
                    override x.Set(r,v) = { r with name = v }
                    override x.Update(r,f) = { r with name = f r.name }
                }
            let objectType =
                { new Lens<PlaceTransformObjects.Object, PlaceTransformObjects.ObjectType>() with
                    override x.Get(r) = r.objectType
                    override x.Set(r,v) = { r with objectType = v }
                    override x.Update(r,f) = { r with objectType = f r.objectType }
                }
            let transformation =
                { new Lens<PlaceTransformObjects.Object, DragNDrop.Transformation>() with
                    override x.Get(r) = r.transformation
                    override x.Set(r,v) = { r with transformation = v }
                    override x.Update(r,f) = { r with transformation = f r.transformation }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MWorld(__initial : PlaceTransformObjects.World) = 
        let mutable __current = __initial
        let _objects = MMap.Create(__initial.objects, (fun v -> MObject.Create(v)), (fun (m,v) -> MObject.Update(m, v)), (fun v -> v))
        let _selectedObjects = MSet.Create(__initial.selectedObjects)
        
        member x.objects = _objects :> amap<_,_>
        member x.selectedObjects = _selectedObjects :> aset<_>
        
        member x.Update(v : PlaceTransformObjects.World) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MMap.Update(_objects, v.objects)
                MSet.Update(_selectedObjects, v.selectedObjects)
        
        static member Create(v : PlaceTransformObjects.World) = MWorld(v)
        static member Update(m : MWorld, v : PlaceTransformObjects.World) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module World =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let objects =
                { new Lens<PlaceTransformObjects.World, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,PlaceTransformObjects.Object>>() with
                    override x.Get(r) = r.objects
                    override x.Set(r,v) = { r with objects = v }
                    override x.Update(r,f) = { r with objects = f r.objects }
                }
            let selectedObjects =
                { new Lens<PlaceTransformObjects.World, Aardvark.Base.hset<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.selectedObjects
                    override x.Set(r,v) = { r with selectedObjects = v }
                    override x.Update(r,f) = { r with selectedObjects = f r.selectedObjects }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MScene(__initial : PlaceTransformObjects.Scene) = 
        let mutable __current = __initial
        let _world = MWorld.Create(__initial.world)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.world = _world
        member x.camera = _camera
        
        member x.Update(v : PlaceTransformObjects.Scene) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MWorld.Update(_world, v.world)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
        
        static member Create(v : PlaceTransformObjects.Scene) = MScene(v)
        static member Update(m : MScene, v : PlaceTransformObjects.Scene) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Scene =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let world =
                { new Lens<PlaceTransformObjects.Scene, PlaceTransformObjects.World>() with
                    override x.Get(r) = r.world
                    override x.Set(r,v) = { r with world = v }
                    override x.Update(r,f) = { r with world = f r.world }
                }
            let camera =
                { new Lens<PlaceTransformObjects.Scene, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
