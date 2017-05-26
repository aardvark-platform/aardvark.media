namespace Demo.TestApp

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Demo.TestApp

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MUrdar(__initial : Demo.TestApp.Urdar) = 
        let mutable __current = __initial
        let _urdar = ResetMod.Create(__initial.urdar)
        
        member x.urdar = _urdar :> IMod<_>
        
        member x.Update(v : Demo.TestApp.Urdar) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_urdar,v.urdar)
        
        static member Create(v : Demo.TestApp.Urdar) = MUrdar(v)
        static member Update(m : MUrdar, v : Demo.TestApp.Urdar) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
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
    type MModel(__initial : Demo.TestApp.Model) = 
        let mutable __current = __initial
        let _boxHovered = ResetMod.Create(__initial.boxHovered)
        let _dragging = ResetMod.Create(__initial.dragging)
        let _lastName = MOption.Create(__initial.lastName)
        let _elements = MList.Create(__initial.elements)
        let _hasD3Hate = ResetMod.Create(__initial.hasD3Hate)
        let _boxScale = ResetMod.Create(__initial.boxScale)
        let _objects = MMap.Create(__initial.objects, (fun v -> MUrdar.Create(v)), (fun (m,v) -> MUrdar.Update(m, v)), (fun v -> v))
        let _lastTime = ResetMod.Create(__initial.lastTime)
        
        member x.boxHovered = _boxHovered :> IMod<_>
        member x.dragging = _dragging :> IMod<_>
        member x.lastName = _lastName :> IMod<_>
        member x.elements = _elements :> alist<_>
        member x.hasD3Hate = _hasD3Hate :> IMod<_>
        member x.boxScale = _boxScale :> IMod<_>
        member x.objects = _objects :> amap<_,_>
        member x.lastTime = _lastTime :> IMod<_>
        
        member x.Update(v : Demo.TestApp.Model) =
            if not (System.Object.ReferenceEquals(__current, v)) then
                __current <- v
                
                ResetMod.Update(_boxHovered,v.boxHovered)
                ResetMod.Update(_dragging,v.dragging)
                MOption.Update(_lastName, v.lastName)
                MList.Update(_elements, v.elements)
                ResetMod.Update(_hasD3Hate,v.hasD3Hate)
                ResetMod.Update(_boxScale,v.boxScale)
                MMap.Update(_objects, v.objects)
                ResetMod.Update(_lastTime,v.lastTime)
        
        static member Create(v : Demo.TestApp.Model) = MModel(v)
        static member Update(m : MModel, v : Demo.TestApp.Model) = m.Update(v)
        
        override x.ToString() = __current.ToString()
        member x.AsString = sprintf "%A" __current
    
    
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
                { new Lens<Demo.TestApp.Model, Aardvark.Base.hmap<Microsoft.FSharp.Core.string,Demo.TestApp.Urdar>>() with
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
