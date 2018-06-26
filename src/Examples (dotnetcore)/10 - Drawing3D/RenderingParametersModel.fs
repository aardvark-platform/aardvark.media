namespace RenderingParametersModel

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI.Primitives
open Aardvark.Base.Rendering

[<DomainType>]
type RenderingParameters = {
    fillMode : FillMode
    cullMode : CullMode
}

type Action =
    | SetFillMode of FillMode
    | SetCullMode of CullMode

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderingParameters =
    
    let initial =
        {
            fillMode = FillMode.Fill
            cullMode = CullMode.None
        }