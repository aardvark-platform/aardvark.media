namespace Viewer

open Aardvark.Base
open Aardvark.Base.Incremental

[<DomainType>]
type ViewerModel =
    {
        file : Option<string>
    }
