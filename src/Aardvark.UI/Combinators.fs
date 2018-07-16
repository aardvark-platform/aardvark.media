namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Incremental


[<AutoOpen>]
module Combinators =
    let ifElse (m : IMod<bool>) (t : Attribute<'msg>) (e : Attribute<'msg>) =
        let (k,tf) = t
        let (k2,ef) = e
        if k <> k2 then failwith "attribute keys must match"
        k, m |> Mod.map (function true -> Some tf | false -> Some ef)

namespace Aardvark.Base

module LensOperators = 
    
    let inline (.*) (l : ^l) (r : ^r) = Lens.Compose(l,r)
    let inline (<==) (l : Lens<'s,'v>) (s : 's, v : 'v) = l.Set(s,v)

    let inline (=&>) (l : Lens<'s,'v>, s : 's) (f : 'v -> 'v) = 
        l.Update(s,f)

