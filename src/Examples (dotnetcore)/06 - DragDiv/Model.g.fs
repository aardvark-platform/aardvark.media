namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MObject(__initial : Model.Object) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Object> = Aardvark.Base.Incremental.EqModRef<Model.Object>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Object>
        let _position = ResetMod.Create(__initial.position)
        
        member x.position = _position :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Object) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_position,v.position)
                
        
        static member Create(__initial : Model.Object) : MObject = MObject(__initial)
        static member Update(m : MObject, v : Model.Object) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.Object> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Object =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let position =
                { new Lens<Model.Object, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.position
                    override x.Set(r,v) = { r with position = v }
                    override x.Update(r,f) = { r with position = f r.position }
                }
    
    
    type MDrag(__initial : Model.Drag) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Drag> = Aardvark.Base.Incremental.EqModRef<Model.Drag>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Drag>
        let _name = ResetMod.Create(__initial.name)
        let _startOffset = ResetMod.Create(__initial.startOffset)
        
        member x.name = _name :> IMod<_>
        member x.startOffset = _startOffset :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Drag) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_name,v.name)
                ResetMod.Update(_startOffset,v.startOffset)
                
        
        static member Create(__initial : Model.Drag) : MDrag = MDrag(__initial)
        static member Update(m : MDrag, v : Model.Drag) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.Drag> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Drag =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let name =
                { new Lens<Model.Drag, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.name
                    override x.Set(r,v) = { r with name = v }
                    override x.Update(r,f) = { r with name = f r.name }
                }
            let startOffset =
                { new Lens<Model.Drag, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.startOffset
                    override x.Set(r,v) = { r with startOffset = v }
                    override x.Update(r,f) = { r with startOffset = f r.startOffset }
                }
    
    
    type MModel(__initial : Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Model> = Aardvark.Base.Incremental.EqModRef<Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Model>
        let _dragObject = MOption.Create(__initial.dragObject, (fun v -> MDrag.Create(v)), (fun (m,v) -> MDrag.Update(m, v)), (fun v -> v))
        let _objects = MMap.Create(__initial.objects, (fun v -> MObject.Create(v)), (fun (m,v) -> MObject.Update(m, v)), (fun v -> v))
        
        member x.dragObject = _dragObject :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MOption.Update(_dragObject, v.dragObject)
                MMap.Update(_objects, v.objects)
                
        
        static member Create(__initial : Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let dragObject =
                { new Lens<Model.Model, Microsoft.FSharp.Core.Option<Model.Drag>>() with
                    override x.Get(r) = r.dragObject
                    override x.Set(r,v) = { r with dragObject = v }
                    override x.Update(r,f) = { r with dragObject = f r.dragObject }
                }
            let objects =
                { new Lens<Model.Model, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,Model.Object>>() with
                    override x.Get(r) = r.objects
                    override x.Set(r,v) = { r with objects = v }
                    override x.Update(r,f) = { r with objects = f r.objects }
                }
