module QuickTest

open System
open Aardvark.Base.Incremental



type MDropDownModel(__initial : DropDownModel) =
    inherit obj()
    let mutable __current = __initial
    let _values = ResetMod.Create(__initial.values)
    let _selected = ResetMod.Create(__initial.selected)
    
    member x.values = _values :> IMod<_>
    member x.selected = _selected :> IMod<_>
    
    member x.Update(v : DropDownModel) =
        if not (System.Object.ReferenceEquals(__current, v)) then
            __current <- v
            
            ResetMod.Update(_values,v.values)
            ResetMod.Update(_selected,v.selected)
            
    
    static member Create(__initial : DropDownModel) : MDropDownModel = MDropDownModel(__initial)
    static member Update(m : MDropDownModel, v : DropDownModel) = m.Update(v)
    
    override x.ToString() = __current.ToString()
    member x.AsString = sprintf "%A" __current
    interface IUpdatable<DropDownModel> with
        member x.Update v = x.Update v



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DropDownModel =
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Lens =
        let values =
            { new Lens<DropDownModel, Microsoft.FSharp.Collections.list<Microsoft.FSharp.Core.string>>() with
                override x.Get(r) = r.values
                override x.Set(r,v) = { r with values = v }
                override x.Update(r,f) = { r with values = f r.values }
            }
        let selected =
            { new Lens<DropDownModel, Microsoft.FSharp.Core.string>() with
                override x.Get(r) = r.selected
                override x.Set(r,v) = { r with selected = v }
                override x.Update(r,f) = { r with selected = f r.selected }
            }
