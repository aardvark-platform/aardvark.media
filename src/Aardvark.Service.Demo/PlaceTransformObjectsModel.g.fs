namespace PlaceTransformObjects

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open PlaceTransformObjects

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MObject private(__initial : PlaceTransformObjects.Object) =
        let mutable __current = __initial
        let _name = ResetMod(__initial.name)
        let _objectType = ResetMod(__initial.objectType)
        let _transformation = DragNDrop.Mutable.MTransformation.Create(__initial.transformation)
        
        member x.name = _name :> IMod<_>
        member x.objectType = _objectType :> IMod<_>
        member x.transformation = _transformation
        
        member x.Update(__model : PlaceTransformObjects.Object) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _name.Update(__model.name)
                _objectType.Update(__model.objectType)
                _transformation.Update(__model.transformation)
        
        static member Update(__self : MObject, __model : PlaceTransformObjects.Object) = __self.Update(__model)
        
        static member Create(initial) = MObject(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MObject =
        let inline name (m : MObject) = m.name
        let inline objectType (m : MObject) = m.objectType
        let inline transformation (m : MObject) = m.transformation
    
    
    
    
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
    [<System.Runtime.CompilerServices.Extension>]
    type MWorld private(__initial : PlaceTransformObjects.World) =
        let mutable __current = __initial
        let _objects = ResetMapMap(__initial.objects, (fun k v -> MObject.Create(v)), MObject.Update)
        let _selectedObjects = ResetSet(__initial.selectedObjects)
        
        member x.objects = _objects :> amap<_,_>
        member x.selectedObjects = _selectedObjects :> aset<_>
        
        member x.Update(__model : PlaceTransformObjects.World) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _objects.Update(__model.objects)
                _selectedObjects.Update(__model.selectedObjects)
        
        static member Update(__self : MWorld, __model : PlaceTransformObjects.World) = __self.Update(__model)
        
        static member Create(initial) = MWorld(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MWorld =
        let inline objects (m : MWorld) = m.objects
        let inline selectedObjects (m : MWorld) = m.selectedObjects
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module World =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let objects =
                { new Lens<PlaceTransformObjects.World, Aardvark.Base.hmap<Microsoft.FSharp.Core.string, PlaceTransformObjects.Object>>() with
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
    [<System.Runtime.CompilerServices.Extension>]
    type MScene private(__initial : PlaceTransformObjects.Scene) =
        let mutable __current = __initial
        let _world = MWorld.Create(__initial.world)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.world = _world
        member x.camera = _camera
        
        member x.Update(__model : PlaceTransformObjects.Scene) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _world.Update(__model.world)
                _camera.Update(__model.camera)
        
        static member Update(__self : MScene, __model : PlaceTransformObjects.Scene) = __self.Update(__model)
        
        static member Create(initial) = MScene(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MScene =
        let inline world (m : MScene) = m.world
        let inline camera (m : MScene) = m.camera
    
    
    
    
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
