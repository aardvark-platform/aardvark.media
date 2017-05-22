namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MNumericInput private(__initial : Aardvark.UI.NumericInput) =
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
        
        member x.Update(__model : Aardvark.UI.NumericInput) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _value.Update(__model.value)
                _min.Update(__model.min)
                _max.Update(__model.max)
                _step.Update(__model.step)
                _format.Update(__model.format)
        
        static member Create(initial) = MNumericInput(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MNumericInput =
        let inline value (m : MNumericInput) = m.value
        let inline min (m : MNumericInput) = m.min
        let inline max (m : MNumericInput) = m.max
        let inline step (m : MNumericInput) = m.step
        let inline format (m : MNumericInput) = m.format
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NumericInput =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<Aardvark.UI.NumericInput, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
            let min =
                { new Lens<Aardvark.UI.NumericInput, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.min
                    override x.Set(r,v) = { r with min = v }
                    override x.Update(r,f) = { r with min = f r.min }
                }
            let max =
                { new Lens<Aardvark.UI.NumericInput, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.max
                    override x.Set(r,v) = { r with max = v }
                    override x.Update(r,f) = { r with max = f r.max }
                }
            let step =
                { new Lens<Aardvark.UI.NumericInput, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.step
                    override x.Set(r,v) = { r with step = v }
                    override x.Update(r,f) = { r with step = f r.step }
                }
            let format =
                { new Lens<Aardvark.UI.NumericInput, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.format
                    override x.Set(r,v) = { r with format = v }
                    override x.Update(r,f) = { r with format = f r.format }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    type MTreeViewModel private(__initial : Aardvark.UI.TreeViewModel) =
        let mutable __current = __initial
        let _tree = ResetMod(__initial.tree)
        
        member x.tree = _tree :> IMod<_>
        
        member x.Update(__model : Aardvark.UI.TreeViewModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _tree.Update(__model.tree)
        
        static member Create(initial) = MTreeViewModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MTreeViewModel =
        let inline tree (m : MTreeViewModel) = m.tree
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TreeViewModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let tree =
                { new Lens<Aardvark.UI.TreeViewModel, Aardvark.UI.TreeViewTree>() with
                    override x.Get(r) = r.tree
                    override x.Set(r,v) = { r with tree = v }
                    override x.Update(r,f) = { r with tree = f r.tree }
                }
