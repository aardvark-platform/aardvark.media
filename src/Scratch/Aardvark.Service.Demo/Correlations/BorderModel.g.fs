namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MBorder(__initial : CorrelationDrawing.Border) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Border> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Border>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Border>
        let _annotations = MList.Create(__initial.annotations, (fun v -> MAnnotation.Create(v)), (fun (m,v) -> MAnnotation.Update(m, v)), (fun v -> v))
        
        member x.annotations = _annotations :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Border) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MList.Update(_annotations, v.annotations)
                
        
        static member Create(__initial : CorrelationDrawing.Border) : MBorder = MBorder(__initial)
        static member Update(m : MBorder, v : CorrelationDrawing.Border) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Border> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Border =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let annotations =
                { new Lens<CorrelationDrawing.Border, Aardvark.Base.plist<CorrelationDrawing.Annotation>>() with
                    override x.Get(r) = r.annotations
                    override x.Set(r,v) = { r with annotations = v }
                    override x.Update(r,f) = { r with annotations = f r.annotations }
                }
