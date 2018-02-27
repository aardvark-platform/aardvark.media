namespace DrawingModel

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open DrawingModel

[<AutoOpen>]
module Mutable =

    
    
    type MSimpleDrawingModel(__initial : DrawingModel.SimpleDrawingModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<DrawingModel.SimpleDrawingModel> = Aardvark.Base.Incremental.EqModRef<DrawingModel.SimpleDrawingModel>(__initial) :> Aardvark.Base.Incremental.IModRef<DrawingModel.SimpleDrawingModel>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = RenderingParametersModel.Mutable.MRenderingParameters.Create(__initial.rendering)
        let _draw = ResetMod.Create(__initial.draw)
        let _hoverPosition = MOption.Create(__initial.hoverPosition)
        let _points = ResetMod.Create(__initial.points)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.draw = _draw :> IMod<_>
        member x.hoverPosition = _hoverPosition :> IMod<_>
        member x.points = _points :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : DrawingModel.SimpleDrawingModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                RenderingParametersModel.Mutable.MRenderingParameters.Update(_rendering, v.rendering)
                ResetMod.Update(_draw,v.draw)
                MOption.Update(_hoverPosition, v.hoverPosition)
                ResetMod.Update(_points,v.points)
                
        
        static member Create(__initial : DrawingModel.SimpleDrawingModel) : MSimpleDrawingModel = MSimpleDrawingModel(__initial)
        static member Update(m : MSimpleDrawingModel, v : DrawingModel.SimpleDrawingModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<DrawingModel.SimpleDrawingModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SimpleDrawingModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<DrawingModel.SimpleDrawingModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<DrawingModel.SimpleDrawingModel, RenderingParametersModel.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let draw =
                { new Lens<DrawingModel.SimpleDrawingModel, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.draw
                    override x.Set(r,v) = { r with draw = v }
                    override x.Update(r,f) = { r with draw = f r.draw }
                }
            let hoverPosition =
                { new Lens<DrawingModel.SimpleDrawingModel, Microsoft.FSharp.Core.option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.hoverPosition
                    override x.Set(r,v) = { r with hoverPosition = v }
                    override x.Update(r,f) = { r with hoverPosition = f r.hoverPosition }
                }
            let points =
                { new Lens<DrawingModel.SimpleDrawingModel, Microsoft.FSharp.Collections.list<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
