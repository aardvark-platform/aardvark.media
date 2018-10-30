namespace OpcSelectionViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open OpcSelectionViewer

[<AutoOpen>]
module Mutable =

    
    
    type MOpcData(__initial : OpcSelectionViewer.OpcData) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.OpcData> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.OpcData>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.OpcData>
        let _kdTree = MMap.Create(__initial.kdTree)
        let _localBB = ResetMod.Create(__initial.localBB)
        let _globalBB = ResetMod.Create(__initial.globalBB)
        
        member x.patchHierarchy = __current.Value.patchHierarchy
        member x.kdTree = _kdTree :> amap<_,_>
        member x.localBB = _localBB :> IMod<_>
        member x.globalBB = _globalBB :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.OpcData) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MMap.Update(_kdTree, v.kdTree)
                ResetMod.Update(_localBB,v.localBB)
                ResetMod.Update(_globalBB,v.globalBB)
                
        
        static member Create(__initial : OpcSelectionViewer.OpcData) : MOpcData = MOpcData(__initial)
        static member Update(m : MOpcData, v : OpcSelectionViewer.OpcData) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.OpcData> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module OpcData =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let patchHierarchy =
                { new Lens<OpcSelectionViewer.OpcData, Aardvark.SceneGraph.Opc.PatchHierarchy>() with
                    override x.Get(r) = r.patchHierarchy
                    override x.Set(r,v) = { r with patchHierarchy = v }
                    override x.Update(r,f) = { r with patchHierarchy = f r.patchHierarchy }
                }
            let kdTree =
                { new Lens<OpcSelectionViewer.OpcData, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.KdTrees.Level0KdTree>>() with
                    override x.Get(r) = r.kdTree
                    override x.Set(r,v) = { r with kdTree = v }
                    override x.Update(r,f) = { r with kdTree = f r.kdTree }
                }
            let localBB =
                { new Lens<OpcSelectionViewer.OpcData, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.localBB
                    override x.Set(r,v) = { r with localBB = v }
                    override x.Update(r,f) = { r with localBB = f r.localBB }
                }
            let globalBB =
                { new Lens<OpcSelectionViewer.OpcData, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.globalBB
                    override x.Set(r,v) = { r with globalBB = v }
                    override x.Update(r,f) = { r with globalBB = f r.globalBB }
                }
    
    
    type MModel(__initial : OpcSelectionViewer.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Model> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.Model>
        let _cameraState = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.cameraState)
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _opcInfos = MMap.Create(__initial.opcInfos, (fun v -> MOpcData.Create(v)), (fun (m,v) -> MOpcData.Update(m, v)), (fun v -> v))
        let _boxes = ResetMod.Create(__initial.boxes)
        let _intersectionPoints = ResetMod.Create(__initial.intersectionPoints)
        let _threads = ResetMod.Create(__initial.threads)
        let _intersection = ResetMod.Create(__initial.intersection)
        
        member x.cameraState = _cameraState
        member x.fillMode = _fillMode :> IMod<_>
        member x.opcInfos = _opcInfos :> amap<_,_>
        member x.patchHierarchies = __current.Value.patchHierarchies
        member x.kdTrees2 = __current.Value.kdTrees2
        member x.boxes = _boxes :> IMod<_>
        member x.intersectionPoints = _intersectionPoints :> IMod<_>
        member x.threads = _threads :> IMod<_>
        member x.intersection = _intersection :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                ResetMod.Update(_fillMode,v.fillMode)
                MMap.Update(_opcInfos, v.opcInfos)
                ResetMod.Update(_boxes,v.boxes)
                ResetMod.Update(_intersectionPoints,v.intersectionPoints)
                ResetMod.Update(_threads,v.threads)
                ResetMod.Update(_intersection,v.intersection)
                
        
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
            let opcInfos =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.OpcData>>() with
                    override x.Get(r) = r.opcInfos
                    override x.Set(r,v) = { r with opcInfos = v }
                    override x.Update(r,f) = { r with opcInfos = f r.opcInfos }
                }
            let patchHierarchies =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<Aardvark.SceneGraph.Opc.PatchHierarchy>>() with
                    override x.Get(r) = r.patchHierarchies
                    override x.Set(r,v) = { r with patchHierarchies = v }
                    override x.Update(r,f) = { r with patchHierarchies = f r.patchHierarchies }
                }
            let kdTrees2 =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.hmap<Aardvark.Base.Box3d,OpcSelectionViewer.KdTrees.Level0KdTree>>() with
                    override x.Get(r) = r.kdTrees2
                    override x.Set(r,v) = { r with kdTrees2 = v }
                    override x.Update(r,f) = { r with kdTrees2 = f r.kdTrees2 }
                }
            let boxes =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<Aardvark.Base.Box3d>>() with
                    override x.Get(r) = r.boxes
                    override x.Set(r,v) = { r with boxes = v }
                    override x.Update(r,f) = { r with boxes = f r.boxes }
                }
            let intersectionPoints =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.V3f[]>() with
                    override x.Get(r) = r.intersectionPoints
                    override x.Set(r,v) = { r with intersectionPoints = v }
                    override x.Update(r,f) = { r with intersectionPoints = f r.intersectionPoints }
                }
            let threads =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Incremental.ThreadPool<OpcSelectionViewer.Message>>() with
                    override x.Get(r) = r.threads
                    override x.Set(r,v) = { r with threads = v }
                    override x.Update(r,f) = { r with threads = f r.threads }
                }
            let intersection =
                { new Lens<OpcSelectionViewer.Model, System.Boolean>() with
                    override x.Get(r) = r.intersection
                    override x.Set(r,v) = { r with intersection = v }
                    override x.Update(r,f) = { r with intersection = f r.intersection }
                }
