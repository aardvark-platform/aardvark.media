namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MLogModel(__initial : CorrelationDrawing.LogModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.LogModel> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.LogModel>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.LogModel>
        let _id = ResetMod.Create(__initial.id)
        let _range = ResetMod.Create(__initial.range)
        
        member x.id = _id :> IMod<_>
        member x.range = _range :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.LogModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_id,v.id)
                ResetMod.Update(_range,v.range)
                
        
        static member Create(__initial : CorrelationDrawing.LogModel) : MLogModel = MLogModel(__initial)
        static member Update(m : MLogModel, v : CorrelationDrawing.LogModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.LogModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module LogModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let id =
                { new Lens<CorrelationDrawing.LogModel, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
            let range =
                { new Lens<CorrelationDrawing.LogModel, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.range
                    override x.Set(r,v) = { r with range = v }
                    override x.Update(r,f) = { r with range = f r.range }
                }
