namespace Aardvark.ImmutableSceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

[<AutoOpen>]
module PickStuff = 

    let altKey = Keys.LeftAlt
    let ctrlKey = Keys.LeftCtrl
    let shiftKey = Keys.LeftShift

    type KeyEvent = Down of Keys | Up of Keys | NoEvent

    type MouseEvent = Down of MouseButtons | Move | Click of MouseButtons | Up of MouseButtons | NoEvent

    type PickOccurance = { 
        mouse : MouseEvent
        key : KeyEvent
        point : V3d 
        ray : Ray3d
     }

     type NoPick = { mouse : MouseEvent; ray : Ray3d }


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module PickOccurance =
        let position (p : PickOccurance) = p.point

    module Mouse =
        let move (p : PickOccurance) = p.mouse = Move
        let down (p : PickOccurance) = match p.mouse with | Down b -> true | _ -> false  
        let down' (button : MouseButtons) (p : PickOccurance) = match p.mouse with | Down b when b = button -> true | _ -> false

    module Key = 
        let up (key : Keys) (p : PickOccurance) = match p.key with | KeyEvent.Up k when k = key -> true | _ -> false
        let down (key : Keys) (p : PickOccurance) = match p.key with | KeyEvent.Down k when k = key -> true | _ -> false

    type Transparency = Solid | PickThrough
    type PickOperation<'msg> = (PickOccurance -> Option<'msg>) * Transparency

    module Pick =
        let ignore = []

    
    let on (p : PickOccurance -> bool) (r : V3d -> 'msg) : PickOperation<'msg> = 
        (fun pickOcc -> 
            if p pickOcc then 
                Some (r (PickOccurance.position pickOcc))
            else None), Solid

    let anyways (r : Ray3d -> 'msg) : PickOperation<'msg> =
        (fun pickOcc -> Some <| r pickOcc.ray), PickThrough

    let whenever (p : PickOccurance -> bool) (r : Ray3d -> 'msg) : PickOperation<'msg> =
        (fun pickOcc -> if p pickOcc then Some <| r pickOcc.ray else None), PickThrough
            

    let onPickThrough (p : PickOccurance -> bool) (r : V3d -> 'msg) : PickOperation<'msg> = 
        (fun pickOcc -> 
            if p pickOcc then 
                Some (r (PickOccurance.position pickOcc))
            else None), PickThrough
    
    type Hits<'msg> = list<float * list<PickOperation<'msg>>>
    
    type GlobalPick = { mouseEvent : MouseEvent; hits : bool; keyEvent : KeyEvent }
//    module GlobalPick =
//        let map (f : 'a -> 'b) (p : GlobalPick<'a>) =
//            { 
//                ray = p.ray
//                mouseEvent = p.mouseEvent
//                hits =  p.hits |> List.map (fun (d,p) -> (d,p |> List.map (fun pf -> fun p -> pf p |> Option.map f)))
//            }

    module Primitives =

        type Primitive = 
            | Sphere      of Sphere3d
            | Cone        of center : V3d * dir : V3d * height : float * radius : float
            | Cylinder    of center : V3d * dir : V3d * height : float * radius : float
            | Quad        of Quad3d 
            | Everything

        let hitPrimitive (p : Primitive) (trafo : Trafo3d) (ray : Ray3d) action =
            let mutable ha = RayHit3d.MaxRange
            match p with
                | Sphere s -> 
                    let transformed = trafo.Forward.TransformPos(s.Center)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.HitsSphere(transformed,s.Radius,0.0,Double.PositiveInfinity, &ha) then
                        [ray, ha.T, action]
                    else []
                | Cone(center,dir,height,radius) | Cylinder(center,dir,height,radius) -> 
                    let cylinder = Cylinder3d(trafo.Forward.TransformPos center,trafo.Forward.TransformPos (center+dir*height),radius)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.Hits(cylinder,0.0,Double.MaxValue,&ha) then
                        [ray, ha.T, action]
                    else []
                | Quad q -> 
                    let transformed = Quad3d(q.Points |> Seq.map trafo.Forward.TransformPos)
                    if ray.HitsQuad(transformed.P0,transformed.P1,transformed.P2,transformed.P3,0.0,Double.MaxValue,&ha) then [ray, ha.T, action]
                    else []
                | Everything ->
                    [ray, 0.0, action]

        let cylinder c d h r = Cylinder(c,d,h,r)

    module List =
        let updateAt i f xs =
            let rec work current xs =
                match xs with 
                    | x::xs -> 
                        if i = current then f x :: xs
                        else x :: work (current+1) xs
                    | [] -> []
            work 0 xs

open Primitives

type ISg<'msg> = inherit ISg


type AbstractApplicator<'msg>(child : IMod<ISg<'msg>>) =
    interface ISg<'msg>
    interface IApplicator with
        member x.Child = child |> Mod.map (fun a -> a :> ISg)
    member x.Child = child
    new(s : ISg<'msg>) = AbstractApplicator<'msg>(Mod.constant s)

type Group<'msg>(xs : aset<ISg<'msg>>) =
    interface ISg<'msg>
    interface IGroup with
        member x.Children = xs |> ASet.map (fun a -> a :> ISg)

    member x.Children = xs

    new(l : list<ISg<'msg>>) = Group<'msg>(ASet.ofList l)

type Transform<'msg>(trafo : IMod<Trafo3d>, xs : list<ISg<'msg>>) =
    inherit Sg.TrafoApplicator(trafo, Group xs)
    interface ISg<'msg>

type Colored<'msg>(color : IMod<C4b>, xs : list<ISg<'msg>>) =
    inherit AbstractApplicator<'msg>(Group xs)
    interface ISg<'msg>
    member x.Color = color

type On<'msg>(picks : list<PickOperation<'msg>>, children : list<ISg<'msg>>) =
    inherit AbstractApplicator<'msg>(Group children)
    member x.PickOperations = picks
    interface ISg<'msg>

type Leaf<'msg>(xs : Primitive) =
    interface ISg<'msg>
    member x.Primitive = xs

type ViewTrafo<'msg>(v : IMod<Trafo3d>, c : IMod<ISg<'msg>>) =
    inherit AbstractApplicator<'msg>(c)
    member x.ViewTrafo = v
    member x.Child = c

type ProjTrafo<'msg>(v : IMod<Trafo3d>, c : IMod<ISg<'msg>>) =
    inherit AbstractApplicator<'msg>(c)
    member x.ProjTrafo = v
    member x.Child = c

