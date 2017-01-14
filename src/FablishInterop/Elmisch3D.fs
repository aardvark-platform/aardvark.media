namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

[<AutoOpen>]
module PickStuff = 
    type Kind = Move of V3d | Down of MouseButtons * V3d

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Event =
        let move = function Move _ -> true | _ -> false
        let down = function Down _ -> true | _ -> false       
        let down' p = function Down(p',_) when p = p' -> true | _ -> false 
        let position = function Move s -> s | Down(_, s) -> s

    type PickOperation<'msg> = Kind -> Option<'msg>

    module Pick =
        let ignore = []
        let map f (p : PickOperation<'a>) =
            fun k -> 
                match p k with
                    | Some r -> Some (f r)
                    | None -> None

    
    let on (p : Kind -> bool) (r : V3d -> 'msg) (k : Kind) = if p k then Some (r (Event.position k)) else None

    type MouseEvent = Down of MouseButtons | Move | Click of MouseButtons | Up of MouseButtons
    type NoPick = NoPick of MouseEvent * Ray3d

    module Primitives =

        type Primitive = 
            | Sphere      of Sphere3d
            | Cone        of center : V3d * dir : V3d * height : float * radius : float
            | Cylinder    of center : V3d * dir : V3d * height : float * radius : float
            | Quad        of Quad3d 

        let hitPrimitive (p : Primitive) (trafo : Trafo3d) (ray : Ray3d) action =
            let mutable ha = RayHit3d.MaxRange
            match p with
                | Sphere s -> 
                    let transformed = trafo.Forward.TransformPos(s.Center)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.HitsSphere(transformed,s.Radius,0.0,Double.PositiveInfinity, &ha) then
                        [ha.T, action]
                    else []
                | Cone(center,dir,height,radius) | Cylinder(center,dir,height,radius) -> 
                    let cylinder = Cylinder3d(trafo.Forward.TransformPos center,trafo.Forward.TransformPos (center+dir*height),radius)
                    let mutable ha = RayHit3d.MaxRange
                    if ray.Hits(cylinder,0.0,Double.MaxValue,&ha) then
                        [ha.T, action]
                    else []
                | Quad q -> 
                    let transformed = Quad3d(q.Points |> Seq.map trafo.Forward.TransformPos)
                    if ray.HitsPlane(Plane3d.ZPlane,0.0,Double.MaxValue,&ha) then [ha.T, action]
                    else []

    module List =
        let updateAt i f xs =
            let rec work current xs =
                match xs with 
                    | x::xs -> 
                        if i = current then f x :: xs
                        else x :: work (current+1) xs
                    | [] -> []
            work 0 xs


module AnotherSceneGraph =

    open Primitives

    type ISg<'msg> = inherit ISg

    let on (p : Kind -> bool) (r : V3d -> 'msg) (k : Kind) = if p k then Some (r (Event.position k)) else None

    let cylinder c d h r = Cylinder(c,d,h,r)

    type AbstractApplicator<'msg>(child : IMod<ISg<'msg>>) =
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

    type Conv<'msg>(applicator : ISg -> ISg, children : ISg<'msg>) =
        let child = Mod.constant (applicator children)
        interface ISg<'msg>
        interface IApplicator with
            member x.Child = child
        member x.SceneGraphChild = child
        member x.Children = children

    let private conv app xs = Conv<_>(app,xs) :> ISg<'msg>

    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics
    [<Semantic>]
    type LeafSemantics() =
        
        member x.InhColor(c : Colored<'msg>) =
            c.Child?InhColor <- c.Color

        member x.InColor(r : Root<ISg>) =
            r.Child?InhColor <- Mod.constant C4b.White

        member x.RenderObjects(c : Conv<'msg>) : aset<IRenderObject> =
            ASet.bind (fun s -> s?RenderObjects()) c.SceneGraphChild 

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
                    let colors = l?InhColor |> Mod.map  (fun c -> Array.replicate vertices.Length c)
                    let normals = Array.replicate vertices.Length (p.Edge03.Cross(p.P2-p.P0)).Normalized
                    let ig = IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, vertices :> Array; DefaultSemantic.Normals, normals :> System.Array], SymDict.empty)
                    ig
                     |> Sg.ofIndexedGeometry
                     |> Sg.vertexAttribute DefaultSemantic.Colors colors
                     |> Semantic.renderObjects
      
      

    type PickObject<'msg> =
        {
            trafo : IMod<Trafo3d>
            primitive : Primitive
            actions : list<PickOperation<'msg>>
        }    

    [<AutoOpen>]
    module SgExt = 
        type ISg<'msg> with
            member x.PickObjects() : aset<PickObject<'msg>> = x?PickObjects()
            member x.PickOperations : list<PickOperation<'msg>> = x?PickOperations

    [<Semantic>]
    type PickingSemantics() =
    

        member x.PickOperations(o : Root<ISg<'msg>>) =
            o.Child?PickOperations <- List.empty<PickOperation<'msg>>

        member x.PickOperations(o : On<'msg>) =
            o.Child?PickOperations <- o.PickOperations

        member x.PickOperations(o : Conv<'msg>) =
            o.Children?PickOperations <- o.PickOperations


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
                        trafo = l.ModelTrafo
                        primitive = l.Primitive
                        actions = ops
                    }


    let transform t xs = Transform<'msg>(t,xs) :> ISg<'msg>
    let translate x y z xs = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, xs) :> ISg<_>
    let translate' x y z c = Transform<'msg>(Trafo3d.Translation(x,y,z) |> Mod.constant, List.singleton c) :> ISg<_>
    let colored c xs = Colored<'msg>(c,xs) :> ISg<'msg>
    let colored' c x = Colored<'msg>(c,x |> List.singleton) :> ISg<'msg>
    let pick picks xs = On<'msg>(picks,xs) :> ISg<'msg>
    let group (xs : list<_>) = Group<'msg>(xs) :> ISg<'msg>
    let leaf x = Leaf<'msg>(x) :> ISg<'msg> 
    let render picks p = pick picks [leaf p]

    let uniform name value xs = conv (Sg.uniform name value) xs
    let effect effects xs = conv (Sg.effect effects) xs
    let viewTrafo viewTrafo xs = conv (Sg.viewTrafo viewTrafo) xs
    let projTrafo projTrafo xs = conv (Sg.projTrafo projTrafo) xs
    let camera camera xs = conv (Sg.camera camera) xs
    


module Elmish3DADaptive =

    open AnotherSceneGraph

    type App<'model,'mmodel,'msg,'view> =
        {
            initial   : 'model
            ofPickMsg : 'model  -> NoPick  -> list<'msg>
            update    : 'model  -> 'msg -> 'model
            view      : 'mmodel -> 'view
        }

    type Unpersist<'immut,'mut> =
        {
            unpersist : 'immut -> ReuseCache -> 'mut
            apply     : 'immut -> 'mut -> ReuseCache -> unit
        }

    let inline unpersist< ^immut, ^mut when ^immut : (member ToMod : ReuseCache -> ^mut) and ^mut : (member Apply : ^immut * ReuseCache -> unit) > () : Unpersist< ^immut,^ mut> = 
        let inline u  (immut : ^immut) (scope : ReuseCache) = (^immut : (member ToMod : ReuseCache -> ^mut) (immut,scope))
        let inline a  (immut : ^immut) (mut : ^mut) (scope : ReuseCache) = (^mut : (member Apply : ^immut * ReuseCache -> unit) (mut,immut,scope))
        { unpersist = u; apply = a }

    let createAppAdaptive (keyboard : IKeyboard) (mouse : IMouse) (camera : IMod<Camera>) (unpersist :  Unpersist<'model,'mmodel>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)  =

        let model = Mod.init app.initial

        let reuseCache = ReuseCache()
        let mmodel = unpersist.unpersist model.Value reuseCache

        let updateModel (m : 'model) =
            transact (fun () -> 
                model.Value <- m
                unpersist.apply m mmodel reuseCache
            )
        let view = app.view mmodel
        let pickObjects = view.PickObjects()
        let pickReader = pickObjects.GetReader()

        let pick (r : Ray3d) : list<float * list<PickOperation<'msg>>> =
            pickReader.GetDelta() |> ignore
            let picks =
                pickReader.Content 
                 |> Seq.toList 
                 |> List.collect (fun p -> 
                        Primitives.hitPrimitive p.primitive (Mod.force p.trafo) r p.actions
                    )
            picks

        let updatePickMsg (m : NoPick) (model : 'model) =
            app.ofPickMsg model m |> List.fold app.update model

        let mutable down = false

        mouse.Move.Values.Subscribe(fun (oldP,newP) -> 
            let ray = newP |> Camera.pickRay (camera |> Mod.force) 
            let mutable model = updatePickMsg (NoPick(MouseEvent.Move,ray)) model.Value // wrong
            match pick ray with
                | (d,f)::_ -> 
                    for msg in f do
                        match msg (Kind.Move (ray.GetPointOnRay d)) with
                            | Some r -> model <- app.update model r
                            | _ -> ()
                | [] -> ()
            updateModel model 
        ) |> ignore

        mouse.Down.Values.Subscribe(fun p ->  
            down <- true
            let ray = mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            let mutable model = model.Value
            match pick ray with
                | ((d,f)::_) -> 
                    for msg in f do
                        match msg (Kind.Down(p, ray.GetPointOnRay d)) with
                            | Some r -> 
                                model <- app.update model r
                            | _ -> ()
                | [] -> 
                    model <- updatePickMsg (NoPick(MouseEvent.Click p, ray)) model
            updateModel model 
        ) |> ignore
 
        mouse.Up.Values.Subscribe(fun p ->     
            down <- false
            let ray = mouse.Position |> Mod.force |> Camera.pickRay (camera |> Mod.force)
            let model = updatePickMsg (NoPick(MouseEvent.Up p, ray)) model.Value
            updateModel model 
        ) |> ignore

        Sg.adapter view

    let inline createAppAdaptiveD (keyboard : IKeyboard) (mouse : IMouse) (camera : IMod<Camera>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)=
        createAppAdaptive keyboard mouse camera ( unpersist ()) app


