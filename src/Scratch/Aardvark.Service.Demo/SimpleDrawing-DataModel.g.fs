namespace Simple2DDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Simple2DDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MPolygon(__initial : Simple2DDrawing.Polygon) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Simple2DDrawing.Polygon> = Aardvark.Base.Incremental.EqModRef<Simple2DDrawing.Polygon>(__initial) :> Aardvark.Base.Incremental.IModRef<Simple2DDrawing.Polygon>
        let _points = ResetMod.Create(__initial.points)
        
        member x.points = _points :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Simple2DDrawing.Polygon) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_points,v.points)
                
        
        static member Create(__initial : Simple2DDrawing.Polygon) : MPolygon = MPolygon(__initial)
        static member Update(m : MPolygon, v : Simple2DDrawing.Polygon) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
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
        let mutable __current : Aardvark.Base.Incremental.IModRef<Simple2DDrawing.Model> = Aardvark.Base.Incremental.EqModRef<Simple2DDrawing.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Simple2DDrawing.Model>
        let _finishedPolygons = MList.Create(__initial.finishedPolygons, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        let _workingPolygon = MOption.Create(__initial.workingPolygon, (fun v -> MPolygon.Create(v)), (fun (m,v) -> MPolygon.Update(m, v)), (fun v -> v))
        let _cursor = MOption.Create(__initial.cursor)
        let _past = ResetMod.Create(__initial.past)
        let _future = ResetMod.Create(__initial.future)
        
        member x.finishedPolygons = _finishedPolygons :> alist<_>
        member x.workingPolygon = _workingPolygon :> IMod<_>
        member x.cursor = _cursor :> IMod<_>
        member x.past = _past :> IMod<_>
        member x.future = _future :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Simple2DDrawing.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MList.Update(_finishedPolygons, v.finishedPolygons)
                MOption.Update(_workingPolygon, v.workingPolygon)
                MOption.Update(_cursor, v.cursor)
                _past.Update(v.past)
                _future.Update(v.future)
                
        
        static member Create(__initial : Simple2DDrawing.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Simple2DDrawing.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
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
            let cursor =
                { new Lens<Simple2DDrawing.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V2d>>() with
                    override x.Get(r) = r.cursor
                    override x.Set(r,v) = { r with cursor = v }
                    override x.Update(r,f) = { r with cursor = f r.cursor }
                }
            let past =
                { new Lens<Simple2DDrawing.Model, Microsoft.FSharp.Core.Option<Simple2DDrawing.Model>>() with
                    override x.Get(r) = r.past
                    override x.Set(r,v) = { r with past = v }
                    override x.Update(r,f) = { r with past = f r.past }
                }
            let future =
                { new Lens<Simple2DDrawing.Model, Microsoft.FSharp.Core.Option<Simple2DDrawing.Model>>() with
                    override x.Get(r) = r.future
                    override x.Set(r,v) = { r with future = v }
                    override x.Update(r,f) = { r with future = f r.future }
                }
