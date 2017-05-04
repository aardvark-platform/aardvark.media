namespace DragNDrop

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DragNDrop

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MModel private(__initial : DragNDrop.Model) =
        let mutable __current = __initial
        let _trafo = ResetMod(__initial.trafo)
        let _dragging = ResetMod(__initial.dragging)
        let _camera = Demo.TestApp.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.trafo = _trafo :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.camera = _camera
        
        member x.Update(__model : DragNDrop.Model) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _trafo.Update(__model.trafo)
                _dragging.Update(__model.dragging)
                _camera.Update(__model.camera)
        
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
                { new Lens<DragNDrop.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.dragging
                    override x.Set(r,v) = { r with dragging = v }
                    override x.Update(r,f) = { r with dragging = f r.dragging }
                }
            let camera =
                { new Lens<DragNDrop.Model, Demo.TestApp.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
