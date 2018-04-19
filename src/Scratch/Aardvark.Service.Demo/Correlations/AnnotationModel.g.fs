namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MAnnotation(__initial : CorrelationDrawing.Annotation) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Annotation> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Annotation>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Annotation>
        let _geometry = ResetMod.Create(__initial.geometry)
        let _projection = ResetMod.Create(__initial.projection)
        let _semantic = MSemantic.Create(__initial.semantic)
        let _points = MList.Create(__initial.points)
        let _segments = MList.Create(__initial.segments, (fun v -> MList.Create(v)), (fun (m,v) -> MList.Update(m, v)), (fun v -> v :> alist<_>))
        let _visible = ResetMod.Create(__initial.visible)
        let _text = ResetMod.Create(__initial.text)
        
        member x.geometry = _geometry :> IMod<_>
        member x.projection = _projection :> IMod<_>
        member x.semantic = _semantic
        member x.points = _points :> alist<_>
        member x.segments = _segments :> alist<_>
        member x.visible = _visible :> IMod<_>
        member x.text = _text :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Annotation) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_projection,v.projection)
                MSemantic.Update(_semantic, v.semantic)
                MList.Update(_points, v.points)
                MList.Update(_segments, v.segments)
                ResetMod.Update(_visible,v.visible)
                ResetMod.Update(_text,v.text)
                
        
        static member Create(__initial : CorrelationDrawing.Annotation) : MAnnotation = MAnnotation(__initial)
        static member Update(m : MAnnotation, v : CorrelationDrawing.Annotation) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Annotation> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Annotation =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let geometry =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.GeometryType>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let projection =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.Projection>() with
                    override x.Get(r) = r.projection
                    override x.Set(r,v) = { r with projection = v }
                    override x.Update(r,f) = { r with projection = f r.projection }
                }
            let semantic =
                { new Lens<CorrelationDrawing.Annotation, CorrelationDrawing.Semantic>() with
                    override x.Get(r) = r.semantic
                    override x.Set(r,v) = { r with semantic = v }
                    override x.Update(r,f) = { r with semantic = f r.semantic }
                }
            let points =
                { new Lens<CorrelationDrawing.Annotation, Aardvark.Base.plist<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
            let segments =
                { new Lens<CorrelationDrawing.Annotation, Aardvark.Base.plist<Aardvark.Base.plist<Aardvark.Base.V3d>>>() with
                    override x.Get(r) = r.segments
                    override x.Set(r,v) = { r with segments = v }
                    override x.Update(r,f) = { r with segments = f r.segments }
                }
            let visible =
                { new Lens<CorrelationDrawing.Annotation, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.visible
                    override x.Set(r,v) = { r with visible = v }
                    override x.Update(r,f) = { r with visible = f r.visible }
                }
            let text =
                { new Lens<CorrelationDrawing.Annotation, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.text
                    override x.Set(r,v) = { r with text = v }
                    override x.Update(r,f) = { r with text = f r.text }
                }
