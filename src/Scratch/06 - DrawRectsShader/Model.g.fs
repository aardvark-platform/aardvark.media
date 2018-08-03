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
                | Rect(corners, color) -> MRect(__model, corners, color) :> MObject
                | Polygon(vertices, colors) -> MPolygon(__model, vertices, colors) :> MObject
        
        static member Create(v : Inc.Model.Object) =
            ResetMod.Create(MObject.CreateValue v) :> IMod<_>
        
        [<System.Runtime.CompilerServices.Extension>]
        static member Update(m : IMod<MObject>, v : Inc.Model.Object) =
            let m = unbox<ResetMod<MObject>> m
            if not (m.GetValue().TryUpdate v) then
                m.Update(MObject.CreateValue v)
    
    and private MRect(__initial : Inc.Model.Object, corners : Aardvark.Base.Box2d, color : Inc.Model.Color) =
        inherit MObject()
        
        let mutable __current = __initial
        let _corners = ResetMod.Create(corners)
        let _color = ResetMod.Create(color)
        member x.corners = _corners :> IMod<_>
        member x.color = _color :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : Inc.Model.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Rect(corners,color) -> 
                        __current <- __model
                        _corners.Update(corners)
                        _color.Update(color)
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
            | :? MRect as v -> MRect(v.corners,v.color)
            | :? MPolygon as v -> MPolygon(v.vertices,v.colors)
            | _ -> failwith "impossible"
    
    
    
    
    
    
    type MModel(__initial : Inc.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.Model> = Aardvark.Base.Incremental.EqModRef<Inc.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.Model>
        let _selectedObject = MOption.Create(__initial.selectedObject)
        let _objects = MMap.Create(__initial.objects, (fun v -> MObject.Create(v)), (fun (m,v) -> MObject.Update(m, v)), (fun v -> v))
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _hoverHandle = MOption.Create(__initial.hoverHandle)
        let _dragEndpoint = MOption.Create(__initial.dragEndpoint)
        let _translation = MOption.Create(__initial.translation)
        let _down = MOption.Create(__initial.down)
        let _dragging = MOption.Create(__initial.dragging)
        let _openRect = MOption.Create(__initial.openRect)
        
        member x.selectedObject = _selectedObject :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        member x.cameraState = _cameraState
        member x.hoverHandle = _hoverHandle :> IMod<_>
        member x.dragEndpoint = _dragEndpoint :> IMod<_>
        member x.translation = _translation :> IMod<_>
        member x.down = _down :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.openRect = _openRect :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MOption.Update(_selectedObject, v.selectedObject)
                MMap.Update(_objects, v.objects)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                MOption.Update(_hoverHandle, v.hoverHandle)
                MOption.Update(_dragEndpoint, v.dragEndpoint)
                MOption.Update(_translation, v.translation)
                MOption.Update(_down, v.down)
                MOption.Update(_dragging, v.dragging)
                MOption.Update(_openRect, v.openRect)
                
        
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
            let hoverHandle =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<System.Int32>>() with
                    override x.Get(r) = r.hoverHandle
                    override x.Set(r,v) = { r with hoverHandle = v }
                    override x.Update(r,f) = { r with hoverHandle = f r.hoverHandle }
                }
            let dragEndpoint =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Inc.Model.DragEndpoint>>() with
                    override x.Get(r) = r.dragEndpoint
                    override x.Set(r,v) = { r with dragEndpoint = v }
                    override x.Update(r,f) = { r with dragEndpoint = f r.dragEndpoint }
                }
            let translation =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.translation
                    override x.Set(r,v) = { r with translation = v }
                    override x.Update(r,f) = { r with translation = f r.translation }
                }
            let down =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.down
                    override x.Set(r,v) = { r with down = v }
                    override x.Update(r,f) = { r with down = f r.down }
                }
            let dragging =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Inc.Model.DragState>>() with
                    override x.Get(r) = r.dragging
                    override x.Set(r,v) = { r with dragging = v }
                    override x.Update(r,f) = { r with dragging = f r.dragging }
                }
            let openRect =
                { new Lens<Inc.Model.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.Box3d>>() with
                    override x.Get(r) = r.openRect
                    override x.Set(r,v) = { r with openRect = v }
                    override x.Update(r,f) = { r with openRect = f r.openRect }
                }
