namespace SimpleTest

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open SimpleTest

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MModel(__initial : SimpleTest.Model) = 
        let mutable __current = __initial
        let _value = ResetMod.Create(__initial.value)
        let _cameraModel = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraModel)
        
        member x.value = _value :> IMod<_>
        member x.cameraModel = _cameraModel
        
        member x.Update(v : SimpleTest.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_value,v.value)
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraModel, v.cameraModel)
        
        static member Create(v : SimpleTest.Model) = MModel(v)
        static member Update(m : MModel, v : SimpleTest.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<SimpleTest.Model, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
            let cameraModel =
                { new Lens<SimpleTest.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraModel
                    override x.Set(r,v) = { r with cameraModel = v }
                    override x.Update(r,f) = { r with cameraModel = f r.cameraModel }
                }
