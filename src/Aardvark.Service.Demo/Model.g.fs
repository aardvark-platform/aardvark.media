namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Demo.TestApp

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MUrdar private(__initial : Demo.TestApp.Urdar) =
        let mutable __current = __initial
        let _urdar = ResetMod(__initial.urdar)
        
        member x.urdar = _urdar :> IMod<_>
        
        member x.Update(__model : Demo.TestApp.Urdar) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _urdar.Update(__model.urdar)
        
        static member Update(__self : MUrdar, __model : Demo.TestApp.Urdar) = __self.Update(__model)
        
        static member Create(initial) = MUrdar(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MUrdar =
        let inline urdar (m : MUrdar) = m.urdar
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Urdar =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let urdar =
                { new Lens<Demo.TestApp.Urdar, Microsoft.FSharp.Core.int>() with
                    override x.Get(r) = r.urdar
                    override x.Set(r,v) = { r with urdar = v }
                    override x.Update(r,f) = { r with urdar = f r.urdar }
                }
    [<StructuredFormatDisplay("{AsString}")>]
    [<System.Runtime.CompilerServices.Extension>]
    type MModel private(__initial : Demo.TestApp.Model) =
        let mutable __current = __initial
        let _boxHovered = ResetMod(__initial.boxHovered)
        let _dragging = ResetMod(__initial.dragging)
        let _lastName = ResetMod(__initial.lastName)
        let _elements = ResetList(__initial.elements)
        let _hasD3Hate = ResetMod(__initial.hasD3Hate)
        let _boxScale = ResetMod(__initial.boxScale)
        let _objects = ResetMapMap(__initial.objects, (fun k v -> MUrdar.Create(v)), MUrdar.Update)
        let _lastTime = ResetMod(__initial.lastTime)
        
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.lastName = _lastName :> IMod<_>
        member x.elements = _elements :> alist<_>
        member x.hasD3Hate = _hasD3Hate :> IMod<_>
        member x.boxScale = _boxScale :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        member x.lastTime = _lastTime :> IMod<_>
        
        member x.Update(__model : Demo.TestApp.Model) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _boxHovered.Update(__model.boxHovered)
                _dragging.Update(__model.dragging)
                _lastName.Update(__model.lastName)
                _elements.Update(__model.elements)
                _hasD3Hate.Update(__model.hasD3Hate)
                _boxScale.Update(__model.boxScale)
                _objects.Update(__model.objects)
                _lastTime.Update(__model.lastTime)
        
        static member Update(__self : MModel, __model : Demo.TestApp.Model) = __self.Update(__model)
        
        static member Create(initial) = MModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MModel =
        let inline boxHovered (m : MModel) = m.boxHovered
        let inline dragging (m : MModel) = m.dragging
        let inline lastName (m : MModel) = m.lastName
        let inline elements (m : MModel) = m.elements
        let inline hasD3Hate (m : MModel) = m.hasD3Hate
        let inline boxScale (m : MModel) = m.boxScale
        let inline objects (m : MModel) = m.objects
        let inline lastTime (m : MModel) = m.lastTime
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Model =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
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
            let objects =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.hmap<Microsoft.FSharp.Core.string, Demo.TestApp.Urdar>>() with
                    override x.Get(r) = r.objects
                    override x.Set(r,v) = { r with objects = v }
                    override x.Update(r,f) = { r with objects = f r.objects }
                }
            let lastTime =
                { new Lens<Demo.TestApp.Model, Aardvark.Base.MicroTime>() with
                    override x.Get(r) = r.lastTime
                    override x.Set(r,v) = { r with lastTime = v }
                    override x.Update(r,f) = { r with lastTime = f r.lastTime }
                }
