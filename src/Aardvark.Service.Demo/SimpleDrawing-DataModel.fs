(*
This demo shows how to implement a simple drawing app with undo/redo. it is based on aardvark.ui, which uses
elm architecture + svg for drawing stuff (the ideas carry over to real aardvark.media 3d elm coding).
The structure is as follows:
 - SimpleDrawing-DataModel.fs: defines the domain (using DDD https://fsharpforfunandprofit.com/ddd/) in the namespace Simple2DDrawing
 - SimpleDrawing.fs which defines a top level module called Simple2DDrawingApp which opens the Simple2DDrawing namespace.

Aardvark.Compiler.DomainTypes (Which is installed in this project) automatically creates adaptive data types, given types annotated with
[<DomainType>] attribute.
The generated files follow the naming sheme <originalFileName>.g.fs
In this example the generated file is called SimpleDrawing-DataModel.g.fs
Although this process of generating adaptive datatypes is transparent to the user (via msbuild target import), we provide a cmd line utility for 
explicitly generating the *.g files. (TODO aardvark-team: write about Aardvark.Compiler.DomainTypeTool)

*)

// naming convention, extra namespace for domain types (the automatically generated file adds AutoOpen modules to this namespace)
namespace Simple2DDrawing

open Aardvark.Base // stuff like V2d
open Aardvark.Base.Incremental // [<DomainType>] is defined here

// start down with the definition of model

[<DomainType>]
type Polygon = { points : list<V2d> } // polygon is a list of points, we use list<V2d> here, since tracking single points adaptively is kind of overkill here though plist<V2d> would work as well.

(* ok our domain can be modelled as such:
 - we have a list of polygons
 - we have a polygon we are currently working on, i.e. in which we append points 
 - in order to show a preview of the current polygon including its potential next point, we store an option of V2d
   which is updated to store the coordinates of the mouse
 - in order to do undo, we store an optional past of this model
 - in order to do redo, we store an optional future of this model.
 *)

[<DomainType>]
type Model =
    {
        finishedPolygons : plist<Polygon> // we use plist here, since plist is automatically mapped to alists in the adaptive version (See https://rawgit.com/vrvis/aardvark.media/base31/docs/DomainTypeGeneration.html)

        workingPolygon : Option<Polygon> // maybe we have an (unfinished) polygon we are working on
        cursor         : Option<V2d>     // the cursor

        [<TreatAsValue>]        // since we don't want to have automatically maintained incremental versions of the past, we treat this field as value (the adaptive version becoms IMod<Option<Model>> instead of MOption<MModel>)
        past   : Option<Model>  // note that this is for efficiency only
        [<TreatAsValue>]
        future : Option<Model>
    }



type Message = 
    | AddPoint of V2d       // add a point to a given coordinate
    | ClosePolygon of V2d   // close a polygon at coordinate (actually ignored, since in our interaction closing polygons do not create an additional point)
    | MoveCursor of V2d     // called by mousemove further on, in order to always have constistent cursor value
     
    // undo redo action
    | Undo of unit 
    | Redo of unit