namespace SvgDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open SvgDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : SvgDrawing.Model) =
        inherit obj()
        let mutable __current = __initial
        let _nixi = ResetMod.Create(__initial.nixi)
        
        member x.nixi = _nixi :> IMod<_>
        
        member x.Update(v : SvgDrawing.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_nixi,v.nixi)
                
        
        static member Create(__initial : SvgDrawing.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : SvgDrawing.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<SvgDrawing.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let nixi =
                { new Lens<SvgDrawing.Model, Microsoft.FSharp.Core.int>() with
                    override x.Get(r) = r.nixi
                    override x.Set(r,v) = { r with nixi = v }
                    override x.Update(r,f) = { r with nixi = f r.nixi }
                }
