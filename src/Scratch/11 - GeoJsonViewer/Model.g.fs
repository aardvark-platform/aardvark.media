namespace GeoJsonViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open GeoJsonViewer

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : GeoJsonViewer.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<GeoJsonViewer.Model> = Aardvark.Base.Incremental.EqModRef<GeoJsonViewer.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<GeoJsonViewer.Model>
        let _value = ResetMod.Create(__initial.value)
        
        member x.value = _value :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : GeoJsonViewer.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_value,v.value)
                
        
        static member Create(__initial : GeoJsonViewer.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : GeoJsonViewer.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<GeoJsonViewer.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let value =
                { new Lens<GeoJsonViewer.Model, System.Int32>() with
                    override x.Get(r) = r.value
                    override x.Set(r,v) = { r with value = v }
                    override x.Update(r,f) = { r with value = f r.value }
                }
