namespace Aardvark.UI

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Rendering
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Suave.Logging


module AMap =
    let keys (m : amap<'k, 'v>) : aset<'k> =
        ASet.create (fun scope ->
            let reader = m.GetReader()
            { new AbstractReader<hdeltaset<'k>>(scope, HDeltaSet.monoid) with
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


type SceneEventKind =
    | Enter
    | Leave
    | Move
    | Click
    | DoubleClick
    | Down
    | Up

[<AbstractClass; Sealed; Extension>]
type RayPartExtensions private() =
    [<Extension>]
    static member Transformed(this : RayPart, m : M44d) =
        RayPart(FastRay3d(this.Ray.Ray.Transformed(m)), this.TMin, this.TMax)

type SceneEvent =
    {
        evtKind    : SceneEventKind
        evtRay     : RayPart
        evtButtons : MouseButtons
        evtTrafo   : IMod<Trafo3d>
    }

    member x.kind = x.evtKind
    member x.localRay = x.evtRay.Transformed(x.evtTrafo.GetValue().Backward)
    member x.globalRay = x.evtRay
    member x.buttons = x.evtButtons

type SceneHit = 
    { 
        event : SceneEvent
        rayT  : float 
    }
    member inline x.kind = x.event.kind
    member inline x.localRay = x.event.localRay
    member inline x.globalRay = x.event.globalRay
    member inline x.buttons = x.event.buttons

    member x.globalPosition = x.globalRay.Ray.Ray.GetPointOnRay x.rayT
    member x.localPosition = x.localRay.Ray.Ray.GetPointOnRay x.rayT


    


module SgTools =
    
    type ISceneHitProcessor =
        abstract member NeededEvents : aset<SceneEventKind>

    type ISceneHitProcessor<'a> =
        inherit ISceneHitProcessor
        abstract member Process : SceneHit -> bool * seq<'a>
        

    type IMessageProcessor<'a> =
        abstract member NeededEvents : aset<SceneEventKind>
        abstract member Map : aset<SceneEventKind> * ('x -> seq<'a>) -> IMessageProcessor<'x>
        abstract member MapHit : aset<SceneEventKind> * (SceneHit -> bool * seq<'a>) -> ISceneHitProcessor

    type IMessageProcessor<'a, 'b> =
        inherit IMessageProcessor<'a>
        abstract member Process : 'a -> seq<'b>


    module MessageProcessor =
        [<AutoOpen>]
        module Implementation =

            type HitProcessor<'a>(needed : aset<SceneEventKind>, mapping : SceneHit -> bool * seq<'a>) =
                member x.Process(msg : SceneHit) =
                    mapping msg

                interface ISceneHitProcessor with
                    member x.NeededEvents = needed

                interface ISceneHitProcessor<'a> with
                    member x.Process hit = x.Process hit

            type Processor<'a, 'b>(needed : aset<SceneEventKind>, mapping : 'a -> seq<'b>) =
                member x.Map(newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) =
                    Processor<'x, 'b>(ASet.union needed newNeeded, f >> Seq.collect mapping) :> IMessageProcessor<'x, 'b>
                    
                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                    let f x =
                        let cont, msgs = f x
                        cont, Seq.collect mapping msgs

                    HitProcessor<'b>(ASet.union needed newNeeded, f) :> ISceneHitProcessor<'b>

                member x.Process(msg : 'a) =
                    mapping msg

                interface IMessageProcessor<'a> with
                    member x.NeededEvents = needed
                    member x.Map (newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) = x.Map(newNeeded, f) :> IMessageProcessor<'x>
                    member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) = x.MapHit(newNeeded, f) :> ISceneHitProcessor
                        
                interface IMessageProcessor<'a, 'b> with
                    member x.Process msg = x.Process msg

            type IdentityProcessor<'a> private() =
                
                static let instance = IdentityProcessor<'a>() :> IMessageProcessor<'a>

                static member Instance = instance

                interface IMessageProcessor<'a, 'a> with
                    member x.NeededEvents = ASet.empty

                    member x.Map(needed : aset<SceneEventKind>, f : 'x -> seq<'a>) =
                        Processor<'x, 'a>(needed, f) :> IMessageProcessor<_>
                        
                    member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                        HitProcessor<'a>(newNeeded, f) :> ISceneHitProcessor

                    member x.Process(msg : 'a) =
                        Seq.singleton msg

            type IgnoreProcessor<'a, 'b> private() =
                
                static let instance = IgnoreProcessor<'a, 'b>() 

                static member Instance = instance

                interface ISceneHitProcessor<'b> with
                    member x.NeededEvents = ASet.empty
                    member x.Process hit = true, Seq.empty

                interface IMessageProcessor<'a, 'b> with
                    member x.NeededEvents = ASet.empty

                    member x.Map(newNeeded : aset<_>, f : 'x -> seq<'a>) =
                        IgnoreProcessor<'x, 'b>.Instance :> IMessageProcessor<'x>
                        
                    member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
                        IgnoreProcessor<obj, 'b>.Instance :> ISceneHitProcessor

                    member x.Process(msg : 'a) =
                        Seq.empty
                

        let id<'msg> = IdentityProcessor<'msg>.Instance

        let ignore<'a, 'b> = IgnoreProcessor<'a, 'b>.Instance

        let map (newNeeded : aset<SceneEventKind>) (mapping : 'x -> 'a) (p : IMessageProcessor<'a>) =
            p.Map(newNeeded, mapping >> Seq.singleton)

        let choose (newNeeded : aset<SceneEventKind>) (mapping : 'x -> Option<'a>) (p : IMessageProcessor<'a>) =
            p.Map(newNeeded, mapping >> Option.map Seq.singleton >> Option.defaultValue Seq.empty)

        let collect (newNeeded : aset<SceneEventKind>) (mapping : 'x -> seq<'a>) (p : IMessageProcessor<'a>) =
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

    type MapApplicator<'inner, 'outer>(mapping : 'inner -> seq<'outer>, child : ISg<'inner>) =
        interface Aardvark.SceneGraph.IApplicator with
            member x.Child = child |> unbox |> Mod.constant

        interface ISg<'outer>

        member x.Child = child
        member x.Mapping = mapping

    type EventApplicator<'msg>(events : amap<SceneEventKind, SceneHit -> bool * seq<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events

    type GlobalEvent<'msg>(events : amap<SceneEventKind, SceneEvent -> seq<'msg>>, child : ISg<'msg>) =
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

        let pickable' (p : IMod<PickShape>) (sg : ISg<'msg>) =
            sg |> unboxed (fun s -> new Sg.PickableApplicator(Mod.map Pickable.ofShape p, Mod.constant s) :> ISg)

        let pickBoundingBox (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickBoundingBox)

        let toUntypedSg (sg : ISg<'msg>) = unbox sg

        let noEvents (sg : ISg) : ISg<'msg> =             
           match sg with
           | :? ISg<'msg> as isgMsg -> 
             Log.warn "[Media:] superfluous use of Sg.noEvents, returning input as is"
             isgMsg
           | _ -> box sg

        let withEvents (events : list<SceneEventKind * (SceneHit -> bool * seq<'msg>)>) (sg : ISg<'msg>) =
            Sg.EventApplicator(AMap.ofList events, sg) :> ISg<'msg>
 
        let withGlobalEvents (events : list<SceneEventKind * (SceneEvent -> seq<'msg>)>) (sg : ISg<'msg>) =
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

        let surface (m : ISurface) (sg : ISg<'msg>) =
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
            Sg.MapApplicator<'a,'b>(f >> Seq.singleton,a) :> ISg<_>


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
            let withEvents (events : amap<SceneEventKind, SceneHit -> bool * seq<'msg>>) (sg : ISg<'msg>) =
                Sg.EventApplicator(events, sg) :> ISg<'msg>
 
            let withGlobalEvents (events : amap<SceneEventKind, (SceneEvent -> seq<'msg>)>) (sg : ISg<'msg>) =
                Sg.GlobalEvent(events, sg) :> ISg<'msg>

                
[<AutoOpen>]
module FShadeSceneGraph =

    open Aardvark.SceneGraph
    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering


    type SgEffectBuilder<'a>() =
        inherit EffectBuilder()

        member x.Run(f : unit -> list<FShadeEffect>) =
            let surface = 
                f() 

            fun (sg : ISg<'a>) -> ``F# Sg``.Sg.effect surface sg

    module Sg =
        let shader<'a> = SgEffectBuilder<'a>()

[<AutoOpen>]
module ``Sg Events`` =
    
    module Sg =
        let private simple (kind : SceneEventKind) (f : SceneHit -> 'msg) =
            kind, fun evt -> false, Seq.delay (fun () -> Seq.singleton (f evt))

        let onClick (f : V3d -> 'msg) =
            simple SceneEventKind.Click (fun (evt : SceneHit) -> f evt.globalPosition)
            
        let onDoubleClick (f : V3d -> 'msg) =
            simple SceneEventKind.DoubleClick (fun (evt : SceneHit) -> f evt.globalPosition)
            
        let onMouseDown (f : MouseButtons -> V3d -> 'msg) =
            simple SceneEventKind.Down (fun (evt : SceneHit) -> f evt.buttons evt.globalPosition)
        
        let onMouseDownEvt (f : SceneHit -> 'msg) =
            simple SceneEventKind.Down (fun (evt : SceneHit) -> f evt)
            
        let onMouseMove (f : V3d -> 'msg) =
            simple SceneEventKind.Move (fun (evt : SceneHit) -> f evt.globalPosition)

        let onMouseMoveRay (f : RayPart -> 'msg) =
            simple SceneEventKind.Move (fun (evt : SceneHit) -> f evt.globalRay)

        let onMouseUp (f : V3d -> 'msg) =
            simple SceneEventKind.Up (fun (evt : SceneHit) -> f evt.globalPosition)

        let onEnter (f : V3d -> 'msg) =
            simple SceneEventKind.Enter (fun (evt : SceneHit) -> f evt.globalPosition)
            
        let onLeave (f : unit -> 'msg) =
            simple SceneEventKind.Leave (fun (evt : SceneHit) -> f ())

    module Global=
        let onMouseDown (f : SceneEvent -> 'msg) =
            SceneEventKind.Down, f >> Seq.singleton
        let onMouseMove (f : SceneEvent -> 'msg) =
            SceneEventKind.Move, f >> Seq.singleton
        let onMouseUp (f : SceneEvent -> 'msg) =
            SceneEventKind.Up, f >> Seq.singleton

open Aardvark.Base.Ag

type PickTree<'msg>(sg : ISg<'msg>) =
    let objects : aset<PickObject> = sg?PickObjects()
    let bvh = BvhTree.ofASet PickObject.bounds objects

    let needed = //ASet.ofList [ SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up; SceneEventKind.Move]
        objects |> ASet.collect (fun o -> 
            match Ag.tryGetInhAttribute o.Scope "PickProcessor" with
                | Some (:? SgTools.ISceneHitProcessor<'msg> as proc) ->
                    proc.NeededEvents
                | _ ->
                    ASet.empty
        )

   
    let mutable last = None
    let entered = System.Collections.Generic.HashSet<_>()

    static let intersectLeaf (kind : SceneEventKind) (part : RayPart) (p : PickObject) =
        let pickable = p.Pickable |> Mod.force
        match Pickable.intersect part pickable with
            | Some t -> 
                let pt = part.Ray.Ray.GetPointOnRay t
                match Ag.tryGetInhAttribute p.Scope "PickProcessor" with
                    | Some (:? SgTools.ISceneHitProcessor<'msg> as proc) ->
                        Some <| RayHit(t, proc)
                    | _ ->
                        None
            | None -> 
                None

    member private x.Perform (evt : SceneEvent, bvh : BvhTree<PickObject>, seen : hset<SgTools.ISceneHitProcessor<'msg>>) =
        let intersections = bvh.Intersections(intersectLeaf evt.kind, evt.globalRay)
        use e = intersections.GetEnumerator()

        let rec run (evt : SceneEvent) (seen : hset<SgTools.ISceneHitProcessor<'msg>>) (contEnter : bool) =
            //let topLevel = HSet.isEmpty seen

            if e.MoveNext() then
                let hit = e.Current
                let proc = hit.Value

                if HSet.contains proc seen then
                    run evt seen contEnter
                else
                    let cont, msgs =
                        proc.Process { event = evt; rayT = hit.T }

                    // rethink this stuff 

                    let cc, msgs =
                        if Some proc <> last && contEnter then
                            let l = last
                            let cc,enters = proc.Process { event = { evt with evtKind = SceneEventKind.Enter }; rayT = hit.T } 
                            entered.Add proc |> ignore
                            if not cc then
                                //last <- Some proc
                                false, seq {
                                    match l with
                                        | Some l -> 
                                            let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = hit.T } 
                                            yield! leaves
                                        | None -> 
                                            ()

                                    yield! enters
                                    yield! msgs
                                }
                            else
                                //last <- Some proc
                                true, msgs
                        else 
                            entered.Add proc |> ignore
                            true, msgs

                    
                    if cont then
                        let consumed, rest = run evt (HSet.add proc seen) cc
                        consumed, Seq.append msgs rest
                    else
                        true, msgs

            else
                 match last with
                    | Some l when contEnter -> 
                        if entered.Contains l then false, Seq.empty
                        else
                            last <- None
                            let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 } 
                            false, leaves
                    | _ -> 
                        false, Seq.empty
                
        let oldEntered = entered |> HashSet.toList
        entered.Clear()

        let c, msgs = run evt HSet.empty true
        
        let leaves = 
            seq {
                for o in oldEntered do
                    if entered.Contains o then ()
                    else 
                        let _,msgs =  o.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 } 
                        yield! msgs
            }
        c, seq { yield! leaves; yield! msgs }

//
//
//        match bvh.Intersect(intersectLeaf evt.kind, evt.globalRay) with
//            | Some (hit) ->
//                let trafo, proc = hit.Value
//                let evt = { evt with evtTrafo = trafo }
//
//                let cont, msgs =
//                    if HSet.contains proc seen then
//                        true, []
//                    else
//                        Log.line "hit: %A" hit.T
//                        proc.Process { event = evt; rayT = hit.T }
//
//                let msgs =
//                    if Some proc <> last && topLevel then
//                        let _,enters = proc.Process { event = { evt with evtKind = SceneEventKind.Enter }; rayT = hit.T } 
//                        match last with
//                            | Some l -> 
//                                let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = hit.T } 
//                                last <- Some proc
//                                leaves @ enters @ msgs
//                            | None -> 
//                                last <- Some proc
//                                enters @ msgs
//                    else 
//                        msgs
//
//                if cont then
//                    let evt = { evt with evtRay = RayPart(evt.evtRay.Ray, hit.T + 0.01, evt.evtRay.TMax) }
//                    let consumed, rest = x.Perform(evt, bvh, HSet.add proc seen)
//                    consumed, msgs @ rest
//                else
//                    true, msgs
//
//            | None -> 
//                 match last with
//                    | Some l when topLevel -> 
//                        last <- None
//                        let _,leaves = l.Process { event = { evt with evtKind = SceneEventKind.Leave }; rayT = -1.0 } 
//                        false, leaves
//                    | _ -> 
//                        false, []

    member x.Needed = needed

    member x.Perform(evt : SceneEvent) =
        let bvh = bvh |> Mod.force
        x.Perform(evt,bvh,HSet.empty)
        
    member x.Dispose() =
        bvh.Dispose()

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

module PickTree =
    let ofSg (sg : ISg<'msg>) = new PickTree<'msg>(sg)
    let perform (evt : SceneEvent) (tree : PickTree<'msg>) = tree.Perform(evt)


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
    open Aardvark.SceneGraph.Semantics
    open Aardvark.UI.Sg
    open Aardvark.UI.SgTools.MessageProcessor.Implementation

    type IRuntime with
        member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, sg : ISg<'msg>) =
            x.CompileRender(fbo, config, unbox<Aardvark.SceneGraph.ISg> sg)

        member x.CompileRender(fbo : IFramebufferSignature, sg : ISg<'msg>) =
            x.CompileRender(fbo, unbox<Aardvark.SceneGraph.ISg> sg)

    type GlobalPicks<'msg> = amap<SceneEventKind, SceneEvent -> seq<'msg>> 

    type ISg<'msg> with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.GlobalPicks() : GlobalPicks<'msg> = x?GlobalPicks();
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

        member x.MessageProcessor : IMessageProcessor<'msg> = x?MessageProcessor
        member x.PickProcessor : ISceneHitProcessor = x?PickProcessor


    //let rec allMsgSgs (s : ISg) : aset<ISg<'msg>> =
    //    match s with
    //        | :? ISg<'msg> as s -> ASet.ofList [s]
    //        | :? IApplicator as a -> 
    //            a.Child |> ASet.bind (fun c -> 
    //                let ctx = Ag.getContext()
    //                Ag.useScope (ctx.GetChildScope a) (fun () ->
    //                    allMsgSgs c
    //                )
    //            )
    //        | :? IGroup as g -> g.Children |> ASet.collect allMsgSgs
    //        | _ -> ASet.empty
                
    let rec collectMsgSgs (mapping : ISg<'msg> -> aset<'a>) (s : ISg) : aset<'a> =
        let ctx = Ag.getContext()
        Ag.useScope (ctx.GetChildScope s) (fun () ->
            match s with
                | :? ISg<'msg> as s -> 
                    mapping s

                | :? IApplicator as a -> 
                    a.Child |> ASet.bind (collectMsgSgs mapping)

                | :? IGroup as g -> 
                    g.Children |> ASet.collect (collectMsgSgs mapping)

                | _ -> 
                    ASet.empty
        )
                     

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

        

        member x.GlobalPicks(g : IGroup<'msg>) : GlobalPicks<'msg> =
             // usuperfast
             
             g.Children 
                |> ASet.collect (fun g -> g.GlobalPicks() |> AMap.toASet)
                |> AMap.ofASet
                |> AMap.map (fun k vs e ->
                    seq {
                        for v in vs do
                            yield! v e
                    }
                )

        member x.GlobalPicks(a : IApplicator<'msg>) : GlobalPicks<'msg> =
            a.Child.GlobalPicks()
            
            
        member x.GlobalPicks(a : Sg.Adapter<'msg>) : GlobalPicks<'msg> =
            a.Child 
                |> ASet.bind (collectMsgSgs (fun g -> g.GlobalPicks() |> AMap.toASet))
                |> AMap.ofASet
                |> AMap.map (fun k vs e ->
                    seq {
                        for v in vs do
                            yield! v e
                    }
                )
          

        member x.GlobalPicks(g : Sg.GlobalEvent<'msg>) : GlobalPicks<'msg> =
            let b = g.Child.GlobalPicks()
            let trafo = g.ModelTrafo

            let own = g.Events |> AMap.map (fun k l e -> l { e with evtTrafo = trafo })
            AMap.unionWith (fun k l r -> fun e -> Seq.append (l e) (r e)) own b

            
        member x.GlobalPicks(other : ISg<'msg>) : GlobalPicks<'msg>  =
            AMap.empty

        member x.GlobalPicks(ma : MapApplicator<'i,'o>) : GlobalPicks<'o> =
            let picks = ma.Child.GlobalPicks()
            picks |> AMap.map (fun k v e -> v e |> Seq.collect ma.Mapping)


    [<Semantic>]
    type MessageProcessorSem() =

        member x.MessageProcessor(root : Root<ISg<'msg>>) =
            root.Child?MessageProcessor <- MessageProcessor.id<'msg>
            
        member x.MessageProcessor(app : Sg.MapApplicator<'inner, 'outer>) =
            let parent = app.MessageProcessor
            app.Child?MessageProcessor <- parent.Map(ASet.empty, app.Mapping)

        member x.PickProcessor(root : Root<ISg<'msg>>) =
            root.Child?PickProcessor <- IgnoreProcessor<obj, 'msg>.Instance :> ISceneHitProcessor

        member x.PickProcessor(app : Sg.EventApplicator<'msg>) =
            let msg = app.MessageProcessor

    
            let needed = 
                AMap.keys app.Events |> ASet.map (fun n ->
                    match n with
                        | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                        | _ -> n
                )
 
            let trafo = app.ModelTrafo

            let processor (hit : SceneHit) =
                let evts = app.Events.Content |> Mod.force
                let createArtificialMove = 
                    (HMap.containsKey SceneEventKind.Enter evts || HMap.containsKey SceneEventKind.Leave evts) &&
                    (not <| HMap.containsKey SceneEventKind.Move evts)
                let evts =
                    if createArtificialMove then HMap.add SceneEventKind.Move (fun _ -> true, Seq.empty) evts
                    else evts
                match HMap.tryFind hit.kind evts with
                    | Some cb -> cb { hit with event = { hit.event with evtTrafo = trafo } }
                    | None -> true, Seq.empty

            app.Child?PickProcessor <- msg.MapHit(needed, processor)
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









