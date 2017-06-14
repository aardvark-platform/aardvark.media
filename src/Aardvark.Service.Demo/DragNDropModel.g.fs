namespace DragNDrop

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DragNDrop

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : DragNDrop.Model) =
        inherit obj()
        let mutable __current = __initial
        let _trafo = ResetMod.Create(__initial.trafo)
        let _dragging = MOption.Create(__initial.dragging)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.trafo = _trafo :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.camera = _camera
        
        member x.Update(v : DragNDrop.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_trafo,v.trafo)
                MOption.Update(_dragging, v.dragging)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                
        
        static member Create(__initial : DragNDrop.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : DragNDrop.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<DragNDrop.Model> with
            member x.Update v = x.Update v
    
    
    
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
    
    
    type MTransformation(__initial : DragNDrop.Transformation) =
        inherit obj()
        let mutable __current = __initial
        let _trafo = ResetMod.Create(__initial.trafo)
        let _hovered = MOption.Create(__initial.hovered)
        let _grabbed = MOption.Create(__initial.grabbed)
        
        member x.trafo = _trafo :> IMod<_>
        member x.hovered = _hovered :> IMod<_>
        member x.grabbed = _grabbed :> IMod<_>
        
        member x.Update(v : DragNDrop.Transformation) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_trafo,v.trafo)
                MOption.Update(_hovered, v.hovered)
                MOption.Update(_grabbed, v.grabbed)
                
        
        static member Create(__initial : DragNDrop.Transformation) : MTransformation = MTransformation(__initial)
        static member Update(m : MTransformation, v : DragNDrop.Transformation) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<DragNDrop.Transformation> with
            member x.Update v = x.Update v
    
    
    
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
    
    
    type MScene(__initial : DragNDrop.Scene) =
        inherit obj()
        let mutable __current = __initial
        let _transformation = MTransformation.Create(__initial.transformation)
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        
        member x.transformation = _transformation
        member x.camera = _camera
        
        member x.Update(v : DragNDrop.Scene) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MTransformation.Update(_transformation, v.transformation)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                
        
        static member Create(__initial : DragNDrop.Scene) : MScene = MScene(__initial)
        static member Update(m : MScene, v : DragNDrop.Scene) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<DragNDrop.Scene> with
            member x.Update v = x.Update v
    
    
    
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
