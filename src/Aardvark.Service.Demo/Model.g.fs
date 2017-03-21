namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MModel private(__initial : Demo.TestApp.Model) =
        let mutable __current = __initial
        let _lastName = ResetMod(__initial.lastName)
        let _elements = ResetList(__initial.elements)
        let _hasD3Hate = ResetMod(__initial.hasD3Hate)
        let _boxScale = ResetMod(__initial.boxScale)
        let _boxHovered = ResetMod(__initial.boxHovered)
        let _dragging = ResetMod(__initial.dragging)
        
        member x.lastName = _lastName :> IMod<_>
        member x.elements = _elements :> alist<_>
        member x.hasD3Hate = _hasD3Hate :> IMod<_>
        member x.boxScale = _boxScale :> IMod<_>
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        
        member x.Update(__model : Demo.TestApp.Model) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _lastName.Update(__model.lastName)
                _elements.Update(__model.elements)
                _hasD3Hate.Update(__model.hasD3Hate)
                _boxScale.Update(__model.boxScale)
                _boxHovered.Update(__model.boxHovered)
                _dragging.Update(__model.dragging)
        
        static member Create(initial) = MModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MModel =
        let inline lastName (m : MModel) = m.lastName
        let inline elements (m : MModel) = m.elements
        let inline hasD3Hate (m : MModel) = m.hasD3Hate
        let inline boxScale (m : MModel) = m.boxScale
        let inline boxHovered (m : MModel) = m.boxHovered
        let inline dragging (m : MModel) = m.dragging
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let lastName =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.Option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.lastName
                    override x.Set(r,v) = { r with lastName = v }
                    override x.Update(r,f) = { r with lastName = f r.lastName }
                }
            let elements =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.plist<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.elements
                    override x.Set(r,v) = { r with elements = v }
                    override x.Update(r,f) = { r with elements = f r.elements }
                }
            let hasD3Hate =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.hasD3Hate
                    override x.Set(r,v) = { r with hasD3Hate = v }
                    override x.Update(r,f) = { r with hasD3Hate = f r.hasD3Hate }
                }
            let boxScale =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.float>() with
                    override x.Get(r) = r.boxScale
                    override x.Set(r,v) = { r with boxScale = v }
                    override x.Update(r,f) = { r with boxScale = f r.boxScale }
                }
            let boxHovered =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.boxHovered
                    override x.Set(r,v) = { r with boxHovered = v }
                    override x.Update(r,f) = { r with boxHovered = f r.boxHovered }
                }
            let dragging =
                { new Lens<Demo.TestApp.Model, Microsoft.FSharp.Core.bool>() with
                    override x.Get(r) = r.dragging
                    override x.Set(r,v) = { r with dragging = v }
                    override x.Update(r,f) = { r with dragging = f r.dragging }
                }
