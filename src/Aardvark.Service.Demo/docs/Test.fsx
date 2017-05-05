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

#r "FShade.Compiler.dll"
#r "FShade.dll"

#r "Aardvark.Base.Rendering.dll"

//#r "Aardvark.SceneGraph.dll"
//#r "Aardvark.Rendering.NanoVg.dll"
//#r "Aardvark.Rendering.GL.dll"
//#r "Aardvark.Application.dll"
//#r "Aardvark.Application.WinForms.dll"
//#r "Aardvark.Application.WinForms.GL.dll"

(**


# Deriving a Domain Specific Language for purely functional 3D Graphics

In this paper, we present a simple yet powerful domain specific language
for working with threedimensional scene data. 
We start by specifying the domain types [(DDD)][ddd] of a example problem, and extend the
domain model with functionality for rendering and interacting with the domain model.

Our first example is little drawing tool which allows to draw polygons on a vertical plane
centered in the 3D scene.
Later we will extend the program with functionality for picking and translating objects
by using a maya style 3D controller.

Note that this document is written as F# literate script, i.e. this document is both, the
paper and the implementation ;)

 [ddd]: http://fsharpforfunandprofit.com/ddd/

## Domain Driven Design 

Let us start with the domain model for polygons:
*)

open Aardvark.Base // provides vector types

type Polygon = list<V3d>

open Aardvark.Base.Incremental

type ID<'a> = 
    abstract member ID : int
    abstract member Self : 'a

type Gabbl = 
    {
        u : int
    } 
    with
        interface ID<Gabbl> with 
            member x.ID = 1
            member x.Self = x

type Urdar =
    {
        blub : int
        gabbl : Gabbl
    }
    with
        interface ID<Urdar> with 
            member x.ID = 1
            member x.Self = x


type Unpersist() =
    member x.Bind(m : ID<'a>, f : 'a -> 'b) : 'b = 
        let m = Mod.init m.Self
        f m.Value
    member x.Bind(v : 'a, f : IMod<'a> -> 'b) : int =
        failwith ""

    member x.Return(v : 'a) = v

let unpersist = Unpersist()

