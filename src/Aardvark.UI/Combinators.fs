namespace Aardvark.UI

open Aardvark.Base
open FSharp.Data.Adaptive


[<AutoOpen>]
module Combinators =
    let ifElse (m : aval<bool>) (t : Attribute<'msg>) (e : Attribute<'msg>) =
        let (k,tf) = t
        let (k2,ef) = e
        if k <> k2 then failwith "attribute keys must match"
        k, m |> AVal.map (function true -> Some tf | false -> Some ef)

namespace Aardvark.Base

module LensOperators = 
    
    let inline (.*) (l : ^l) (r : ^r) = Lens.Compose(l,r)
    let inline (<==) (l : Lens<'s,'v>) (s : 's, v : 'v) = l.Set(s,v)

    let inline (=&>) (l : Lens<'s,'v>, s : 's) (f : 'v -> 'v) = 
        l.Update(s,f)

