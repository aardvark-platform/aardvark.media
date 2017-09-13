namespace QuickTest

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open QuickTest

[<AutoOpen>]
module Mutable =

    
    
    type MDropDownModel(__initial : QuickTest.DropDownModel) =
        inherit obj()
        let mutable __current = __initial
        let _values = MList.Create(__initial.values)
        let _selected = ResetMod.Create(__initial.selected)
        let _newValue = ResetMod.Create(__initial.newValue)
        
        member x.values = _values :> alist<_>
        member x.selected = _selected :> IMod<_>
        member x.newValue = _newValue :> IMod<_>
        
        member x.Update(v : QuickTest.DropDownModel) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_values, v.values)
                ResetMod.Update(_selected,v.selected)
                ResetMod.Update(_newValue,v.newValue)
                
        
        static member Create(__initial : QuickTest.DropDownModel) : MDropDownModel = MDropDownModel(__initial)
        static member Update(m : MDropDownModel, v : QuickTest.DropDownModel) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<QuickTest.DropDownModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DropDownModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let values =
                { new Lens<QuickTest.DropDownModel, Aardvark.Base.plist<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.values
                    override x.Set(r,v) = { r with values = v }
                    override x.Update(r,f) = { r with values = f r.values }
                }
            let selected =
                { new Lens<QuickTest.DropDownModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.selected
                    override x.Set(r,v) = { r with selected = v }
                    override x.Update(r,f) = { r with selected = f r.selected }
                }
            let newValue =
                { new Lens<QuickTest.DropDownModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.newValue
                    override x.Set(r,v) = { r with newValue = v }
                    override x.Update(r,f) = { r with newValue = f r.newValue }
                }
