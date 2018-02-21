namespace CorrelationDrawing

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open CorrelationDrawing

[<AutoOpen>]
module Mutable =

    
    
    type MSemantic(__initial : CorrelationDrawing.Semantic) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Semantic> = Aardvark.Base.Incremental.EqModRef<CorrelationDrawing.Semantic>(__initial) :> Aardvark.Base.Incremental.IModRef<CorrelationDrawing.Semantic>
        let _label = ResetMod.Create(__initial.label)
        let _elevation = ResetMod.Create(__initial.elevation)
        let _azimuth = ResetMod.Create(__initial.azimuth)
        let _size = ResetMod.Create(__initial.size)
        let _style = ResetMod.Create(__initial.style)
        let _geometry = ResetMod.Create(__initial.geometry)
        
        member x.label = _label :> IMod<_>
        member x.elevation = _elevation :> IMod<_>
        member x.azimuth = _azimuth :> IMod<_>
        member x.size = _size :> IMod<_>
        member x.style = _style :> IMod<_>
        member x.geometry = _geometry :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : CorrelationDrawing.Semantic) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_label,v.label)
                ResetMod.Update(_elevation,v.elevation)
                ResetMod.Update(_azimuth,v.azimuth)
                ResetMod.Update(_size,v.size)
                ResetMod.Update(_style,v.style)
                ResetMod.Update(_geometry,v.geometry)
                
        
        static member Create(__initial : CorrelationDrawing.Semantic) : MSemantic = MSemantic(__initial)
        static member Update(m : MSemantic, v : CorrelationDrawing.Semantic) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<CorrelationDrawing.Semantic> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Semantic =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let label =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.label
                    override x.Set(r,v) = { r with label = v }
                    override x.Update(r,f) = { r with label = f r.label }
                }
            let elevation =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.double>() with
                    override x.Get(r) = r.elevation
                    override x.Set(r,v) = { r with elevation = v }
                    override x.Update(r,f) = { r with elevation = f r.elevation }
                }
            let azimuth =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.double>() with
                    override x.Get(r) = r.azimuth
                    override x.Set(r,v) = { r with azimuth = v }
                    override x.Update(r,f) = { r with azimuth = f r.azimuth }
                }
            let size =
                { new Lens<CorrelationDrawing.Semantic, Microsoft.FSharp.Core.double>() with
                    override x.Get(r) = r.size
                    override x.Set(r,v) = { r with size = v }
                    override x.Update(r,f) = { r with size = f r.size }
                }
            let style =
                { new Lens<CorrelationDrawing.Semantic, CorrelationRendering.Types.Style>() with
                    override x.Get(r) = r.style
                    override x.Set(r,v) = { r with style = v }
                    override x.Update(r,f) = { r with style = f r.style }
                }
            let geometry =
                { new Lens<CorrelationDrawing.Semantic, CorrelationRendering.Types.GeometryType>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
