(***hide***)
#I "../../../bin/Release"
#r "Aardvark.Base.dll" 
#r "Aardvark.Base.TypeProviders.dll"

#r "System.Reactive.Core.dll"
#r "System.Reactive.Interfaces.dll"
#r "System.Reactive.Linq.dll"
#r "DevILSharp.dll"

#r "System.Numerics.dll"
#r "System.Drawing.dll"
#r "System.Data.Linq.dll"
#r "System.Data.dll"
#r "System.Collections.Immutable.dll"
#r "System.ComponentModel.Composition.dll"

#r "Aardvark.Base.Essentials.dll"
#r "Aardvark.Base.FSharp.dll"
#r "Aardvark.Base.Incremental.dll"
#r "Aardvark.Base.Runtime.dll"

#r "Aardvark.Compiler.DomainTypes.dll"

#r "FShade.Core.dll"
#r "FShade.GLSL.dll"
#r "FShade.Imperative.dll"

#r "Aardvark.Base.Rendering.dll"

#r "Aardvark.SceneGraph.dll"
#r "Aardvark.Rendering.NanoVg.dll"
#r "Aardvark.Rendering.GL.dll"
#r "Aardvark.Application.dll"
#r "Aardvark.Application.WinForms.dll"
#r "Aardvark.Application.WinForms.GL.dll"

#r "Aardvark.UI.dll"
#r "Aardvark.UI.Primitives.dll"

open Aardvark.Base
open Aardvark.Base.Incremental

(**


# Automatic diffing for immutable types using Aardvark.Compiler.DomainTypes

In order to extract incremental changes from arbitrary operations performed on a purely functional data structure, we use automatically generated types which provide:
 
 - Computing the change set of two different states of a immutable object (e.g. the old state and the new state after applying some operations)
 - Supplying those changes to a mutable target object.

For each (possible nested) immutable type `t`, we create a new type `mt` which has one operation:
 
 - update(newImmutableValue : 't) : unit

In our implementation, we call immutable types which have an associated mutable type `domain type`. Our F# plugin `Aardvark.Compiler.DomainTypes`
automatically rewrites the original types (annotated with `dommain type attribute) and creates the mutable types under the
hood.

while for each field, the new type provides a field containing the mutable representation of the original field.
Let us consider a simple example:
*)

type Object = { position : V3d }

type Scene = 
    {
        name   : string
        object : Object
    }

(**
The Generated mutable types would be:
*)

type MObject = { postion : IMod<V3d> } // field replaced by mutable variant (V3d is no domain type, make it a mod)

type MScene = 
    {
        name : IMod<string> // field replaced by mutable variant (V3d is no domain type, make it a mod)
        object : MObject // there is a mutable representation of Objects, so use this one instead of the less efficient version IMod<Object>.
    } with member x.Update(n : Scene) = failwith "update mutable state with new immutable value"

(**
This translation allows you to work with purely functional immutable data and feed the updated values to a mutable representation, 
which can be updated automatically and provides modifiables which can be used to build dependency graphs.

For some types such as lists we provide special implementations which make use of efficient adaptive implementations of those datastructures.
Next we show the complete translation sheme for domain types.

| Type                                                     | Mutable Type         | 
| -------------------------------------------------------- |:--------------------:|
| Record type `t` marked with domain type attribute        | mutable type `mt`    | 
| persistent hash set: `hset<'a>`     | `aset<'a>`      | 
| persistent list `hlist<'a>`         | `alist<'a>`     |  
| persistent hash map: `hmap<'k,'v>`  | `amap<'k,'v>`   |
| F# option type: `option<'a>`        | `MOption<'a>` with active patterns: `MSome(v)` and `MNone`  |
| Union type: `type U = A1 | .. | An` | Mutable types, active patterns: `MA1 .. MAn` |
| all other types `'t`                | `IMod<'t>` |


There are currently two possibilities to adjust this translation process to your needs:

 - Sometimes, it is useful to make exceptions of this sheme for some fields. For example, for implementing undo
we often use a single field `past : 'model` which contains the complete model of the previous state. 
Here we would like to break finegrained diffing and use a changeable value `past : IMod<'model>` instead.
This can be achieved by marking the `past` field in the type definition with the `[<TreatAsValue>]` attribute.
 - For custom types, contained in hash containers (e.g. `hset<'k>,hmap<'k,_>`) is necessary to provide the
   diff generator with a mechanism to extract a hash value from the type `'k`. This can be achieved, by
   adding a hashable field (which has a type with identity, i.e. a reference type) and marking this field
   with the `[<PrimaryKey>]` attribute.



*)