namespace RenderingParametersModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open RenderingParametersModel

[<AutoOpen>]
module Mutable =

    
    
    type MRenderingParameters(__initial : RenderingParametersModel.RenderingParameters) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<RenderingParametersModel.RenderingParameters> = Aardvark.Base.Incremental.EqModRef<RenderingParametersModel.RenderingParameters>(__initial) :> Aardvark.Base.Incremental.IModRef<RenderingParametersModel.RenderingParameters>
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _cullMode = ResetMod.Create(__initial.cullMode)
        
        member x.fillMode = _fillMode :> IMod<_>
        member x.cullMode = _cullMode :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : RenderingParametersModel.RenderingParameters) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_cullMode,v.cullMode)
                
        
        static member Create(__initial : RenderingParametersModel.RenderingParameters) : MRenderingParameters = MRenderingParameters(__initial)
        static member Update(m : MRenderingParameters, v : RenderingParametersModel.RenderingParameters) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<RenderingParametersModel.RenderingParameters> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RenderingParameters =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let fillMode =
                { new Lens<RenderingParametersModel.RenderingParameters, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let cullMode =
                { new Lens<RenderingParametersModel.RenderingParameters, Aardvark.Base.Rendering.CullMode>() with
                    override x.Get(r) = r.cullMode
                    override x.Set(r,v) = { r with cullMode = v }
                    override x.Update(r,f) = { r with cullMode = f r.cullMode }
                }