type Conv<'msg>(applicator : ISg -> ISg, children : ISg<'msg>) =
    let child = Mod.constant (applicator children)
    interface ISg<'msg>
    interface IApplicator with
        member x.Child = child
    member x.SceneGraphChild = child
    member x.Children = children

type Map<'a,'b>(f : 'a -> 'b, source : ISg<'a>) =
    interface ISg<'b>
    member x.F = f
    member x.Source = source

open Aardvark.Base.Ag
open Aardvark.SceneGraph.Semantics
[<Semantic>]
type LeafSemantics() =
        
    member x.InhColor(c : Colored<'msg>) =
        c.Child?InhColor <- c.Color

    member x.InhColor(r : Root<ISg>) =
        r.Child?InhColor <- Mod.constant C4b.White

    member x.RenderObjects(c : Conv<'msg>) : aset<IRenderObject> =
        ASet.bind (fun s -> s?RenderObjects()) c.SceneGraphChild 

    member x.RenderObjects(m : Map<_,_>) : aset<IRenderObject> =
        m.Source?RenderObjects()

    member x.RenderObjects(l : Leaf<'msg>) =
        match l.Primitive with
            | Sphere s -> 
                Sg.sphere 5 (l?InhColor) (Mod.constant s.Radius) |> Sg.transform (Trafo3d.Translation s.Center)
                |> Semantic.renderObjects
            | Cone(c,d,h,r) -> 
                l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCone c d h r 10 color |> Sg.ofIndexedGeometry) 
                |> Sg.dynamic
                |> Semantic.renderObjects
            | Cylinder(c,d,h,r) ->
                l?InhColor |> Mod.map (fun (color : C4b) -> IndexedGeometryPrimitives.solidCylinder c d h r r 10 color |> Sg.ofIndexedGeometry) 
                |> Sg.dynamic
                |> Semantic.renderObjects
            | Quad p -> 
                let vertices = p.Points |> Seq.map V3f |> Seq.toArray
                let index = [| 0; 1; 2; 0; 2; 3 |]
                let c : IMod<C4b> = l?InhColor
                let colors = c |> Mod.map  (fun c -> Array.replicate vertices.Length c)
                let normals = Array.replicate vertices.Length (p.Edge03.Cross(p.P2-p.P0)).Normalized
                let ig = IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, vertices :> Array; DefaultSemantic.Normals, normals :> System.Array], SymDict.empty)
                ig
                    |> Sg.ofIndexedGeometry
                    |> Sg.vertexAttribute DefaultSemantic.Colors colors
                    |> Semantic.renderObjects
            | Everything -> ASet.empty

      
      

type PickObject<'msg> =
    {
        modeltrafo : IMod<Trafo3d>
        viewtrafo : IMod<Trafo3d>
        projtrafo : IMod<Trafo3d>
        primitive : Primitive
        actions : list<PickOperation<'msg>>
    }  
        

module PickObject =
    let map ( f : 'a -> 'b) (p : PickObject<'a>) : PickObject<'b> =
        {
            modeltrafo = p.modeltrafo
            viewtrafo = p.viewtrafo
            projtrafo = p.projtrafo
            actions = List.map (fun (pick,transparency) -> (fun kind -> Option.map f (pick kind)),transparency) p.actions
            primitive = p.primitive
        }  


[<AutoOpen>]
module SgExt = 
    type ISg<'msg> with
        member x.PickObjects() : aset<PickObject<'msg>> = x?PickObjects()
        member x.PickOperations : list<PickOperation<'msg>> = x?PickOperations

module Pick =
    let pixel (p : PixelPosition) (modelTrafo : Trafo3d) (viewTrafo : Trafo3d) (projTrafo : Trafo3d) =
        let n = p.NormalizedPosition
        let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
        let dir = projTrafo.Backward.TransformPosProj ndc |> Vec.normalize
        let modelViewTrafo = (modelTrafo * viewTrafo)
        let viewDir = viewTrafo.Backward.TransformDir dir
        let viewLoc = viewTrafo.Backward.TransformPos V3d.OOO
        Ray3d(viewLoc, Vec.normalize viewDir)



[<Semantic>]
type PickingSemantics() =
    

    member x.PickOperations(o : Root<ISg<'msg>>) =
        o.Child?PickOperations <- List.empty<PickOperation<'msg>>

    member x.PickOperations(o : On<'msg>) =
        o.Child?PickOperations <- o.PickOperations

    member x.PickOperations(o : Conv<'msg>) =
        o.Children?PickOperations <- o.PickOperations

    member x.PickObjects(o : Map<'a,'b>) : aset<PickObject<'b>> =
        let pi : aset<PickObject<'a>> = o.Source.PickObjects()
        ASet.map (PickObject.map o.F) pi

    member x.ViewTrafo(v : ViewTrafo<'msg>) =
        v.Child?ViewTrafo <- v.ViewTrafo

    member x.ProjTrafo(v : ProjTrafo<'msg>) =
        v.Child?ProjTrafo <- v.ProjTrafo

    member x.PickObjects(g : Conv<'msg>) : aset<PickObject<'msg>> =
        g.Children?PickObjects()

    member x.PickObjects(g : Group<'msg>) : aset<PickObject<'msg>> =
        g.Children |> ASet.collect (fun s -> s.PickObjects())

    member x.PickObjects(s : Transform<'msg>) : aset<PickObject<'msg>> =
        s.Child |> ASet.bind (fun c -> c?PickObjects())
               
    member x.PickObjects(c : AbstractApplicator<'msg>) : aset<PickObject<'msg>> =
        c.Child |> ASet.bind (fun c -> c?PickObjects())
                   
    member x.PickObjects(l : Leaf<'msg>)  : aset<PickObject<'msg>> =
        match l.PickOperations with
            | [] -> ASet.empty
            | ops -> 
                ASet.single {
                    modeltrafo = l.ModelTrafo
                    viewtrafo = l.ViewTrafo
                    projtrafo = l.ProjTrafo
                    primitive = l.Primitive
                    actions = ops
                }



module Scene =

    let private conv app xs = Conv<_>(app,xs) :> ISg<'msg>
    let transform t xs = Transform<'msg>(t,xs) :> ISg<'msg>
    let translate x y z xs = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, xs) :> ISg<_>
    let translate' x y z c = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, List.singleton c) :> ISg<_>
    let transform' t x = Transform<'msg>(t,[x]) :> ISg<'msg>
    let colored c xs = Colored<'msg>(c,xs) :> ISg<'msg>
    let colored' c x = Colored<'msg>(c,x |> List.singleton) :> ISg<'msg>
    let pick picks xs = On<'msg>(picks,xs) :> ISg<'msg>
    let group (xs : list<_>) = Group<'msg>(xs) :> ISg<'msg>
    let agroup (xs : aset<_>) = Group<'msg>(xs) :> ISg<'msg>
    let leaf x = Leaf<'msg>(x) :> ISg<'msg> 
    let render picks p = pick picks [leaf p]
    let map f (a : ISg<'a>) : ISg<'b> = Map<_,_>(f,a) :> ISg<'b>
    let uniform name value xs = conv (Sg.uniform name value) xs
    let effect effects xs = conv (Sg.effect effects) xs
    let viewTrafo viewTrafo x = ViewTrafo(viewTrafo,Mod.init x) :> ISg<_>
    let projTrafo projTrafo x =  ProjTrafo(projTrafo,Mod.init x) :> ISg<_>
    let camera camera xs = 
        xs |> viewTrafo (camera |> Mod.map Camera.viewTrafo)
           |> projTrafo (camera |> Mod.map Camera.projTrafo)
           |> conv (Sg.camera camera) 
    let camera' c xs = camera c (group xs)

    
