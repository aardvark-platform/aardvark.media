namespace SimpleTest

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open SimpleTest

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MModel private(__initial : SimpleTest.Model) =
        let mutable __current = __initial
        let _value = ResetMod(__initial.value)
        let _cameraModel = Aardvark.UI.Mutable.MCameraControllerState.Create(__initial.cameraModel)
        
        member x.value = _value :> IMod<_>
        member x.cameraModel = _cameraModel
        
        member x.Update(__model : SimpleTest.Model) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _value.Update(__model.value)
                _cameraModel.Update(__model.cameraModel)
        
        static member Create(initial) = MModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MModel =
        let inline value (m : MModel) = m.value
        let inline cameraModel (m : MModel) = m.cameraModel
    
    
    
    
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
                { new Lens<SimpleTest.Model, Aardvark.UI.CameraControllerState>() with
                    override x.Get(r) = r.cameraModel
                    override x.Set(r,v) = { r with cameraModel = v }
                    override x.Update(r,f) = { r with cameraModel = f r.cameraModel }
                }
