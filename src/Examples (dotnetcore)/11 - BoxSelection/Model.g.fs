namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MVisibleBox(__initial : Model.VisibleBox) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.VisibleBox> = Aardvark.Base.Incremental.EqModRef<Model.VisibleBox>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.VisibleBox>
        let _geometry = ResetMod.Create(__initial.geometry)
        let _color = ResetMod.Create(__initial.color)
        
        member x.geometry = _geometry :> IMod<_>
        member x.color = _color :> IMod<_>
        member x.id = __current.Value.id
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.VisibleBox) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                ResetMod.Update(_geometry,v.geometry)
                ResetMod.Update(_color,v.color)
                
        
        static member Create(__initial : Model.VisibleBox) : MVisibleBox = MVisibleBox(__initial)
        static member Update(m : MVisibleBox, v : Model.VisibleBox) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.VisibleBox> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module VisibleBox =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let geometry =
                { new Lens<Model.VisibleBox, Aardvark.Base.Box3d>() with
                    override x.Get(r) = r.geometry
                    override x.Set(r,v) = { r with geometry = v }
                    override x.Update(r,f) = { r with geometry = f r.geometry }
                }
            let color =
                { new Lens<Model.VisibleBox, Aardvark.Base.C4b>() with
                    override x.Get(r) = r.color
                    override x.Set(r,v) = { r with color = v }
                    override x.Update(r,f) = { r with color = f r.color }
                }
            let id =
                { new Lens<Model.VisibleBox, Microsoft.FSharp.Core.string>() with
                    override x.Get(r) = r.id
                    override x.Set(r,v) = { r with id = v }
                    override x.Update(r,f) = { r with id = f r.id }
                }
    
    
    type MBoxSelectionDemoModel(__initial : Model.BoxSelectionDemoModel) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.BoxSelectionDemoModel> = Aardvark.Base.Incremental.EqModRef<Model.BoxSelectionDemoModel>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.BoxSelectionDemoModel>
        let _camera = Aardvark.UI.Primitives.Mutable.MCameraControllerState.Create(__initial.camera)
        let _rendering = RenderingParametersModel.Mutable.MRenderingParameters.Create(__initial.rendering)
        let _boxes = MList.Create(__initial.boxes, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxesSet = MSet.Create((fun (v : Model.VisibleBox) -> v.id :> obj), __initial.boxesSet, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxesMap = MMap.Create(__initial.boxesMap, (fun v -> MVisibleBox.Create(v)), (fun (m,v) -> MVisibleBox.Update(m, v)), (fun v -> v))
        let _boxHovered = MOption.Create(__initial.boxHovered)
        let _selectedBoxes = MSet.Create(__initial.selectedBoxes)
        
        member x.camera = _camera
        member x.rendering = _rendering
        member x.boxes = _boxes :> alist<_>
        member x.boxesSet = _boxesSet :> aset<_>
        member x.boxesMap = _boxesMap :> amap<_,_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.selectedBoxes = _selectedBoxes :> aset<_>
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.BoxSelectionDemoModel) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                Aardvark.UI.Primitives.Mutable.MCameraControllerState.Update(_camera, v.camera)
                RenderingParametersModel.Mutable.MRenderingParameters.Update(_rendering, v.rendering)
                MList.Update(_boxes, v.boxes)
                MSet.Update(_boxesSet, v.boxesSet)
                MMap.Update(_boxesMap, v.boxesMap)
                MOption.Update(_boxHovered, v.boxHovered)
                MSet.Update(_selectedBoxes, v.selectedBoxes)
                
        
        static member Create(__initial : Model.BoxSelectionDemoModel) : MBoxSelectionDemoModel = MBoxSelectionDemoModel(__initial)
        static member Update(m : MBoxSelectionDemoModel, v : Model.BoxSelectionDemoModel) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.BoxSelectionDemoModel> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module BoxSelectionDemoModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let camera =
                { new Lens<Model.BoxSelectionDemoModel, Aardvark.UI.Primitives.CameraControllerState>() with
                    override x.Get(r) = r.camera
                    override x.Set(r,v) = { r with camera = v }
                    override x.Update(r,f) = { r with camera = f r.camera }
                }
            let rendering =
                { new Lens<Model.BoxSelectionDemoModel, RenderingParametersModel.RenderingParameters>() with
                    override x.Get(r) = r.rendering
                    override x.Set(r,v) = { r with rendering = v }
                    override x.Update(r,f) = { r with rendering = f r.rendering }
                }
            let boxes =
                { new Lens<Model.BoxSelectionDemoModel, Aardvark.Base.plist<Model.VisibleBox>>() with
                    override x.Get(r) = r.boxes
                    override x.Set(r,v) = { r with boxes = v }
                    override x.Update(r,f) = { r with boxes = f r.boxes }
                }
            let boxesSet =
                { new Lens<Model.BoxSelectionDemoModel, Aardvark.Base.hset<Model.VisibleBox>>() with
                    override x.Get(r) = r.boxesSet
                    override x.Set(r,v) = { r with boxesSet = v }
                    override x.Update(r,f) = { r with boxesSet = f r.boxesSet }
                }
            let boxesMap =
                { new Lens<Model.BoxSelectionDemoModel, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,Model.VisibleBox>>() with
                    override x.Get(r) = r.boxesMap
                    override x.Set(r,v) = { r with boxesMap = v }
                    override x.Update(r,f) = { r with boxesMap = f r.boxesMap }
                }
            let boxHovered =
                { new Lens<Model.BoxSelectionDemoModel, Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
            let selectedBoxes =
                { new Lens<Model.BoxSelectionDemoModel, Aardvark.Base.hset<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.selectedBoxes
                    override x.Set(r,v) = { r with selectedBoxes = v }
                    override x.Update(r,f) = { r with selectedBoxes = f r.selectedBoxes }
                }
