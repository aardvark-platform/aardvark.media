namespace Simple2DDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Simple2DDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MPolygon(__initial : Simple2DDrawing.Polygon) =
        inherit obj()
        let mutable __current = __initial
        let _points = ResetMod.Create(__initial.points)
        
        member x.points = _points :> IMod<_>
        
        member x.Update(v : Simple2DDrawing.Polygon) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_points,v.points)
                
        
        static member Create(__initial : Simple2DDrawing.Polygon) : MPolygon = MPolygon(__initial)
        static member Update(m : MPolygon, v : Simple2DDrawing.Polygon) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<Simple2DDrawing.Polygon> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Polygon =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let points =
                { new Lens<Simple2DDrawing.Polygon, Microsoft.FSharp.Collections.list<Aardvark.Base.V2d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
    
    
    type MModel(__initial : Simple2DDrawing.Model) =
        inherit obj()
        let mutable __current = __initial
        let _finishedPolygons = MList.Create(__initial.finishedPolygons, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        let _workingPolygon = MOption.Create(__initial.workingPolygon, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        
        member x.finishedPolygons = _finishedPolygons :> alist<_>
        member x.workingPolygon = _workingPolygon :> IMod<_>
        
        member x.Update(v : Simple2DDrawing.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_finishedPolygons, v.finishedPolygons)
                MOption.Update(_workingPolygon, v.workingPolygon)
                
        
        static member Create(__initial : Simple2DDrawing.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Simple2DDrawing.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<Simple2DDrawing.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let finishedPolygons =
                { new Lens<Simple2DDrawing.Model, Aardvark.Base.plist<Simple2DDrawing.Polygon>>() with
                    override x.Get(r) = r.finishedPolygons
                    override x.Set(r,v) = { r with finishedPolygons = v }
                    override x.Update(r,f) = { r with finishedPolygons = f r.finishedPolygons }
                }
            let workingPolygon =
                { new Lens<Simple2DDrawing.Model, Microsoft.FSharp.Core.Option<Simple2DDrawing.Polygon>>() with
                    override x.Get(r) = r.workingPolygon
                    override x.Set(r,v) = { r with workingPolygon = v }
                    override x.Update(r,f) = { r with workingPolygon = f r.workingPolygon }
                }
