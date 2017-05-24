namespace DragNDrop

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DragNDrop

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MModel private(__initial : DragNDrop.Model) =
        let mutable __current = __initial
        let _trafo = ResetMod(__initial.trafo)
        let _dragging = ResetMod(__initial.dragging)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.trafo = _trafo :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.camera = _camera
        
        member x.Update(__model : DragNDrop.Model) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _trafo.Update(__model.trafo)
                _dragging.Update(__model.dragging)
                _camera.Update(__model.camera)
        
        static member Update(__self : MModel, __model : DragNDrop.Model) = __self.Update(__model)
        
        static member Create(initial) = MModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MModel =
        let inline trafo (m : MModel) = m.trafo
        let inline dragging (m : MModel) = m.dragging
        let inline camera (m : MModel) = m.camera
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let trafo =
                { new Lens<DragNDrop.Model, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.trafo
                    override x.Set(r,v) = { r with trafo = v }
                    override x.Update(r,f) = { r with trafo = f r.trafo }
                }
            let dragging =
                { new Lens<DragNDrop.Model, Microsoft.FSharp.Core.Option<DragNDrop.Drag>>() with
                    override x.Get(r) = r.dragging
                    override x.Set(r,v) = { r with dragging = v }
                    override x.Update(r,f) = { r with dragging = f r.dragging }
                }
            let camera =
                { new Lens<DragNDrop.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MTransformation private(__initial : DragNDrop.Transformation) =
        let mutable __current = __initial
        let _trafo = ResetMod(__initial.trafo)
        let _hovered = ResetMod(__initial.hovered)
        let _grabbed = ResetMod(__initial.grabbed)
        
        member x.trafo = _trafo :> IMod<_>
        member x.hovered = _hovered :> IMod<_>
        member x.grabbed = _grabbed :> IMod<_>
        
        member x.Update(__model : DragNDrop.Transformation) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _trafo.Update(__model.trafo)
                _hovered.Update(__model.hovered)
                _grabbed.Update(__model.grabbed)
        
        static member Update(__self : MTransformation, __model : DragNDrop.Transformation) = __self.Update(__model)
        
        static member Create(initial) = MTransformation(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MTransformation =
        let inline trafo (m : MTransformation) = m.trafo
        let inline hovered (m : MTransformation) = m.hovered
        let inline grabbed (m : MTransformation) = m.grabbed
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Transformation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let trafo =
                { new Lens<DragNDrop.Transformation, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.trafo
                    override x.Set(r,v) = { r with trafo = v }
                    override x.Update(r,f) = { r with trafo = f r.trafo }
                }
            let hovered =
                { new Lens<DragNDrop.Transformation, Microsoft.FSharp.Core.Option<DragNDrop.Axis>>() with
                    override x.Get(r) = r.hovered
                    override x.Set(r,v) = { r with hovered = v }
                    override x.Update(r,f) = { r with hovered = f r.hovered }
                }
            let grabbed =
                { new Lens<DragNDrop.Transformation, Microsoft.FSharp.Core.Option<DragNDrop.PickPoint>>() with
                    override x.Get(r) = r.grabbed
                    override x.Set(r,v) = { r with grabbed = v }
                    override x.Update(r,f) = { r with grabbed = f r.grabbed }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MScene private(__initial : DragNDrop.Scene) =
        let mutable __current = __initial
        let _transformation = MTransformation.Create(__initial.transformation)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.transformation = _transformation
        member x.camera = _camera
        
        member x.Update(__model : DragNDrop.Scene) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _transformation.Update(__model.transformation)
                _camera.Update(__model.camera)
        
        static member Update(__self : MScene, __model : DragNDrop.Scene) = __self.Update(__model)
        
        static member Create(initial) = MScene(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MScene =
        let inline transformation (m : MScene) = m.transformation
        let inline camera (m : MScene) = m.camera
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Scene =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let transformation =
                { new Lens<DragNDrop.Scene, DragNDrop.Transformation>() with
                    override x.Get(r) = r.transformation
                    override x.Set(r,v) = { r with transformation = v }
                    override x.Update(r,f) = { r with transformation = f r.transformation }
                }
            let camera =
                { new Lens<DragNDrop.Scene, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
