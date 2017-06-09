namespace RenderModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open RenderModel

[<AutoOpen>]
module Mutable =

    [<AbstractClass; System.Runtime.CompilerServices.Extension; StructuredFormatDisplay("{AsString}")>]
    type MObject() =
        abstract member TryUpdate : RenderModel.Object -> bool
        abstract member AsString : string
        
        static member private CreateValue(__model : RenderModel.Object) = 
            match __model with
                | FileModel(item) -> MFileModel(__model, item) :> MObject
                | Sphere(center, radius) -> MSphere(__model, center, radius) :> MObject
                | Box(item) -> MBox(__model, item) :> MObject
        
        static member Create(v : RenderModel.Object) =
            ResetMod.Create(MObject.CreateValue v) :> IMod<_>
        
        [<System.Runtime.CompilerServices.Extension>]
        static member Update(m : IMod<MObject>, v : RenderModel.Object) =
            let m = unbox<ResetMod<MObject>> m
            if not (m.GetValue().TryUpdate v) then
                m.Update(MObject.CreateValue v)
    
    and private MFileModel(__initial : RenderModel.Object, item : Microsoft.FSharp.Core.string) =
        inherit MObject()
        
        let mutable __current = __initial
        let _item = ResetMod.Create(item)
        member x.item = _item :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : RenderModel.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | FileModel(item) -> 
                        __current <- __model
                        _item.Update(item)
                        true
                    | _ -> false
    
    and private MSphere(__initial : RenderModel.Object, center : Aardvark.Base.V3d, radius : Microsoft.FSharp.Core.float) =
        inherit MObject()
        
        let mutable __current = __initial
        let _center = ResetMod.Create(center)
        let _radius = ResetMod.Create(radius)
        member x.center = _center :> IMod<_>
        member x.radius = _radius :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : RenderModel.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Sphere(center,radius) -> 
                        __current <- __model
                        _center.Update(center)
                        _radius.Update(radius)
                        true
                    | _ -> false
    
    and private MBox(__initial : RenderModel.Object, item : Aardvark.Base.Box3d) =
        inherit MObject()
        
        let mutable __current = __initial
        let _item = ResetMod.Create(item)
        member x.item = _item :> IMod<_>
        
        override x.ToString() = __current.ToString()
        override x.AsString = sprintf "%A" __current
        
        override x.TryUpdate(__model : RenderModel.Object) = 
            if System.Object.ReferenceEquals(__current, __model) then
                true
            else
                match __model with
                    | Box(item) -> 
                        __current <- __model
                        _item.Update(item)
                        true
                    | _ -> false
    
    
    [<AutoOpen>]
    module MObjectPatterns =
        let (|MFileModel|MSphere|MBox|) (m : MObject) =
            match m with
            | :? MFileModel as v -> MFileModel(v.item)
            | :? MSphere as v -> MSphere(v.center,v.radius)
            | :? MBox as v -> MBox(v.item)
            | _ -> failwith "impossible"
    
    
    
    
    [<StructuredFormatDisplay("{AsString}")>]
    type MModel(__initial : RenderModel.Model) = 
        let mutable __current = __initial
        let _trafo = ResetMod.Create(__initial.trafo)
        let _currentModel = MOption.Create(__initial.currentModel, (fun v -> MObject.Create(v)), (fun (m,v) -> MObject.Update(m, v)), (fun v -> v))
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        
        member x.trafo = _trafo :> IMod<_>
        member x.currentModel = _currentModel :> IMod<_>
        member x.cameraState = _cameraState
        
        member x.Update(v : RenderModel.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_trafo,v.trafo)
                MOption.Update(_currentModel, v.currentModel)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
        
        static member Create(v : RenderModel.Model) = MModel(v)
        static member Update(m : MModel, v : RenderModel.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let trafo =
                { new Lens<RenderModel.Model, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.trafo
                    override x.Set(r,v) = { r with trafo = v }
                    override x.Update(r,f) = { r with trafo = f r.trafo }
                }
            let currentModel =
                { new Lens<RenderModel.Model, Microsoft.FSharp.Core.Option<RenderModel.Object>>() with
                    override x.Get(r) = r.currentModel
                    override x.Set(r,v) = { r with currentModel = v }
                    override x.Update(r,f) = { r with currentModel = f r.currentModel }
                }
            let cameraState =
                { new Lens<RenderModel.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
