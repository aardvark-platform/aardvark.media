namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Suave.Logging

type SceneEventKind =
    | Enter
    | Leave
    | Move
    | Click
    | DoubleClick
    | Down
    | Up

[<AutoOpen>]
module RayPartExtension =

    type RayPart with
        member x.Transformed(t : Trafo3d) =
            RayPart(FastRay3d(x.Ray.Ray.Transformed(t.Forward)), x.TMin, x.TMax)

type SceneEvent =
    {
        kind    : SceneEventKind
        ray     : RayPart          // global space
        buttons : MouseButtons

        trafo : Trafo3d            // local -> global
    }

type SceneHit(event : SceneEvent, rayT : double) =       
    member x.Event = event
    member x.LocalRay =
        event.ray.Transformed(event.trafo.Inverse)
    member x.GlobalRay =
        event.ray
    member x.LocalPosition = 
        event.trafo.Backward.TransformPos(x.LocalPosition)
    member x.GlobalPosition =   
        event.ray.Ray.Ray.GetPointOnRay rayT
    member x.IsValid =
        rayT.IsPositiveInfinity() |> not

    new(event : SceneEvent) = SceneHit(event, infinity)

type SceneEventHandler<'msg>  = SceneHit -> bool * list<'msg>
type GlobalEventHandler<'msg> = SceneEvent -> bool -> list<'msg>

type ISg<'msg> =
    inherit ISg

type IApplicator<'msg> =
    inherit ISg<'msg>
    abstract member Child : ISg<'msg>

type IGroup<'msg> =
    inherit ISg<'msg>
    abstract member Children : aset<ISg<'msg>>



