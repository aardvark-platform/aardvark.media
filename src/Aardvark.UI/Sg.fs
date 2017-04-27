namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

type SceneEventKind =
    | Enter
    | Leave
    | Move
    | Click
    | DoubleClick
    | Down
    | Up

type SceneEvent =
    {
        kind    : SceneEventKind
        ray     : RayPart
        rayT    : float
        buttons : MouseButtons
    }

    member x.position = x.ray.Ray.Ray.GetPointOnRay x.rayT


module SgTools =
    
    type IMessageProcessor<'a> =
        abstract member NeededEvents : aset<SceneEventKind>
        abstract member Map : aset<SceneEventKind> * ('x -> list<'a>) -> IMessageProcessor<'x>

    type IMessageProcessor<'a, 'b> =
        inherit IMessageProcessor<'a>
        abstract member Process : 'a -> list<'b>


    module MessageProcessor =
        [<AutoOpen>]
        module Implementation =
            type Processor<'a, 'b>(needed : aset<SceneEventKind>, mapping : 'a -> list<'b>) =
                member x.Map(newNeeded : aset<SceneEventKind>, f : 'x -> list<'a>) =
                    Processor<'x, 'b>(ASet.union needed newNeeded, f >> List.collect mapping) :> IMessageProcessor<'x, 'b>

                member x.Process(msg : 'a) =
                    mapping msg

                interface IMessageProcessor<'a> with
                    member x.NeededEvents = needed
                    member x.Map (newNeeded : aset<SceneEventKind>, f : 'x -> list<'a>) = x.Map(newNeeded, f) :> IMessageProcessor<'x>
                    
                interface IMessageProcessor<'a, 'b> with
                    member x.Process msg = x.Process msg

            type IdentityProcessor<'a> private() =
                
                static let instance = IdentityProcessor<'a>() :> IMessageProcessor<'a>

                static member Instance = instance

                interface IMessageProcessor<'a, 'a> with
                    member x.NeededEvents = ASet.empty

                    member x.Map(needed : aset<SceneEventKind>, f : 'x -> list<'a>) =
                        Processor<'x, 'a>(needed, f) :> IMessageProcessor<_>

                    member x.Process(msg : 'a) =
                        [ msg ]

            type IgnoreProcessor<'a, 'b> private() =
                
                static let instance = IgnoreProcessor<'a, 'b>() :> IMessageProcessor<'a>

                static member Instance = instance

                interface IMessageProcessor<'a, 'b> with
                    member x.NeededEvents = ASet.empty

                    member x.Map(newNeeded : aset<_>, f : 'x -> list<'a>) =
                        IgnoreProcessor<'x, 'b>.Instance

                    member x.Process(msg : 'a) =
                        []
                

        let id<'msg> = IdentityProcessor<'msg>.Instance

        let ignore<'a, 'b> = IgnoreProcessor<'a, 'b>.Instance

        let map (newNeeded : aset<SceneEventKind>) (mapping : 'x -> 'a) (p : IMessageProcessor<'a>) =
            p.Map(newNeeded, mapping >> List.singleton)

        let choose (newNeeded : aset<SceneEventKind>) (mapping : 'x -> Option<'a>) (p : IMessageProcessor<'a>) =
            p.Map(newNeeded, mapping >> Option.toList)

        let collect (newNeeded : aset<SceneEventKind>) (mapping : 'x -> list<'a>) (p : IMessageProcessor<'a>) =
            p.Map(newNeeded, mapping)


type ISg<'msg> =
    inherit ISg

type IApplicator<'msg> =
    inherit ISg<'msg>
    abstract member Child : ISg<'msg>

type IGroup<'msg> =
    inherit ISg<'msg>
    abstract member Children : aset<ISg<'msg>>


module Sg =

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

    type EventApplicator<'msg>(events : amap<SceneEventKind, SceneEvent -> list<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type Adapter<'msg>(inner : Aardvark.SceneGraph.ISg) =
        inherit Aardvark.SceneGraph.Sg.AbstractApplicator(inner)
        interface ISg<'msg>

    type Set<'msg>(children : aset<ISg<'msg>>) =
        interface ISg
        interface IGroup<'msg> with
            member x.Children = children

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

        let noEvents (sg : ISg) : ISg<'msg> = box sg

        let withEvents (events : list<SceneEventKind * (SceneEvent -> list<'msg>)>) (sg : ISg<'msg>) =
            Sg.EventApplicator(AMap.ofList events, sg) :> ISg<'msg>

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


[<AutoOpen>]
module ``Sg Events`` =
    
    module Sg =
        let onClick (f : V3d -> 'msg) =
            SceneEventKind.Click, fun (evt : SceneEvent) -> [f evt.position]
            
        let onDoubleClick (f : V3d -> 'msg) =
            SceneEventKind.DoubleClick, fun (evt : SceneEvent) -> [f evt.position]
            
        let onMouseDown (f : MouseButtons -> V3d -> 'msg) =
            SceneEventKind.Down, fun (evt : SceneEvent) -> [f evt.buttons evt.position]
            
        let onMouseUp (f : V3d -> 'msg) =
            SceneEventKind.Up, fun (evt : SceneEvent) -> [f evt.position]
            
        let onEnter (f : V3d -> 'msg) =
            SceneEventKind.Enter, fun (evt : SceneEvent) -> [f evt.position]
            
        let onLeave (f : unit -> 'msg) =
            SceneEventKind.Leave, fun (evt : SceneEvent) -> [f ()]

open Aardvark.Base.Ag

type PickTree<'msg>(sg : ISg<'msg>) =
    let objects : aset<PickObject> = sg?PickObjects()
    let bvh = BvhTree.ofASet PickObject.bounds objects

    let needed = //ASet.ofList [ SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up; SceneEventKind.Move]
        objects |> ASet.collect (fun o -> 
            match Ag.tryGetInhAttribute o.Scope "PickProcessor" with
                | Some (:? SgTools.IMessageProcessor<SceneEvent,'msg> as proc) ->
                    proc.NeededEvents
                | _ ->
                    ASet.empty
        )

    static let intersectLeaf (kind : SceneEventKind) (part : RayPart) (p : PickObject) =
        let pickable = p.Pickable |> Mod.force
        match Pickable.intersect part pickable with
            | Some t -> 
                let pt = part.Ray.Ray.GetPointOnRay t
                match Ag.tryGetInhAttribute p.Scope "PickProcessor" with
                    | Some (:? SgTools.IMessageProcessor<SceneEvent,'msg> as proc) ->
                        Some (RayHit(t, proc))
                    | _ ->
                        None
            | None -> 
                None

    let mutable last = None
    
    let perform (evt : byref<SceneEvent>) (bvh : BvhTree<PickObject>) =
        match bvh.Intersect(intersectLeaf evt.kind, evt.ray) with
            | Some (hit) ->
                let proc = hit.Value
                evt <- { evt with rayT = hit.T }

                let perform (evt : SceneEvent) =
                    proc.Process evt

                let evt = evt
                let boot() =
                    [
                        yield! perform { evt with kind = SceneEventKind.Enter }
                        yield! perform evt
                    ]
                    

                match last with
                    | Some lastProc when System.Object.ReferenceEquals(lastProc, proc) ->
                        perform evt

                    | Some lastProc ->
                        last <- Some proc
                        let l = lastProc.Process { evt with kind = SceneEventKind.Leave }
                        l @ boot()
  
                    | None ->
                        last <- Some proc
                        boot()              
            | None ->
                match last with
                    | Some lastProc ->
                        last <- None
                        lastProc.Process { evt with kind = SceneEventKind.Leave }
                    | _ ->
                        []

    member x.Needed = needed

    member x.Perform(evt : byref<SceneEvent>) =
        let bvh = bvh |> Mod.force
        perform &evt bvh
        
    member x.Dispose() =
        bvh.Dispose()

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

module PickTree =
    let ofSg (sg : ISg<'msg>) = new PickTree<'msg>(sg)
    let perform (evt : byref<SceneEvent>) (tree : PickTree<'msg>) = tree.Perform(&evt)


namespace Aardvark.UI.Semantics
open Aardvark.UI
open Aardvark.UI.SgTools
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Ag


type private PickObject = Aardvark.SceneGraph.``Sg Picking Extensions``.PickObject


[<AutoOpen>]
module ``Message Semantics`` =
    open Aardvark.SceneGraph

    type IRuntime with
        member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, sg : ISg<'msg>) =
            x.CompileRender(fbo, config, unbox<Aardvark.SceneGraph.ISg> sg)

        member x.CompileRender(fbo : IFramebufferSignature, sg : ISg<'msg>) =
            x.CompileRender(fbo, unbox<Aardvark.SceneGraph.ISg> sg)

    type ISg<'msg> with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

        member x.MessageProcessor : IMessageProcessor<'msg> = x?MessageProcessor
        member x.PickProcessor : IMessageProcessor<SceneEvent> = x?PickProcessor

    [<Semantic>]
    type StandardSems() =

//        member x.RenderObjects(app : IApplicator<'msg>) =
//            aset {
//                let c = app.Child
//                yield! c.RenderObjects()
//            }

        member x.RenderObjects(g : IGroup<'msg>) =
            aset {
                for c in g.Children do
                    yield! c.RenderObjects()
            }

//        member x.PickObjects(app : IApplicator<'msg>) =
//            aset {
//                let c = app.Child
//                yield! c.PickObjects()
//            }

        member x.PickObjects(g : IGroup<'msg>) =
            aset {
                for c in g.Children do
                    yield! c.PickObjects()
            }

//        member x.GlobalBoundingBox(app : IApplicator<'msg>) =
//            let c = app.Child
//            c.GlobalBoundingBox()

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

//        member x.LocalBoundingBox(app : IApplicator<'msg>) =
//            let c = app.Child
//            c.LocalBoundingBox()

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

    [<Semantic>]
    type MessageProcessorSem() =

        member x.MessageProcessor(root : Root<ISg<'msg>>) =
            root.Child?MessageProcessor <- MessageProcessor.id<'msg>
            
        member x.MessageProcessor(app : Sg.MapApplicator<'inner, 'outer>) =
            let parent = app.MessageProcessor
            app.Child?MessageProcessor <- parent.Map(ASet.empty, app.Mapping)

        member x.PickProcessor(root : Root<ISg<'msg>>) =
            root.Child?PickProcessor <- MessageProcessor.ignore<SceneEvent, 'msg>

        member x.PickProcessor(app : Sg.EventApplicator<'msg>) =
            let msg = app.MessageProcessor

    
            let needed =
                ASet.create (fun scope ->
                    let reader = app.Events.GetReader()
                    { new AbstractReader<hdeltaset<SceneEventKind>>(scope, HDeltaSet.monoid) with
                        member x.Release() =
                            reader.Dispose()

                        member x.Compute(token) =
                            let ops = reader.GetOperations token

                            ops |> HMap.map (fun key op ->
                                match op with
                                    | Set _ -> +1
                                    | Remove -> -1
                            ) |> HDeltaSet.ofHMap
                    }
                )
 

            let processor (evt : SceneEvent) =
                let evts = app.Events.Content |> Mod.force
                match HMap.tryFind evt.kind evts with
                    | Some cb -> cb evt
                    | None -> []
                
            app.Child?PickProcessor <- msg.Map(needed, processor)
//
//
//        member x.MsgPickObjects(a : Sg.EventApplicator<'msg>) =
//            aset {
//                let c = a.Child
//                for po in c.MsgPickObjects() do
//                    yield Sg.PickObject<'msg>(po.Object, AMap.union po.Events a.Events)
//            }
//
//        member x.MsgPickObjects(a : Sg.MapApplicator<'inner, 'outer>) =
//            aset {
//                let c = a.Child
//                for po in c.MsgPickObjects() do
//                    yield Sg.PickObject<'outer>(po.Object, po.Events |> AMap.map (fun _ f p t -> f p t |> List.collect a.Mapping))
//            }
//
//        member x.MsgPickObjects(a : IApplicator<'msg>) =
//            aset {
//                let c = a.Child
//                yield! c.MsgPickObjects()
//            }
//
//        member x.MsgPickObjects(g : IGroup<'msg>) =
//            aset {
//                for c in g.Children do
//                    yield! c.MsgPickObjects()
//            }
//
//        member x.MsgPickObjects(a : Sg.Adapter<'msg>) =
//            aset {
//                let! c = a.Child
//                yield! c.PickObjects() |> ASet.map (fun o -> Sg.PickObject<'msg>(o, AMap.empty))
//            }









