namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MNavigationParameters(__initial : Model.NavigationParameters) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.NavigationParameters> = Aardvark.Base.Incremental.EqModRef<Model.NavigationParameters>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.NavigationParameters>
        let _navigationMode = ResetMod.Create(__initial.navigationMode)
        
        member x.navigationMode = _navigationMode :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.NavigationParameters) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_navigationMode,v.navigationMode)
                
        
        static member Create(__initial : Model.NavigationParameters) : MNavigationParameters = MNavigationParameters(__initial)
        static member Update(m : MNavigationParameters, v : Model.NavigationParameters) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.NavigationParameters> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NavigationParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let navigationMode =
                { new Lens<Model.NavigationParameters, Model.NavigationMode>() with
                    override x.Get(r) = r.navigationMode
                    override x.Set(r,v) = { r with navigationMode = v }
                    override x.Update(r,f) = { r with navigationMode = f r.navigationMode }
                }
    
    
    type MNavigationModeDemoModel(__initial : Model.NavigationModeDemoModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.NavigationModeDemoModel> = Aardvark.Base.Incremental.EqModRef<Model.NavigationModeDemoModel>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.NavigationModeDemoModel>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = RenderingParametersModel.Mutable.MRenderingParameters.Create(__initial.rendering)
        let _navigation = MNavigationParameters.Create(__initial.navigation)
        let _navsensitivity = Aardvark.UI.Mutable.MNumericInput.Create(__initial.navsensitivity)
        let _zoomFactor = Aardvark.UI.Mutable.MNumericInput.Create(__initial.zoomFactor)
        let _panFactor = Aardvark.UI.Mutable.MNumericInput.Create(__initial.panFactor)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.navigation = _navigation
        member x.navsensitivity = _navsensitivity
        member x.zoomFactor = _zoomFactor
        member x.panFactor = _panFactor
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.NavigationModeDemoModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                RenderingParametersModel.Mutable.MRenderingParameters.Update(_rendering, v.rendering)
                MNavigationParameters.Update(_navigation, v.navigation)
                Aardvark.UI.Mutable.MNumericInput.Update(_navsensitivity, v.navsensitivity)
                Aardvark.UI.Mutable.MNumericInput.Update(_zoomFactor, v.zoomFactor)
                Aardvark.UI.Mutable.MNumericInput.Update(_panFactor, v.panFactor)
                
        
        static member Create(__initial : Model.NavigationModeDemoModel) : MNavigationModeDemoModel = MNavigationModeDemoModel(__initial)
        static member Update(m : MNavigationModeDemoModel, v : Model.NavigationModeDemoModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.NavigationModeDemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NavigationModeDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<Model.NavigationModeDemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<Model.NavigationModeDemoModel, RenderingParametersModel.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let navigation =
                { new Lens<Model.NavigationModeDemoModel, Model.NavigationParameters>() with
                    override x.Get(r) = r.navigation
                    override x.Set(r,v) = { r with navigation = v }
                    override x.Update(r,f) = { r with navigation = f r.navigation }
                }
            let navsensitivity =
                { new Lens<Model.NavigationModeDemoModel, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.navsensitivity
                    override x.Set(r,v) = { r with navsensitivity = v }
                    override x.Update(r,f) = { r with navsensitivity = f r.navsensitivity }
                }
            let zoomFactor =
                { new Lens<Model.NavigationModeDemoModel, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.zoomFactor
                    override x.Set(r,v) = { r with zoomFactor = v }
                    override x.Update(r,f) = { r with zoomFactor = f r.zoomFactor }
                }
            let panFactor =
                { new Lens<Model.NavigationModeDemoModel, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.panFactor
                    override x.Set(r,v) = { r with panFactor = v }
                    override x.Update(r,f) = { r with panFactor = f r.panFactor }
                }
