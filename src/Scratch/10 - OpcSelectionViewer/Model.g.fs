namespace OpcSelectionViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open OpcSelectionViewer

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : OpcSelectionViewer.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Model> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Model>
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _boxes = ResetMod.Create(__initial.boxes)
        let _opcInfos = MMap.Create(__initial.opcInfos, (fun v -> OpcSelectionViewer.Picking.Mutable.MOpcData.Create(v)), (fun (m,v) -> OpcSelectionViewer.Picking.Mutable.MOpcData.Update(m, v)), (fun v -> v))
        let _threads = ResetMod.Create(__initial.threads)
        let _dockConfig = ResetMod.Create(__initial.dockConfig)
        let _picking = OpcSelectionViewer.Picking.Mutable.MPickingModel.Create(__initial.picking)
        let _pickingActive = ResetMod.Create(__initial.pickingActive)
        
        member x.cameraState = _cameraState
        member x.fillMode = _fillMode :> IMod<_>
        member x.patchHierarchies = __current.Value.patchHierarchies
        member x.boxes = _boxes :> IMod<_>
        member x.opcInfos = _opcInfos :> amap<_,_>
        member x.threads = _threads :> IMod<_>
        member x.dockConfig = _dockConfig :> IMod<_>
        member x.picking = _picking
        member x.pickingActive = _pickingActive :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_boxes,v.boxes)
                MMap.Update(_opcInfos, v.opcInfos)
                ResetMod.Update(_threads,v.threads)
                ResetMod.Update(_dockConfig,v.dockConfig)
                OpcSelectionViewer.Picking.Mutable.MPickingModel.Update(_picking, v.picking)
                ResetMod.Update(_pickingActive,v.pickingActive)
                
        
        static member Create(__initial : OpcSelectionViewer.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : OpcSelectionViewer.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let cameraState =
                { new Lens<OpcSelectionViewer.Model, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.cameraState
                    override x.Set(r,v) = { r with cameraState = v }
                    override x.Update(r,f) = { r with cameraState = f r.cameraState }
                }
            let fillMode =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let patchHierarchies =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<Aardvark.SceneGraph.Opc.PatchHierarchy>>() with
                    override x.Get(r) = r.patchHierarchies
                    override x.Set(r,v) = { r with patchHierarchies = v }
                    override x.Update(r,f) = { r with patchHierarchies = f r.patchHierarchies }
                }
            let boxes =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<Aardvark.Base.Box3d>>() with
                    override x.Get(r) = r.boxes
                    override x.Set(r,v) = { r with boxes = v }
                    override x.Update(r,f) = { r with boxes = f r.boxes }
                }
            let opcInfos =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.Picking.OpcData>>() with
                    override x.Get(r) = r.opcInfos
                    override x.Set(r,v) = { r with opcInfos = v }
                    override x.Update(r,f) = { r with opcInfos = f r.opcInfos }
                }
            let threads =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Incremental.ThreadPool<OpcSelectionViewer.Message>>() with
                    override x.Get(r) = r.threads
                    override x.Set(r,v) = { r with threads = v }
                    override x.Update(r,f) = { r with threads = f r.threads }
                }
            let dockConfig =
                { new Lens<OpcSelectionViewer.Model, Aardvark.UI.Primitives.DockConfig>() with
                    override x.Get(r) = r.dockConfig
                    override x.Set(r,v) = { r with dockConfig = v }
                    override x.Update(r,f) = { r with dockConfig = f r.dockConfig }
                }
            let picking =
                { new Lens<OpcSelectionViewer.Model, OpcSelectionViewer.Picking.PickingModel>() with
                    override x.Get(r) = r.picking
                    override x.Set(r,v) = { r with picking = v }
                    override x.Update(r,f) = { r with picking = f r.picking }
                }
            let pickingActive =
                { new Lens<OpcSelectionViewer.Model, System.Boolean>() with
                    override x.Get(r) = r.pickingActive
                    override x.Set(r,v) = { r with pickingActive = v }
                    override x.Update(r,f) = { r with pickingActive = f r.pickingActive }
                }
