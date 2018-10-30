namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Model> = Aardvark.Base.Incremental.EqModRef<Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Model>
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        let _fill = ResetMod.Create(__initial.fill)
        let _dockConfig = ResetMod.Create(__initial.dockConfig)
        let _files = MList.Create(__initial.files)
        
        member x.past = __current.Value.past
        member x.future = __current.Value.future
        member x.cameraState = _cameraState
        member x.cullMode = _cullMode :> IMod<_>
        member x.fill = _fill :> IMod<_>
        member x.dockConfig = _dockConfig :> IMod<_>
        member x.files = _files :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_cullMode,v.cullMode)
                ResetMod.Update(_fill,v.fill)
                ResetMod.Update(_dockConfig,v.dockConfig)
                MList.Update(_files, v.files)
                
        
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
            let past =
                { new Lens<Model.Model, Microsoft.FSharp.Core.Option<Model.Model>>() with
                    override x.Get(r) = r.past
                    override x.Set(r,v) = { r with past = v }
                    override x.Update(r,f) = { r with past = f r.past }
                }
            let future =
                { new Lens<Model.Model, Microsoft.FSharp.Core.Option<Model.Model>>() with
                    override x.Get(r) = r.future
                    override x.Set(r,v) = { r with future = v }
                    override x.Update(r,f) = { r with future = f r.future }
                }
            let cameraState =
                { new Lens<Model.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
            let cullMode =
                { new Lens<Model.Model, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
            let fill =
                { new Lens<Model.Model, System.Boolean>() with
                    override x.Get(r) = r.fill
                    override x.Set(r,v) = { r with fill = v }
                    override x.Update(r,f) = { r with fill = f r.fill }
                }
            let dockConfig =
                { new Lens<Model.Model, Aardvark.UI.Primitives.DockConfig>() with
                    override x.Get(r) = r.dockConfig
                    override x.Set(r,v) = { r with dockConfig = v }
                    override x.Update(r,f) = { r with dockConfig = f r.dockConfig }
                }
            let files =
                { new Lens<Model.Model, Aardvark.Base.plist<System.String>>() with
                    override x.Get(r) = r.files
                    override x.Set(r,v) = { r with files = v }
                    override x.Update(r,f) = { r with files = f r.files }
                }