module Sg =

    type NoEventApplicator<'msg>(child : ISg) =
        interface Aardvark.SceneGraph.IApplicator with
            member x.Child = child |> Mod.constant
        
        member x.Child = child

        interface ISg<'msg>


    type AbstractApplicator<'msg>(child : ISg<'msg>) =
        interface Aardvark.SceneGraph.IApplicator with
            member x.Child = child |> unbox |> Mod.constant

        interface IApplicator<'msg> with
            member x.Child = child

        member x.Child = child

    type MapApplicator<'inner, 'outer>(mapping : 'inner -> list<'outer>, child : ISg<'inner>) =
        interface Aardvark.SceneGraph.IApplicator with
            member x.Child = child |> unbox |> Mod.constant

        interface ISg<'outer>

        member x.Child = child
        member x.Mapping = mapping

    type EventApplicator<'msg>(events : amap<SceneEventKind, SceneEventHandler<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type GlobalEvent<'msg>(events : amap<SceneEventKind, GlobalEventHandler<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type Adapter<'msg>(inner : Aardvark.SceneGraph.ISg) =
        inherit Aardvark.SceneGraph.Sg.AbstractApplicator(inner)
        interface ISg<'msg>

    type Set<'msg>(children : aset<ISg<'msg>>) =
        interface ISg
        interface IGroup<'msg> with
            member x.Children = children

open Sg


module AMap =
    let keys (m : amap<'k, 'v>) : aset<'k> =
        ASet.create (fun scope ->
            let reader = m.GetReader()
            { new AbstractReader<hdeltaset<'k>>(scope, HDeltaSet.monoid) with
                member x.Release() =
                    reader.Dispose()

                member x.Compute(token) =
                    let oldState = reader.State

                    let ops = reader.GetOperations token

                    ops |> HMap.choose (fun key op ->
                        match op with
                            | Set _ -> 
                                if HMap.containsKey key oldState then None
                                else Some (+1)
                            | Remove -> Some (-1)
                    ) |> HDeltaSet.ofHMap
            }
        )


module NewShit =
    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics

    type PickObject<'msg>(obj : PickObject, needed : aset<SceneEventKind>, mapping : SceneHit -> bool * list<'msg>) =
        member x.Object = obj
        member x.Mapping = mapping
        member x.Needed = needed

        member x.Perform(evt : SceneEvent) =
            let obj = Mod.force obj.Pickable
            match Pickable.intersect evt.ray obj with
                | Some t ->
                    let hit = SceneHit(evt, t)
                    Some (mapping hit)
                | None ->
                    None

    type ISg<'msg> with
        member x.MsgPickObjects() : aset<PickObject<'msg>> = x?MsgPickObjects()


    let rec allMsgSgs (s : ISg) : aset<ISg<'msg>> =
        match s with
            | :? ISg<'msg> as s -> ASet.ofList [s]
            | :? IApplicator as a -> a.Child |> ASet.bind allMsgSgs
            | :? IGroup as g -> g.Children |> ASet.collect allMsgSgs
            | _ -> ASet.empty

    [<Semantic>]
    type PickObjectSem() =

        member x.MsgPickObjects(app : Sg.NoEventApplicator<'msg>) =
            app.Child.PickObjects() |> ASet.map (fun o ->
                PickObject<'msg>(o, ASet.empty, fun _ -> true, [])
            )

        member x.MsgPickObjects(app : Sg.EventApplicator<'msg>) =
            app.Child.MsgPickObjects() |> ASet.map (fun o ->
                let needed = AMap.keys app.Events

                PickObject<'msg>(o.Object, ASet.union needed o.Needed, fun hit ->
                    let table = Mod.force app.Events.Content
                    match HMap.tryFind hit.Event.kind table with
                        | Some f -> 
                            let cont, msgs = f hit
                            if cont then
                                let cont, inner = o.Mapping hit
                                cont, msgs @ inner
                            else
                                false, msgs
                        | None -> 
                            o.Mapping hit

                )
            )
        member x.MsgPickObjects(app : ISg<'msg>) =
            ASet.empty<PickObject<'msg>>

        member x.MsgPickObjects(app : Sg.MapApplicator<'i, 'o>) =
            app.Child.MsgPickObjects() |> ASet.map (fun o ->
                PickObject<'o>(o.Object, o.Needed, fun hit ->
                    let cont, msgs = o.Mapping hit
                    cont, List.collect app.Mapping msgs
                )
            )

        member x.MsgPickObjects(s : IGroup<'msg>) =
            s.Children |> ASet.collect (fun g -> g.MsgPickObjects())

        member x.MsgPickObjects(app : IApplicator<'msg>) =
            app.Child.MsgPickObjects()

        member x.MsgPickObjects(app : Sg.Adapter<'msg>) : aset<PickObject<'msg>> =
            app.Child |> ASet.bind (fun c ->
                allMsgSgs c |> ASet.collect (fun c -> c.MsgPickObjects())
            )


[<AutoOpen>]
module ``F# Sg`` =

    module Sg =
        let private box<'msg> (sg : ISg) =
            sg |> Sg.Adapter :> ISg<'msg>

        let private unboxed (f : Aardvark.SceneGraph.ISg -> Aardvark.SceneGraph.ISg) (inner : ISg<'msg>) =
            match inner with
                | :? Sg.Adapter<'msg> as a ->
                    a.Child |> Mod.force |> f |> Sg.Adapter :> ISg<'msg>
                | _ ->
                    inner |> unbox |> f |> Sg.Adapter :> ISg<'msg>

        let pickable (p : PickShape) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickable p)

        let pickBoundingBox (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickBoundingBox)

        let toUntypedSg (sg : ISg<'msg>) = unbox sg

        let noEvents (sg : ISg) : ISg<'msg> = Sg.NoEventApplicator<'msg>(sg) :> ISg<'msg>


        let withEvents (events : list<SceneEventKind * SceneEventHandler<'msg>>) (sg : ISg<'msg>) =
            Sg.EventApplicator(AMap.ofList events, sg) :> ISg<'msg>
 
        let withGlobalEvents (events : list<SceneEventKind * GlobalEventHandler<'msg>>) (sg : ISg<'msg>) =
            Sg.GlobalEvent(AMap.ofList events, sg) :> ISg<'msg>

        let uniform (name : string) (value : IMod<'a>) (sg : ISg<'msg>) =
           sg |> unboxed (Sg.uniform name value)
        
        let trafo (m : IMod<Trafo3d>) (sg : ISg<'msg>) =
           sg |> unboxed (Sg.trafo m)      
       
        let viewTrafo (m : IMod<Trafo3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.viewTrafo m)

        let projTrafo (m : IMod<Trafo3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.projTrafo m)  

        let scale (s : float) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scale s)  

        let translate (x : float) (y : float) (z : float) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.translate x y z)  

        let transform (t : Trafo3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.transform t)

        let camera (cam : IMod<Camera>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.camera cam)

        let surface (m : IMod<ISurface>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.surface m)

        let set (set : aset<ISg<'msg>>) =
            Sg.Set<'msg>(set) :> ISg<'msg>

        let ofSeq (s : seq<#ISg<'msg>>) =
            s |> Seq.cast<ISg<'msg>> |> ASet.ofSeq |> Sg.Set :> ISg<'msg>

        let ofList (l : list<#ISg<'msg>>) =
            l |> ofSeq

        let ofArray (arr : array<#ISg<'msg>>) =
            arr |> ofSeq
       
        let andAlso (sg : ISg<'msg>) (andSg : ISg<'msg>) = 
            ofList [sg;andSg]

        let map (f : 'a -> 'b) (a : ISg<'a>) : ISg<'b> =
            Sg.MapApplicator<'a,'b>(f >> List.singleton,a) :> ISg<_>


        let geometrySet mode attributeTypes (geometries : aset<_>) : ISg<'msg> =
            Sg.GeometrySet(geometries,mode,attributeTypes) |> box

        let dynamic (s : IMod<ISg<'msg>>) = 
            Sg.DynamicNode(Mod.map unbox s) |> box<'msg>

        let onOff (active : IMod<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.onOff active)

        let texture (sem : Symbol) (tex : IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.texture sem tex)

        let diffuseTexture (tex : IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseTexture tex)

        let diffuseTexture' (tex : ITexture) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseTexture' tex)

        let diffuseFileTexture' (path : string) (wantMipMaps : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseFileTexture' path wantMipMaps)

        let fileTexture (sym : Symbol) (path : string) (wantMipMaps : bool) (sg : ISg<'msg>) = 
            sg |> unboxed (Sg.fileTexture sym path wantMipMaps)

        let scopeDependentTexture (sem : Symbol) (tex : Ag.Scope -> IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scopeDependentTexture sem tex)

        let scopeDependentDiffuseTexture (tex : Ag.Scope -> IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scopeDependentDiffuseTexture tex)

        let runtimeDependentTexture (sem : Symbol) (tex : IRuntime -> IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.runtimeDependentTexture sem tex)

        let runtimeDependentDiffuseTexture (tex : IRuntime -> IMod<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.runtimeDependentDiffuseTexture tex)

        let samplerState (sem : Symbol) (state : IMod<Option<SamplerStateDescription>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.samplerState sem state)

        let modifySamplerState (sem : Symbol) (modifier : IMod<SamplerStateDescription -> SamplerStateDescription>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.modifySamplerState sem modifier)

        

        let fillMode (m : IMod<FillMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.fillMode m)

        let blendMode (m : IMod<BlendMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendMode m)

        let cullMode (m : IMod<CullMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.cullMode m)

        let stencilMode (m : IMod<StencilMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilMode m)

        let depthTest (m : IMod<DepthTestMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthTest m)

        let writeBuffers' (buffers : Microsoft.FSharp.Collections.Set<Symbol>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers' buffers)

        let writeBuffers (buffers : Option<Microsoft.FSharp.Collections.Set<Symbol>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers buffers)

        let colorMask (maskRgba : IMod<bool * bool * bool * bool>) (sg : ISg<'msg>) =
            let f sg = sg |> Sg.colorMask maskRgba :> ISg
            sg |> unboxed f

        let depthMask (depthWriteEnabled : IMod<bool>) (sg : ISg<'msg>) =
            let f sg = sg |> Sg.depthMask depthWriteEnabled :> ISg
            sg |> unboxed f

        let vertexAttribute<'a, 'msg when 'a : struct> (s : Symbol) (value : IMod<'a[]>) (sg : ISg<'msg>) = 
            sg |> unboxed (Sg.vertexAttribute s value)

        let index<'a, 'msg when 'a : struct> (value : IMod<'a[]>)  (sg : ISg<'msg>) = 
            sg |> unboxed (Sg.index value)

        let vertexAttribute'<'a, 'msg when 'a : struct> (s : Symbol) (value : 'a[]) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexAttribute' s value)
        
        let index'<'a, 'msg when 'a : struct> (value : 'a[])  (sg : ISg<'msg>) = 
            sg |> unboxed (Sg.index' value)

        let vertexBuffer (s : Symbol) (view : BufferView) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexBuffer s view)

        let vertexBufferValue (s : Symbol) (value : IMod<V4f>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexBufferValue s value)

        let draw (mode : IndexedGeometryMode) : ISg<'msg> =
            Sg.draw mode |> box

        let render (mode : IndexedGeometryMode) (call : DrawCallInfo) : ISg<'msg> =
            Sg.render mode call |> box

        let ofIndexedGeometry (g : IndexedGeometry) : ISg<'msg> =
            Sg.ofIndexedGeometry g |> box

        let ofIndexedGeometryInterleaved (attributes : list<Symbol>) (g : IndexedGeometry) : ISg<'msg> =
            Sg.ofIndexedGeometryInterleaved attributes g |> box

        let instancedGeometry (trafos : IMod<Trafo3d[]>) (g : IndexedGeometry) : ISg<'msg> =
            Sg.instancedGeometry trafos g |> box

        let pass (pass : RenderPass) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pass pass)

        let normalizeToAdaptive (box : Box3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeToAdaptive box)

        let normalizeTo (box : Box3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeTo box)

        let normalize (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalize)

        let normalizeAdaptive (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeAdaptive)

        let effect e (sg : ISg<'msg>) =
            sg |> unboxed (Sg.effect e)

        let adapter (o : obj) : ISg<'msg> =
            Sg.adapter o |> box


        module Incremental =
            let withEvents (events : amap<SceneEventKind, SceneEventHandler<'msg>>) (sg : ISg<'msg>) =
                Sg.EventApplicator(events, sg) :> ISg<'msg>
 
            let withGlobalEvents (events : amap<SceneEventKind, GlobalEventHandler<'msg>>) (sg : ISg<'msg>) =
                Sg.GlobalEvent(events, sg) :> ISg<'msg>


[<AutoOpen>]
module ``Sg Events`` =
    
    module Sg =
        
        let private simple k f = k, fun (evt : SceneHit) -> false, [f evt]
            

        let onClick (f : SceneHit -> 'msg) =
            simple SceneEventKind.Click f
            
        let onDoubleClick (f : SceneHit -> 'msg) =
            simple SceneEventKind.DoubleClick f
            
        let onMouseDown (f : SceneHit -> 'msg) =
            simple SceneEventKind.Down f
            

        let onMouseMove (f : SceneHit -> 'msg) =
            simple SceneEventKind.Move f

        let onMouseUp (f : SceneHit -> 'msg) =
            simple SceneEventKind.Up f
            
        let onEnter (f : SceneHit -> 'msg) =
            simple SceneEventKind.Enter f
            
        let onLeave (f : unit -> 'msg) =
            simple SceneEventKind.Leave (ignore >> f)

        module Global =
        
            let onMouseUp (f : SceneEvent -> 'msg) =
                SceneEventKind.Up, fun (evt : SceneEvent) (wasOther : bool) -> if not wasOther then [f evt] else []

            let onMouseMove (f : SceneEvent -> 'msg) =
                SceneEventKind.Move, fun (evt : SceneEvent) (wasOther : bool) -> if not wasOther then [f evt] else []

open Aardvark.Base.Ag

type PickTree<'msg>(sg : ISg<'msg>) =
    let objects : aset<NewShit.PickObject<'msg>> = sg?MsgPickObjects()
    let bvh = BvhTree.ofASet (fun (o : NewShit.PickObject<'msg>) -> PickObject.bounds o.Object) objects

    let needed = objects |> ASet.collect (fun o -> o.Needed)
    
    let mutable last : Option<NewShit.PickObject<'msg>> = None

    static let intersectLeaf (evt : SceneEvent) (part : RayPart) (o : NewShit.PickObject<'msg>) =
        let evt = { evt with ray = part }
        let obj = Mod.force o.Object.Pickable
        match Pickable.intersect evt.ray obj with
            | Some t ->
                let hit = SceneHit({ evt with trafo = obj.trafo }, t)
                Some (RayHit(t, (o, o.Mapping hit)))
            | None ->
                None


        //let pickable = p.Pickable |> Mod.force
        
        //match Pickable.intersect part pickable with
        //    | Some t -> 
        //        let pt = part.Ray.Ray.GetPointOnRay t
        //        match Ag.tryGetInhAttribute p.Scope "PickProcessor" with
        //            | Some (:? SgTools.IMessageProcessor<SceneHit,'msg> as proc) ->
        //                Some <| RayHit(t, (proc,pickable))
        //            | _ ->
        //                None
        //    | None -> 
        //        None

    member private x.Perform (evt : SceneEvent, bvh : BvhTree<NewShit.PickObject<'msg>>, foundHit : ref<bool>) =
        match bvh.Intersect(intersectLeaf evt, evt.ray) with
            | Some (hit) ->
                let o, (cont, msgs) = hit.Value
                
                foundHit := true
                let doit evt =
                    let evt = { evt with ray = RayPart(evt.ray.Ray, hit.T + 0.01, evt.ray.TMax) }
                    x.Perform(evt,bvh, foundHit)

                let statfulHate = 
                    if Some o <> last then
                        let leaveMsgs =
                            match last with
                                | Some l -> l.Mapping (SceneHit({ evt with kind = SceneEventKind.Leave },hit.T)) |> snd
                                | None -> []
                        let _, enterMsgs = o.Mapping (SceneHit({ evt with kind = SceneEventKind.Enter },hit.T))
                        last <- Some o
                        leaveMsgs @ enterMsgs
                    else
                        []

                if cont then
                    statfulHate @ msgs @ doit evt
                else
                    statfulHate @ msgs

                //match proc.Process (SceneHit(evt,hit.T), true)  with
                //    | (msgs, true) -> 
                //        msgs @ cont evt
                //    | (msgs, false) ->  
                //        foundHit := true
                //        if Some proc <> last then
                //            let enters,_ = proc.Process (SceneHit({ evt with kind = SceneEventKind.Enter },hit.T), false)
                //            match last with
                //                | Some l -> 
                //                    let leaves,_ = l.Process  (SceneHit({ evt with kind = SceneEventKind.Leave },hit.T), false)
                //                    last <- Some proc
                //                    leaves @ enters @ msgs
                //                | None -> 
                //                    last <- Some proc
                //                    enters @ msgs
                //        else msgs
            | None -> 
                 match last with
                    | Some l -> 
                        let _, leaves = l.Mapping (SceneHit({ evt with kind = SceneEventKind.Leave }))
                        last <- None
                        leaves
                    | None -> 
                        []

    member x.Needed = needed

    member x.Perform(evt : SceneEvent, foundHit : byref<bool>) =
        let bvh = bvh |> Mod.force
        let r = ref foundHit
        let result = x.Perform(evt, bvh, r)
        foundHit <- !r
        result
        
    member x.Dispose() =
        bvh.Dispose()

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

module PickTree =
    let ofSg (sg : ISg<'msg>) = new PickTree<'msg>(sg)
    let perform (evt : SceneEvent) (tree : PickTree<'msg>)  =
        let mutable foundHit = false
        tree.Perform(evt,&foundHit)


namespace Aardvark.UI.Semantics
open Aardvark.UI
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Ag


type private PickObject = Aardvark.SceneGraph.``Sg Picking Extensions``.PickObject


[<AutoOpen>]
module ``Message Semantics`` =
    open Aardvark.SceneGraph
    open Aardvark.UI.Sg

    type IRuntime with
        member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, sg : ISg<'msg>) =
            x.CompileRender(fbo, config, unbox<Aardvark.SceneGraph.ISg> sg)

        member x.CompileRender(fbo : IFramebufferSignature, sg : ISg<'msg>) =
            x.CompileRender(fbo, unbox<Aardvark.SceneGraph.ISg> sg)

    type GlobalPicks<'msg> = amap<SceneEventKind, GlobalEventHandler<'msg>> 

    type ISg<'msg> with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.GlobalPicks() : GlobalPicks<'msg> = x?GlobalPicks()
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()


                
        

    [<Semantic>]
    type StandardSems() =

        member x.RenderObjects(g : IGroup<'msg>) =
            aset {
                for c in g.Children do
                    yield! c.RenderObjects()
            }

        member x.PickObjects(g : IGroup<'msg>) =
            aset {
                for c in g.Children do
                    yield! c.PickObjects()
            }

        member x.GlobalBoundingBox(g : IGroup<'msg>) =
            let add (l : Box3d) (r : Box3d) =
                Box3d.Union(l,r)

            let trySub (s : Box3d) (b : Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children 
                |> ASet.mapM (fun c -> c.GlobalBoundingBox()) 
                |> ASet.foldHalfGroup add trySub Box3d.Invalid

        member x.LocalBoundingBox(g : IGroup<'msg>) =
            let add (l : Box3d) (r : Box3d) =
                Box3d.Union(l,r)

            let trySub (s : Box3d) (b : Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children 
                |> ASet.mapM (fun c -> c.LocalBoundingBox()) 
                |> ASet.foldHalfGroup add trySub Box3d.Invalid

        

        member x.GlobalPicks(g : IGroup<'msg>) : GlobalPicks<'msg> =
             // usuperfast
             
             g.Children 
                |> ASet.collect (fun g -> g.GlobalPicks() |> AMap.toASet)
                |> AMap.ofASet
                |> AMap.map (fun k vs ->
                    match HSet.toList vs with
                        | [] -> fun _ _ -> []
                        | h :: rest ->
                            rest |> List.fold (fun l r e b -> l e b @ r e b) h
                            
                )

        member x.GlobalPicks(a : IApplicator<'msg>) : GlobalPicks<'msg> =
            a.Child.GlobalPicks()
            
            
        member x.GlobalPicks(a : Sg.Adapter<'msg>) : GlobalPicks<'msg> =
            let children = a.Child |> ASet.bind NewShit.allMsgSgs
            
            children 
                |> ASet.collect (fun g -> g.GlobalPicks() |> AMap.toASet)
                |> AMap.ofASet
                |> AMap.map (fun k vs ->
                    match HSet.toList vs with
                        | [] -> fun _ _ -> []
                        | h :: rest ->
                            rest |> List.fold (fun l r e b -> l e b @ r e b) h
                            
                )


        member x.GlobalPicks(g : Sg.GlobalEvent<'msg>) : GlobalPicks<'msg> =
            let a : GlobalPicks<'msg> = g.Events
            let b = g.Child.GlobalPicks()
            let t = Aardvark.SceneGraph.Semantics.TrafoExtensions.Semantic.modelTrafo g
            AMap.unionWith (fun k l r e b -> l ({ e with trafo = Mod.force t}) b @ r e b) a b

            
        member x.GlobalPicks(other : ISg<'msg>) : GlobalPicks<'msg>  =
            AMap.empty

        member x.GlobalPicks(ma : MapApplicator<'i,'o>) : GlobalPicks<'o> =
            let picks = ma.Child.GlobalPicks()
            picks |> AMap.map (fun k v e b -> v e b |> List.collect ma.Mapping)










