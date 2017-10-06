namespace QuickTest

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open QuickTest

[<AutoOpen>]
module Mutable =

    
    
    type MQuickTestModel(__initial : QuickTest.QuickTestModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.ModRef<QuickTest.QuickTestModel> = Aardvark.Base.Incremental.Mod.init(__initial)
        let _values = MList.Create(__initial.values)
        let _selected = ResetMod.Create(__initial.selected)
        let _newValue = MOption.Create(__initial.newValue)
        
        member x.values = _values :> alist<_>
        member x.selected = _selected :> IMod<_>
        member x.newValue = _newValue :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : QuickTest.QuickTestModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MList.Update(_values, v.values)
                ResetMod.Update(_selected,v.selected)
                MOption.Update(_newValue, v.newValue)
                
        
        static member Create(__initial : QuickTest.QuickTestModel) : MQuickTestModel = MQuickTestModel(__initial)
        static member Update(m : MQuickTestModel, v : QuickTest.QuickTestModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<QuickTest.QuickTestModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module QuickTestModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let values =
                { new Lens<QuickTest.QuickTestModel, Aardvark.Base.plist<QuickTest.Person>>() with
                    override x.Get(r) = r.values
                    override x.Set(r,v) = { r with values = v }
                    override x.Update(r,f) = { r with values = f r.values }
                }
            let selected =
                { new Lens<QuickTest.QuickTestModel, QuickTest.Person>() with
                    override x.Get(r) = r.selected
                    override x.Set(r,v) = { r with selected = v }
                    override x.Update(r,f) = { r with selected = f r.selected }
                }
            let newValue =
                { new Lens<QuickTest.QuickTestModel, Microsoft.FSharp.Core.option<QuickTest.Person>>() with
                    override x.Get(r) = r.newValue
                    override x.Set(r,v) = { r with newValue = v }
                    override x.Update(r,f) = { r with newValue = f r.newValue }
                }
