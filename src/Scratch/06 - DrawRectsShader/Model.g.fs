namespace Inc.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Inc.Model

[<AutoOpen>]
module Mutable =

    [<AbstractClass; System.Runtime.CompilerServices.Extension; StructuredFormatDisplay("{AsString}")>]
    type MObject() =
        abstract member TryUpdate : Inc.Model.Object -> bool
        abstract member AsString : string
        
        static member private CreateValue(__model : Inc.Model.Object) = 
            match __model with
                | Rect(corners, colors) -> MRect(__model, corners, colors) :> MObject
                | Polygon(vertices, colors) -> MPolygon(__model, vertices, colors) :> MObject
        
        static member Create(v : Inc.Model.Object) =
            ResetMod.Create(MObject.CreateValue v) :> IMod<_>
        
        [<System.Runtime.CompilerServices.Extension>]
        static member Update(m : IMod<MObject>, v : Inc.Model.Object) =
            let m = unbox<ResetMod<MObject>> m
            if not (m.GetValue().TryUpdate v) then
                m.Update(MObject.CreateValue v)
    
    and private MRect(__initial : Inc.Model.Object, corners : Aardvark.Base.Box2d, colors : Microsoft.FSharp.Core.array<Aardvark.Base.C4f>) =
        inherit MObject()
        
        let mutable __current = __initial
        let _corners = ResetMod.Create(corners)
        let _colors = ResetMod.Create(colors)
        member x.corners = _corners :> IMod<_>
        member x.colors = _colors :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : Inc.Model.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Rect(corners,colors) -> 
                        __current <- __model
                        _corners.Update(corners)
                        _colors.Update(colors)
                        true
                    | _ -> false
    
    and private MPolygon(__initial : Inc.Model.Object, vertices : Microsoft.FSharp.Core.array<Aardvark.Base.V2f>, colors : Microsoft.FSharp.Core.array<Aardvark.Base.C4f>) =
        inherit MObject()
        
        let mutable __current = __initial
        let _vertices = ResetMod.Create(vertices)
        let _colors = ResetMod.Create(colors)
        member x.vertices = _vertices :> IMod<_>
        member x.colors = _colors :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : Inc.Model.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Polygon(vertices,colors) -> 
                        __current <- __model
                        _vertices.Update(vertices)
                        _colors.Update(colors)
                        true
                    | _ -> false
    
    
    [<AutoOpen>]
    module MObjectPatterns =
        let (|MRect|MPolygon|) (m : MObject) =
            match m with
            | :? MRect as v -> MRect(v.corners,v.colors)
            | :? MPolygon as v -> MPolygon(v.vertices,v.colors)
            | _ -> failwith "impossible"
    
    
    
    
    
    
    type MModel(__initial : Inc.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.Model> = Aardvark.Base.Incremental.EqModRef<Inc.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.Model>
        let _selectedObject = MOption.Create(__initial.selectedObject)
        let _objects = MMap.Create(__initial.objects, (fun v -> MObject.Create(v)), (fun (m,v) -> MObject.Update(m, v)), (fun v -> v))
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        
        member x.selectedObject = _selectedObject :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        member x.cameraState = _cameraState
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MOption.Update(_selectedObject, v.selectedObject)
                MMap.Update(_objects, v.objects)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                
        
        static member Create(__initial : Inc.Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Inc.Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Inc.Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let selectedObject =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<System.Int32>>() with
                    override x.Get(r) = r.selectedObject
                    override x.Set(r,v) = { r with selectedObject = v }
                    override x.Update(r,f) = { r with selectedObject = f r.selectedObject }
                }
            let objects =
                { new Lens<Inc.Model.Model, Aardvark.Base.hmap<System.Int32,Inc.Model.Object>>() with
                    override x.Get(r) = r.objects
                    override x.Set(r,v) = { r with objects = v }
                    override x.Update(r,f) = { r with objects = f r.objects }
                }
            let cameraState =
                { new Lens<Inc.Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
