namespace Model

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Model

[<AutoOpen>]
module Mutable =

    
    
    type MModel(__initial : Model.Model) =
        inherit obj()
        let mutable __current : Aardvark.Base.Incremental.IModRef<Model.Model> = Aardvark.Base.Incremental.EqModRef<Model.Model>(__initial) :> Aardvark.Base.Incremental.IModRef<Model.Model>
        let _dragInfo = MOption.Create(__initial.dragInfo)
        let _pos = ResetMod.Create(__initial.pos)
        let _dragMode = ResetMod.Create(__initial.dragMode)
        let _stepSize = Aardvark.UI.Mutable.MNumericInput.Create(__initial.stepSize)
        
        member x.dragInfo = _dragInfo :> IMod<_>
        member x.pos = _pos :> IMod<_>
        member x.dragMode = _dragMode :> IMod<_>
        member x.stepSize = _stepSize
        
        member x.Current = __current :> IMod<_>
        member x.Update(v : Model.Model) =
            if not (System.Object.ReferenceEquals(__current.Value, v)) then
                __current.Value <- v
                
                MOption.Update(_dragInfo, v.dragInfo)
                ResetMod.Update(_pos,v.pos)
                ResetMod.Update(_dragMode,v.dragMode)
                Aardvark.UI.Mutable.MNumericInput.Update(_stepSize, v.stepSize)
                
        
        static member Create(__initial : Model.Model) : MModel = MModel(__initial)
        static member Update(m : MModel, v : Model.Model) = m.Update(v)
        
        override x.ToString() = __current.Value.ToString()
        member x.AsString = sprintf "%A" __current.Value
        interface IUpdatable<Model.Model> with
            member x.Update v = x.Update v
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let dragInfo =
                { new Lens<Model.Model, Microsoft.FSharp.Core.Option<Model.DragInfo>>() with
                    override x.Get(r) = r.dragInfo
                    override x.Set(r,v) = { r with dragInfo = v }
                    override x.Update(r,f) = { r with dragInfo = f r.dragInfo }
                }
            let pos =
                { new Lens<Model.Model, Aardvark.Base.V2d>() with
                    override x.Get(r) = r.pos
                    override x.Set(r,v) = { r with pos = v }
                    override x.Update(r,f) = { r with pos = f r.pos }
                }
            let dragMode =
                { new Lens<Model.Model, Model.DragMode>() with
                    override x.Get(r) = r.dragMode
                    override x.Set(r,v) = { r with dragMode = v }
                    override x.Update(r,f) = { r with dragMode = f r.dragMode }
                }
            let stepSize =
                { new Lens<Model.Model, Aardvark.UI.NumericInput>() with
                    override x.Get(r) = r.stepSize
                    override x.Set(r,v) = { r with stepSize = v }
                    override x.Update(r,f) = { r with stepSize = f r.stepSize }
                }
