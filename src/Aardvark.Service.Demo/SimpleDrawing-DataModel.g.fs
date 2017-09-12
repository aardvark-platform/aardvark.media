namespace SimpleDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open SimpleDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MPolygon(__initial : SimpleDrawing.Polygon) =
        inherit obj()
        let mutable __current = __initial
        let _points = MList.Create(__initial.points, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        
        member x.points = _points :> alist<_>
        
        member x.Update(v : SimpleDrawing.Polygon) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_points, v.points)
                
        
        static member Create(__initial : SimpleDrawing.Polygon) : MPolygon = MPolygon(__initial)
        static member Update(m : MPolygon, v : SimpleDrawing.Polygon) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<SimpleDrawing.Polygon> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Polygon =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let points =
                { new Lens<SimpleDrawing.Polygon, Aardvark.Base.plist<SimpleDrawing.Polygon>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
    
    
    type MModel(__initial : SimpleDrawing.Model) =
        inherit obj()
        let mutable __current = __initial
        let _polygons = MList.Create(__initial.polygons, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        
        member x.polygons = _polygons :> alist<_>
        
        member x.Update(v : SimpleDrawing.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                MList.Update(_polygons, v.polygons)
                
        
        static member Create(__initial : SimpleDrawing.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : SimpleDrawing.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
        interface IUpdatable<SimpleDrawing.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let polygons =
                { new Lens<SimpleDrawing.Model, Aardvark.Base.plist<SimpleDrawing.Polygon>>() with
                    override x.Get(r) = r.polygons
                    override x.Set(r,v) = { r with polygons = v }
                    override x.Update(r,f) = { r with polygons = f r.polygons }
                }
