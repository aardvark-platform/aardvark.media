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
        let _min = ResetMod(__initial.min)
        let _max = ResetMod(__initial.max)
        let _step = ResetMod(__initial.step)
        let _format = ResetMod(__initial.format)
        
        member x.value = _value :> IMod<_>
        member x.min = _min :> IMod<_>
        member x.max = _max :> IMod<_>
        member x.step = _step :> IMod<_>
        member x.format = _format :> IMod<_>
        
        member x.Update(__model : UiPrimitives.NumericBox) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _value.Update(__model.value)
                _min.Update(__model.min)
                _max.Update(__model.max)
                _step.Update(__model.step)
                _format.Update(__model.format)
        
        static member Create(initial) = MNumericBox(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MNumericBox =
        let inline value (m : MNumericBox) = m.value
        let inline min (m : MNumericBox) = m.min
        let inline max (m : MNumericBox) = m.max
        let inline step (m : MNumericBox) = m.step
        let inline format (m : MNumericBox) = m.format
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NumericBox =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<UiPrimitives.NumericBox, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
            let min =
                { new Lens<UiPrimitives.NumericBox, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.min
                    override x.Set(r,v) = { r with min = v }
                    override x.Update(r,f) = { r with min = f r.min }
                }
            let max =
                { new Lens<UiPrimitives.NumericBox, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.max
                    override x.Set(r,v) = { r with max = v }
                    override x.Update(r,f) = { r with max = f r.max }
                }
            let step =
                { new Lens<UiPrimitives.NumericBox, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.step
                    override x.Set(r,v) = { r with step = v }
                    override x.Update(r,f) = { r with step = f r.step }
                }
            let format =
                { new Lens<UiPrimitives.NumericBox, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.format
                    override x.Set(r,v) = { r with format = v }
                    override x.Update(r,f) = { r with format = f r.format }
                }
