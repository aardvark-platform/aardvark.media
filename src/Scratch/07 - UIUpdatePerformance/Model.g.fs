namespace Inc.Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Inc.Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Inc.Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Inc.Model.Model> = Aardvark.Base.Incremental.EqModRef<Inc.Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Inc.Model.Model>
        let _value = ResetMod.Create(__initial.value)
        let _threads = ResetMod.Create(__initial.threads)
        let _updateStart = ResetMod.Create(__initial.updateStart)
        let _took = ResetMod.Create(__initial.took)
        let _things = MList.Create(__initial.things)
        
        member x.value = _value :> IMod<_>
        member x.threads = _threads :> IMod<_>
        member x.updateStart = _updateStart :> IMod<_>
        member x.took = _took :> IMod<_>
        member x.things = _things :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Inc.Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_value,v.value)
                ResetMod.Update(_threads,v.threads)
                ResetMod.Update(_updateStart,v.updateStart)
                ResetMod.Update(_took,v.took)
                MList.Update(_things, v.things)
                
        
        static member Create(__initial : Inc.Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Inc.Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Inc.Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<Inc.Model.Model, System.Int32>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
            let threads =
                { new Lens<Inc.Model.Model, Aardvark.Base.Incremental.ThreadPool<Inc.Model.Message>>() with
                    override x.Get(r) = r.threads
                    override x.Set(r,v) = { r with threads = v }
                    override x.Update(r,f) = { r with threads = f r.threads }
                }
            let updateStart =
                { new Lens<Inc.Model.Model, System.Double>() with
                    override x.Get(r) = r.updateStart
                    override x.Set(r,v) = { r with updateStart = v }
                    override x.Update(r,f) = { r with updateStart = f r.updateStart }
                }
            let took =
                { new Lens<Inc.Model.Model, System.Double>() with
                    override x.Get(r) = r.took
                    override x.Set(r,v) = { r with took = v }
                    override x.Update(r,f) = { r with took = f r.took }
                }
            let things =
                { new Lens<Inc.Model.Model, Aardvark.Base.plist<System.String>>() with
                    override x.Get(r) = r.things
                    override x.Set(r,v) = { r with things = v }
                    override x.Update(r,f) = { r with things = f r.things }
                }
