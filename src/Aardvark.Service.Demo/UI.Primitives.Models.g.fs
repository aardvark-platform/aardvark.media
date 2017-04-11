namespace UiPrimitives

open System
open Aardvark.Base
open Aardvark.Base.Incremental

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MNumericBox private(__initial : UiPrimitives.NumericBox) =
        let mutable __current = __initial
        let _value = ResetMod(__initial.value)
        
        member x.value = _value :> IMod<_>
        
        member x.Update(__model : UiPrimitives.NumericBox) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _value.Update(__model.value)
        
        static member Create(initial) = MNumericBox(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MNumericBox =
        let inline value (m : MNumericBox) = m.value
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NumericBox =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<UiPrimitives.NumericBox, UiPrimitives.Numeric>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
