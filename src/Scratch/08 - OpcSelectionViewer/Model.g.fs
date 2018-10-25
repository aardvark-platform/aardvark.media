namespace OpcSelectionViewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open OpcSelectionViewer

[<AutoOpen>]
module Mutable =

    
    
    type MDipAndStrikeResults(__initial : OpcSelectionViewer.DipAndStrikeResults) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.DipAndStrikeResults> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.DipAndStrikeResults>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.DipAndStrikeResults>
        let _plane = ResetMod.Create(__initial.plane)
        let _dipAngle = ResetMod.Create(__initial.dipAngle)
        let _dipDirection = ResetMod.Create(__initial.dipDirection)
        let _strikeDirection = ResetMod.Create(__initial.strikeDirection)
        let _dipAzimuth = ResetMod.Create(__initial.dipAzimuth)
        let _strikeAzimuth = ResetMod.Create(__initial.strikeAzimuth)
        let _centerOfMass = ResetMod.Create(__initial.centerOfMass)
        
        member x.plane = _plane :> IMod<_>
        member x.dipAngle = _dipAngle :> IMod<_>
        member x.dipDirection = _dipDirection :> IMod<_>
        member x.strikeDirection = _strikeDirection :> IMod<_>
        member x.dipAzimuth = _dipAzimuth :> IMod<_>
        member x.strikeAzimuth = _strikeAzimuth :> IMod<_>
        member x.centerOfMass = _centerOfMass :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.DipAndStrikeResults) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_plane,v.plane)
                ResetMod.Update(_dipAngle,v.dipAngle)
                ResetMod.Update(_dipDirection,v.dipDirection)
                ResetMod.Update(_strikeDirection,v.strikeDirection)
                ResetMod.Update(_dipAzimuth,v.dipAzimuth)
                ResetMod.Update(_strikeAzimuth,v.strikeAzimuth)
                ResetMod.Update(_centerOfMass,v.centerOfMass)
                
        
        static member Create(__initial : OpcSelectionViewer.DipAndStrikeResults) : MDipAndStrikeResults = MDipAndStrikeResults(__initial)
        static member Update(m : MDipAndStrikeResults, v : OpcSelectionViewer.DipAndStrikeResults) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.DipAndStrikeResults> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DipAndStrikeResults =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let plane =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, Aardvark.Base.Plane3d>() with
                    override x.Get(r) = r.plane
                    override x.Set(r,v) = { r with plane = v }
                    override x.Update(r,f) = { r with plane = f r.plane }
                }
            let dipAngle =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, System.Double>() with
                    override x.Get(r) = r.dipAngle
                    override x.Set(r,v) = { r with dipAngle = v }
                    override x.Update(r,f) = { r with dipAngle = f r.dipAngle }
                }
            let dipDirection =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, Aardvark.Base.V3d>() with
                    override x.Get(r) = r.dipDirection
                    override x.Set(r,v) = { r with dipDirection = v }
                    override x.Update(r,f) = { r with dipDirection = f r.dipDirection }
                }
            let strikeDirection =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, Aardvark.Base.V3d>() with
                    override x.Get(r) = r.strikeDirection
                    override x.Set(r,v) = { r with strikeDirection = v }
                    override x.Update(r,f) = { r with strikeDirection = f r.strikeDirection }
                }
            let dipAzimuth =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, System.Double>() with
                    override x.Get(r) = r.dipAzimuth
                    override x.Set(r,v) = { r with dipAzimuth = v }
                    override x.Update(r,f) = { r with dipAzimuth = f r.dipAzimuth }
                }
            let strikeAzimuth =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, System.Double>() with
                    override x.Get(r) = r.strikeAzimuth
                    override x.Set(r,v) = { r with strikeAzimuth = v }
                    override x.Update(r,f) = { r with strikeAzimuth = f r.strikeAzimuth }
                }
            let centerOfMass =
                { new Lens<OpcSelectionViewer.DipAndStrikeResults, Aardvark.Base.V3d>() with
                    override x.Get(r) = r.centerOfMass
                    override x.Set(r,v) = { r with centerOfMass = v }
                    override x.Update(r,f) = { r with centerOfMass = f r.centerOfMass }
                }
    
    
    type MDipAndStrike(__initial : OpcSelectionViewer.DipAndStrike) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.DipAndStrike> = Aardvark.Base.Incremental.EqModRef<OpcSelectionViewer.DipAndStrike>(__initial) :> Aardvark.Base.Incremental.IModRef<OpcSelectionViewer.DipAndStrike>
        let _results = MOption.Create(__initial.results, (fun v -> MDipAndStrikeResults.Create(v)), (fun (m,v) -> MDipAndStrikeResults.Update(m, v)), (fun v -> v))
        let _points = MList.Create(__initial.points)
        
        member x.results = _results :> IMod<_>
        member x.points = _points :> alist<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.DipAndStrike) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MOption.Update(_results, v.results)
                MList.Update(_points, v.points)
                
        
        static member Create(__initial : OpcSelectionViewer.DipAndStrike) : MDipAndStrike = MDipAndStrike(__initial)
        static member Update(m : MDipAndStrike, v : OpcSelectionViewer.DipAndStrike) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<OpcSelectionViewer.DipAndStrike> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DipAndStrike =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let results =
                { new Lens<OpcSelectionViewer.DipAndStrike, Microsoft.FSharp.Core.Option<OpcSelectionViewer.DipAndStrikeResults>>() with
                    override x.Get(r) = r.results
                    override x.Set(r,v) = { r with results = v }
                    override x.Update(r,f) = { r with results = f r.results }
                }
            let points =
                { new Lens<OpcSelectionViewer.DipAndStrike, Aardvark.Base.plist<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.points
                    override x.Set(r,v) = { r with points = v }
                    override x.Update(r,f) = { r with points = f r.points }
                }
    
    
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
        let _distance = MOption.Create(__initial.distance)
        let _line = MOption.Create(__initial.line)
        let _fillMode = ResetMod.Create(__initial.fillMode)
        let _renderLine = ResetMod.Create(__initial.renderLine)
        let _showRay = MOption.Create(__initial.showRay)
        let _teleportTrafo = MOption.Create(__initial.teleportTrafo)
        let _teleportBeacon = MOption.Create(__initial.teleportBeacon)
        let _picked = MMap.Create(__initial.picked)
        let _opcInfos = MMap.Create(__initial.opcInfos, (fun v -> MOpcData.Create(v)), (fun (m,v) -> MOpcData.Update(m, v)), (fun v -> v))
        let _boxes = ResetMod.Create(__initial.boxes)
        let _lines = ResetMod.Create(__initial.lines)
        let _intersectionPoints = ResetMod.Create(__initial.intersectionPoints)
        let _workingDns = MOption.Create(__initial.workingDns, (fun v -> MDipAndStrike.Create(v)), (fun (m,v) -> MDipAndStrike.Update(m, v)), (fun v -> v))
        let _initialTransform = ResetMod.Create(__initial.initialTransform)
        let _finalTransform = ResetMod.Create(__initial.finalTransform)
        let _threads = ResetMod.Create(__initial.threads)
        let _intersection = ResetMod.Create(__initial.intersection)
        
        member x.cameraState = _cameraState
        member x.distance = _distance :> IMod<_>
        member x.line = _line :> IMod<_>
        member x.fillMode = _fillMode :> IMod<_>
        member x.renderLine = _renderLine :> IMod<_>
        member x.showRay = _showRay :> IMod<_>
        member x.teleportTrafo = _teleportTrafo :> IMod<_>
        member x.teleportBeacon = _teleportBeacon :> IMod<_>
        member x.picked = _picked :> amap<_,_>
        member x.opcInfos = _opcInfos :> amap<_,_>
        member x.patchHierarchies = __current.Value.patchHierarchies
        member x.kdTrees = __current.Value.kdTrees
        member x.kdTrees2 = __current.Value.kdTrees2
        member x.boxes = _boxes :> IMod<_>
        member x.lines = _lines :> IMod<_>
        member x.intersectionPoints = _intersectionPoints :> IMod<_>
        member x.workingDns = _workingDns :> IMod<_>
        member x.initialTransform = _initialTransform :> IMod<_>
        member x.finalTransform = _finalTransform :> IMod<_>
        member x.threads = _threads :> IMod<_>
        member x.intersection = _intersection :> IMod<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : OpcSelectionViewer.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_cameraState, v.cameraState)
                MOption.Update(_distance, v.distance)
                MOption.Update(_line, v.line)
                ResetMod.Update(_fillMode,v.fillMode)
                ResetMod.Update(_renderLine,v.renderLine)
                MOption.Update(_showRay, v.showRay)
                MOption.Update(_teleportTrafo, v.teleportTrafo)
                MOption.Update(_teleportBeacon, v.teleportBeacon)
                MMap.Update(_picked, v.picked)
                MMap.Update(_opcInfos, v.opcInfos)
                ResetMod.Update(_boxes,v.boxes)
                ResetMod.Update(_lines,v.lines)
                ResetMod.Update(_intersectionPoints,v.intersectionPoints)
                MOption.Update(_workingDns, v.workingDns)
                ResetMod.Update(_initialTransform,v.initialTransform)
                ResetMod.Update(_finalTransform,v.finalTransform)
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
            let distance =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<System.String>>() with
                    override x.Get(r) = r.distance
                    override x.Set(r,v) = { r with distance = v }
                    override x.Update(r,f) = { r with distance = f r.distance }
                }
            let line =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.Line3d>>() with
                    override x.Get(r) = r.line
                    override x.Set(r,v) = { r with line = v }
                    override x.Update(r,f) = { r with line = f r.line }
                }
            let fillMode =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Rendering.FillMode>() with
                    override x.Get(r) = r.fillMode
                    override x.Set(r,v) = { r with fillMode = v }
                    override x.Update(r,f) = { r with fillMode = f r.fillMode }
                }
            let renderLine =
                { new Lens<OpcSelectionViewer.Model, System.Boolean>() with
                    override x.Get(r) = r.renderLine
                    override x.Set(r,v) = { r with renderLine = v }
                    override x.Update(r,f) = { r with renderLine = f r.renderLine }
                }
            let showRay =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<System.Int32>>() with
                    override x.Get(r) = r.showRay
                    override x.Set(r,v) = { r with showRay = v }
                    override x.Update(r,f) = { r with showRay = f r.showRay }
                }
            let teleportTrafo =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.Trafo3d>>() with
                    override x.Get(r) = r.teleportTrafo
                    override x.Set(r,v) = { r with teleportTrafo = v }
                    override x.Update(r,f) = { r with teleportTrafo = f r.teleportTrafo }
                }
            let teleportBeacon =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.teleportBeacon
                    override x.Set(r,v) = { r with teleportBeacon = v }
                    override x.Update(r,f) = { r with teleportBeacon = f r.teleportBeacon }
                }
            let picked =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.hmap<System.Int32,Aardvark.Base.V3d>>() with
                    override x.Get(r) = r.picked
                    override x.Set(r,v) = { r with picked = v }
                    override x.Update(r,f) = { r with picked = f r.picked }
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
            let kdTrees =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<(Aardvark.Base.Geometry.KdTree<Aardvark.Base.Triangle3d> * Aardvark.Base.Trafo3d)>>() with
                    override x.Get(r) = r.kdTrees
                    override x.Set(r,v) = { r with kdTrees = v }
                    override x.Update(r,f) = { r with kdTrees = f r.kdTrees }
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
            let lines =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Collections.List<Aardvark.Base.Line3d>>() with
                    override x.Get(r) = r.lines
                    override x.Set(r,v) = { r with lines = v }
                    override x.Update(r,f) = { r with lines = f r.lines }
                }
            let intersectionPoints =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.V3f[]>() with
                    override x.Get(r) = r.intersectionPoints
                    override x.Set(r,v) = { r with intersectionPoints = v }
                    override x.Update(r,f) = { r with intersectionPoints = f r.intersectionPoints }
                }
            let workingDns =
                { new Lens<OpcSelectionViewer.Model, Microsoft.FSharp.Core.Option<OpcSelectionViewer.DipAndStrike>>() with
                    override x.Get(r) = r.workingDns
                    override x.Set(r,v) = { r with workingDns = v }
                    override x.Update(r,f) = { r with workingDns = f r.workingDns }
                }
            let initialTransform =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.initialTransform
                    override x.Set(r,v) = { r with initialTransform = v }
                    override x.Update(r,f) = { r with initialTransform = f r.initialTransform }
                }
            let finalTransform =
                { new Lens<OpcSelectionViewer.Model, Aardvark.Base.Trafo3d>() with
                    override x.Get(r) = r.finalTransform
                    override x.Set(r,v) = { r with finalTransform = v }
                    override x.Update(r,f) = { r with finalTransform = f r.finalTransform }
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
