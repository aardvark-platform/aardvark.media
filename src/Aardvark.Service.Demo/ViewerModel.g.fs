namespace Viewer

open System
open Aardvark.Base
open Aardvark.Base.Incremental

[<AutoOpen>]
module Mutable =

    [<StructuredFormatDisplay("{AsString}")>]
    type MViewerModel private(__initial : Viewer.ViewerModel) =
        let mutable __current = __initial
        let _file = ResetMod(__initial.file)
        
        member x.file = _file :> IMod<_>
        
        member x.Update(__model : Viewer.ViewerModel) =
            if not (Object.ReferenceEquals(__model, __current)) then
                __current <- __model
                _file.Update(__model.file)
        
        static member Create(initial) = MViewerModel(initial)
        
        override x.ToString() = __current.ToString()
        member private x.AsString = sprintf "%A" __current
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MViewerModel =
        let inline file (m : MViewerModel) = m.file
    
    
    
    
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ViewerModel =
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Lens =
            let file =
                { new Lens<Viewer.ViewerModel, Microsoft.FSharp.Core.Option<Microsoft.FSharp.Core.string>>() with
                    override x.Get(r) = r.file
                    override x.Set(r,v) = { r with file = v }
                    override x.Update(r,f) = { r with file = f r.file }
                }
