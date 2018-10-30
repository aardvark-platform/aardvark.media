namespace OpcSelectionViewer.Picking

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open OpcSelectionViewer.Picking

[<AutoOpen>]
module Mutable =

    
    
    type MOpcData(__initial : OpcSelectionViewer.Picking.OpcData) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Picking.OpcData> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.Picking.OpcData>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Picking.OpcData>
        let _kdTree = MMap.Create(__initial.kdTree)
        let _localBB = ResetMod.Create(__initial.localBB)
        let _globalBB = ResetMod.Create(__initial.globalBB)
        
        member x.patchHierarchy = __current.Value.patchHierarchy
        member x.kdTree = _kdTree :> amap<_,_>
        member x.localBB = _localBB :> IMod<_>
        member x.globalBB = _globalBB :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.Picking.OpcData) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MMap.Update(_kdTree, v.kdTree)
                ResetMod.Update(_localBB,v.localBB)
                ResetMod.Update(_globalBB,v.globalBB)
                
        
        static member Create(__initial : OpcSelectionViewer.Picking.OpcData) : MOpcData = MOpcData(__initial)
        static member Update(m : MOpcData, v : OpcSelectionViewer.Picking.OpcData) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.Picking.OpcData> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module OpcData =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let patchHierarchy =
                { new Lens<OpcSelectionViewer.Picking.OpcData, Aardvark.SceneGraph.Opc.PatchHierarchy>() with
                    override x.Get(r) = r.patchHierarchy
                    override x.Set(r,v) = { r with patchHierarchy = v }
                    override x.Update(r,f) = { r with patchHierarchy = f r.patchHierarchy }
                }
            let kdTree =
                { new Lens<OpcSelectionViewer.Picking.OpcData, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.KdTrees.Level0KdTree>>() with
                    override x.Get(r) = r.kdTree
                    override x.Set(r,v) = { r with kdTree = v }
                    override x.Update(r,f) = { r with kdTree = f r.kdTree }
                }
            let localBB =
                { new Lens<OpcSelectionViewer.Picking.OpcData, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.localBB
                    override x.Set(r,v) = { r with localBB = v }
                    override x.Update(r,f) = { r with localBB = f r.localBB }
                }
            let globalBB =
                { new Lens<OpcSelectionViewer.Picking.OpcData, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.globalBB
                    override x.Set(r,v) = { r with globalBB = v }
                    override x.Update(r,f) = { r with globalBB = f r.globalBB }
                }
    
    
    type MPickingModel(__initial : OpcSelectionViewer.Picking.PickingModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Picking.PickingModel> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.Picking.PickingModel>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Picking.PickingModel>
        let _pickingInfos = MMap.Create(__initial.pickingInfos, (fun v -> MOpcData.Create(v)), (fun (m,v) -> MOpcData.Update(m, v)), (fun v -> v))
        let _intersectionPoints = MList.Create(__initial.intersectionPoints)
        
        member x.pickingInfos = _pickingInfos :> amap<_,_>
        member x.intersectionPoints = _intersectionPoints :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.Picking.PickingModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MMap.Update(_pickingInfos, v.pickingInfos)
                MList.Update(_intersectionPoints, v.intersectionPoints)
                
        
        static member Create(__initial : OpcSelectionViewer.Picking.PickingModel) : MPickingModel = MPickingModel(__initial)
        static member Update(m : MPickingModel, v : OpcSelectionViewer.Picking.PickingModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.Picking.PickingModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickingModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let pickingInfos =
                { new Lens<OpcSelectionViewer.Picking.PickingModel, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.Picking.OpcData>>() with
                    override x.Get(r) = r.pickingInfos
                    override x.Set(r,v) = { r with pickingInfos = v }
                    override x.Update(r,f) = { r with pickingInfos = f r.pickingInfos }
                }
            let intersectionPoints =
                { new Lens<OpcSelectionViewer.Picking.PickingModel, Aardvark.Base.plist<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.intersectionPoints
                    override x.Set(r,v) = { r with intersectionPoints = v }
                    override x.Update(r,f) = { r with intersectionPoints = f r.intersectionPoints }
                }
