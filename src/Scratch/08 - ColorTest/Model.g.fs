namespace Inc.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Inc.Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Inc.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.Model> = Aardvark.Base.Incremental.EqModRef<Inc.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.Model>
        let _value = ResetMod.Create(__initial.value)
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        
        member x.value = _value :> IMod<_>
        member x.cameraState = _cameraState
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_value,v.value)
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
            let value =
                { new Lens<Inc.Model.Model, System.Int32>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
            let cameraState =
                { new Lens<Inc.Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
    
    
    type MIObject(__initial : Inc.Model.IObject) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.IObject> = Aardvark.Base.Incremental.EqModRef<Inc.Model.IObject>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.IObject>
        let _itrafo = ResetMod.Create(__initial.itrafo)
        
        member x.itrafo = _itrafo :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.IObject) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_itrafo,v.itrafo)
                
        
        static member Create(__initial : Inc.Model.IObject) : MIObject = MIObject(__initial)
        static member Update(m : MIObject, v : Inc.Model.IObject) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Inc.Model.IObject> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module IObject =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let itrafo =
                { new Lens<Inc.Model.IObject, System.String>() with
                    override x.Get(r) = r.itrafo
                    override x.Set(r,v) = { r with itrafo = v }
                    override x.Update(r,f) = { r with itrafo = f r.itrafo }
                }
    
    
    type MIScene(__initial : Inc.Model.IScene) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.IScene> = Aardvark.Base.Incremental.EqModRef<Inc.Model.IScene>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.IScene>
        let _iobjects = ResetMod.Create(__initial.iobjects)
        
        member x.iobjects = _iobjects :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.IScene) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_iobjects,v.iobjects)
                
        
        static member Create(__initial : Inc.Model.IScene) : MIScene = MIScene(__initial)
        static member Update(m : MIScene, v : Inc.Model.IScene) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Inc.Model.IScene> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module IScene =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let iobjects =
                { new Lens<Inc.Model.IScene, Aardvark.Base.hrefset<Inc.Model.IObject>>() with
                    override x.Get(r) = r.iobjects
                    override x.Set(r,v) = { r with iobjects = v }
                    override x.Update(r,f) = { r with iobjects = f r.iobjects }
                }
