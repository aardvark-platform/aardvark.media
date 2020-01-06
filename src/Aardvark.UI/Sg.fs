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

type Continuation =
    | Continue
    | Stop


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
        evtKind         : SceneEventKind
        evtRay          : RayPart
        evtPixel        : V2i
        evtView         : Trafo3d
        evtProj         : Trafo3d
        evtViewport     : V2i
        evtButtons      : MouseButtons
        evtAlt          : bool
        evtShift        : bool
        evtCtrl         : bool
        evtTrafo        : IMod<Trafo3d>
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
   
    [<AllowNullLiteral>]
    type SceneEventHandler(parent : SceneEventHandler, needed : aset<SceneEventKind>, before : option<SceneEvent -> option<float> -> Continuation>, after : option<SceneEvent -> option<float> -> Continuation>) =
        
        let level =
            if isNull parent then 0
            else parent.Level + 1

        let needed =
            lazy (
                if isNull parent then needed
                else ASet.union parent.Needed needed
            )

        member x.Level : int = level

        member x.Parent = parent

        member x.Needed = needed.Value

        member x.Before(evt : SceneEvent, rayT : option<float>) : Continuation =
            let cont = 
                if not (isNull parent) then parent.Before(evt, rayT)
                else Continue

            if cont = Continue then
                match before with
                | Some before -> before evt rayT
                | None -> Continue
            else
                cont

        member x.After(evt : SceneEvent, rayT : option<float>) : Continuation =
            let cont = 
                match after with
                | Some after -> after evt rayT
                | None -> Continue

            if cont = Continue && not (isNull parent) then
                parent.After(evt, rayT)
            else
                cont

        member private x.RunAll(evt : SceneEvent, rayT : option<float>) =
            match before with 
            | Some before -> before evt rayT |> ignore
            | None -> ()

            match after with 
            | Some after -> after evt rayT |> ignore
            | None -> ()


        member x.Process(evt : SceneEvent, rayT : option<float>) : Continuation =
            match x.Before(evt, rayT) with
            | Continue -> x.After(evt, rayT)
            | Stop -> Stop


        static member TransitionTo(evt : SceneEvent, rayT : option<float>, src : option<SceneEventHandler>, dst: option<SceneEventHandler>) =
            match src with
            | None ->
                match dst with
                | Some dst ->
                    let rec run (e : SceneEventHandler) =
                        if not (isNull e) then 
                            run e.Parent
                            e.RunAll({ evt with evtKind = SceneEventKind.Enter }, rayT)
                    run dst
                | None ->
                    ()
            | Some src -> 
                match dst with
                | None ->
                    let mutable c = src
                    while not (isNull c) do
                        c.RunAll({ evt with evtKind = SceneEventKind.Leave }, rayT)
                        c <- c.Parent
                | Some dst ->
                    if src <> dst then 
                        let mutable dst = dst
                        let mutable dstTree = []
                        let mutable src = src
                        while dst.Level > src.Level do
                            dstTree <- dst :: dstTree
                            dst <- dst.Parent
                            
                        while src.Level > dst.Level do
                            src.RunAll({ evt with evtKind = SceneEventKind.Leave }, rayT)
                            src <- src.Parent

                        if dst = src then
                            for d in dstTree do
                                d.RunAll({ evt with evtKind = SceneEventKind.Enter }, rayT)
                        else
                            let rec run (l : SceneEventHandler) (r : SceneEventHandler) =
                                if l <> r then
                                    l.RunAll({ evt with evtKind = SceneEventKind.Leave }, rayT)
                                    run l.Parent r.Parent
                                    r.RunAll({ evt with evtKind = SceneEventKind.Enter }, rayT)
                            run src dst

                            for d in dstTree do
                                d.RunAll({ evt with evtKind = SceneEventKind.Enter }, rayT)




        static member Create(parent : SceneEventHandler, sink : seq<'msg> -> unit, needed : aset<SceneEventKind>, ?before : SceneEvent -> option<float> -> Continuation * seq<'msg>, ?after : SceneEvent -> option<float> -> Continuation * seq<'msg>) =
            let before = 
                match before with
                | Some before -> Some (fun e t -> let (cont, msgs) = before e t in sink msgs; cont)
                | None -> None

            let after = 
                match after with
                | Some after -> Some (fun e t -> let (cont, msgs) = after e t in sink msgs; cont)
                | None -> None

            SceneEventHandler(parent, needed, before, after)



    //type ISceneHitProcessor =
    //    abstract member NeededEvents : aset<SceneEventKind>

    //type ISceneHitProcessor<'a> =
    //    inherit ISceneHitProcessor
    //    abstract member Process : SceneHit -> bool * seq<'a>
        

    //type IMessageProcessor<'a> =
    //    abstract member NeededEvents : aset<SceneEventKind>
    //    abstract member Map : aset<SceneEventKind> * ('x -> seq<'a>) -> IMessageProcessor<'x>
    //    abstract member MapHit : aset<SceneEventKind> * (SceneHit -> bool * seq<'a>) -> ISceneHitProcessor

    //type IMessageProcessor<'a, 'b> =
    //    inherit IMessageProcessor<'a>
    //    abstract member Process : 'a -> seq<'b>


    //module MessageProcessor =
    //    [<AutoOpen>]
    //    module Implementation =

    //        type HitProcessor<'a>(needed : aset<SceneEventKind>, mapping : SceneHit -> bool * seq<'a>) =
    //            member x.Process(msg : SceneHit) =
    //                mapping msg

    //            interface ISceneHitProcessor with
    //                member x.NeededEvents = needed

    //            interface ISceneHitProcessor<'a> with
    //                member x.Process hit = x.Process hit

    //        type Processor<'a, 'b>(needed : aset<SceneEventKind>, mapping : 'a -> seq<'b>) =
    //            member x.Map(newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) =
    //                Processor<'x, 'b>(ASet.union needed newNeeded, f >> Seq.collect mapping) :> IMessageProcessor<'x, 'b>
                    
    //            member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
    //                let f x =
    //                    let cont, msgs = f x
    //                    cont, Seq.collect mapping msgs

    //                HitProcessor<'b>(ASet.union needed newNeeded, f) :> ISceneHitProcessor<'b>

    //            member x.Process(msg : 'a) =
    //                mapping msg

    //            interface IMessageProcessor<'a> with
    //                member x.NeededEvents = needed
    //                member x.Map (newNeeded : aset<SceneEventKind>, f : 'x -> seq<'a>) = x.Map(newNeeded, f) :> IMessageProcessor<'x>
    //                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) = x.MapHit(newNeeded, f) :> ISceneHitProcessor
                        
    //            interface IMessageProcessor<'a, 'b> with
    //                member x.Process msg = x.Process msg

    //        type IdentityProcessor<'a> private() =
                
    //            static let instance = IdentityProcessor<'a>() :> IMessageProcessor<'a>

    //            static member Instance = instance

    //            interface IMessageProcessor<'a, 'a> with
    //                member x.NeededEvents = ASet.empty

    //                member x.Map(needed : aset<SceneEventKind>, f : 'x -> seq<'a>) =
    //                    Processor<'x, 'a>(needed, f) :> IMessageProcessor<_>
                        
    //                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
    //                    HitProcessor<'a>(newNeeded, f) :> ISceneHitProcessor

    //                member x.Process(msg : 'a) =
    //                    Seq.singleton msg

    //        type IgnoreProcessor<'a, 'b> private() =
                
    //            static let instance = IgnoreProcessor<'a, 'b>() 

    //            static member Instance = instance

    //            interface ISceneHitProcessor<'b> with
    //                member x.NeededEvents = ASet.empty
    //                member x.Process hit = true, Seq.empty

    //            interface IMessageProcessor<'a, 'b> with
    //                member x.NeededEvents = ASet.empty

    //                member x.Map(newNeeded : aset<_>, f : 'x -> seq<'a>) =
    //                    IgnoreProcessor<'x, 'b>.Instance :> IMessageProcessor<'x>
                        
    //                member x.MapHit(newNeeded : aset<SceneEventKind>, f : SceneHit -> bool * seq<'a>) =
    //                    IgnoreProcessor<obj, 'b>.Instance :> ISceneHitProcessor

    //                member x.Process(msg : 'a) =
    //                    Seq.empty
                

    //    let id<'msg> = IdentityProcessor<'msg>.Instance

    //    let ignore<'a, 'b> = IgnoreProcessor<'a, 'b>.Instance

    //    let map (newNeeded : aset<SceneEventKind>) (mapping : 'x -> 'a) (p : IMessageProcessor<'a>) =
    //        p.Map(newNeeded, mapping >> Seq.singleton)

    //    let choose (newNeeded : aset<SceneEventKind>) (mapping : 'x -> Option<'a>) (p : IMessageProcessor<'a>) =
    //        p.Map(newNeeded, mapping >> Option.map Seq.singleton >> Option.defaultValue Seq.empty)

    //    let collect (newNeeded : aset<SceneEventKind>) (mapping : 'x -> seq<'a>) (p : IMessageProcessor<'a>) =
    //        p.Map(newNeeded, mapping)


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

    type EventApplicator<'msg>(events : amap<SceneEventKind, SceneHit -> Continuation * seq<'msg>>, child : ISg<'msg>) =
        inherit AbstractApplicator<'msg>(child)
        member x.Events = events
        
    type CaptureEventApplicator<'msg>(events : amap<SceneEventKind, SceneHit -> Continuation * seq<'msg>>, child : ISg<'msg>) =
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

        let withEvents (events : list<SceneEventKind * (SceneHit -> Continuation * seq<'msg>)>) (sg : ISg<'msg>) =
            Sg.EventApplicator(AMap.ofList events, sg) :> ISg<'msg>
 
        let withCaptureEvents (events : list<SceneEventKind * (SceneHit -> Continuation * seq<'msg>)>) (sg : ISg<'msg>) =
            Sg.CaptureEventApplicator(AMap.ofList events, sg) :> ISg<'msg>
 
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

        let depthBias (m : IMod<DepthBiasState>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthBias m)

        let frontFace (m : IMod<WindingOrder>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.frontFace m)

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
            let withEvents (events : amap<SceneEventKind, SceneHit -> Continuation * seq<'msg>>) (sg : ISg<'msg>) =
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
            kind, fun evt -> Continue, Seq.delay (fun () -> Seq.singleton (f evt))

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

type IMessageSink<'msg> =
    abstract member Invoke : seq<'msg> -> unit

type PickTree<'msg>(sg : ISg<'msg>) =
    let list = System.Collections.Generic.List<'msg>()
    let sink (msgs: seq<'msg>) =
        lock list (fun () ->
            list.AddRange msgs
        )


    let objects : aset<PickObject> = 
        sg?MessageSink <- { new IMessageSink<'msg> with member x.Invoke(msgs) = sink msgs }
        sg?PickObjects()

    let bvh = 
        BvhTree.ofASet PickObject.bounds objects

    let needed = //ASet.ofList [ SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up; SceneEventKind.Move]
        objects |> ASet.collect (fun o -> 
            match Ag.tryGetInhAttribute o.Scope "SceneEventHandler" with
                | Some (:? SgTools.SceneEventHandler as proc) ->
                    proc.Needed
                | _ ->
                    ASet.empty
        )

   
    let mutable last : option<SgTools.SceneEventHandler> = None

    let consume (cont : Continuation) =
        let all = 
            lock list (fun () ->
                let all = list.ToArray()
                list.Clear()
                all
            )

        cont, all :> seq<_>



    static let intersectLeaf (part : RayPart) (p : PickObject) =
        let pickable = p.Pickable |> Mod.force
        match Pickable.intersect part pickable with
            | Some t -> 
                match Ag.tryGetInhAttribute p.Scope "SceneEventHandler" with
                    | Some (:? SgTools.SceneEventHandler as proc) ->
                        Some <| RayHit(t, proc)
                    | _ ->
                        None
            | None -> 
                None

    member private x.Perform (evt : SceneEvent, bvh : BvhTree<PickObject>, seen : hset<SgTools.SceneEventHandler>) =
        let intersection = bvh.Intersections(intersectLeaf, evt.globalRay) |> Seq.tryHead
        match intersection with
        | Some hit ->
            let proc = hit.Value
            if evt.kind = SceneEventKind.Move then
                SgTools.SceneEventHandler.TransitionTo(evt, Some hit.T, last, Some proc)
            last <- Some proc
            proc.Process(evt, Some hit.T) |> consume
        | None ->
            if evt.kind = SceneEventKind.Move then
                SgTools.SceneEventHandler.TransitionTo(evt, None, last, None)
            last <- None
            consume Continue

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

    type IRuntime with
        member x.CompileRender(fbo : IFramebufferSignature, config : BackendConfiguration, sg : ISg<'msg>) =
            x.CompileRender(fbo, config, unbox<Aardvark.SceneGraph.ISg> sg)

        member x.CompileRender(fbo : IFramebufferSignature, sg : ISg<'msg>) =
            x.CompileRender(fbo, unbox<Aardvark.SceneGraph.ISg> sg)

    type GlobalPicks<'msg> = amap<SceneEventKind, SceneEvent -> seq<'msg>> 

    type ISg<'msg> with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.PickObjects() : aset<PickObject> = x?PickObjects()
        member x.GlobalPicks() : GlobalPicks<'msg> = x?GlobalPicks()
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()
        
        member x.MessageSink : IMessageSink<'msg> = x?MessageSink
        member x.SceneEventHandler : SceneEventHandler = x?SceneEventHandler


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

        member x.MessageSink(root : Root<ISg<'msg>>) =
            root.Child?MessageSink <- { new IMessageSink<'msg> with member x.Invoke(_) = () }

        member x.MessageSink(map : Sg.MapApplicator<'a, 'b>) =
            let parent = map.MessageSink
            let sink = 
                { new IMessageSink<'a> with 
                    member x.Invoke(a) = 
                        a |> Seq.collect map.Mapping |> parent.Invoke 
                }
            map.Child?MessageSink <- sink

        member x.SceneEventHandler(root : Root<ISg<'msg>>) =
            let h : SgTools.SceneEventHandler = null
            root.Child?SceneEventHandler <- h

        member x.SceneEventHandler(app : Sg.EventApplicator<'msg>) =
            let sink = app.MessageSink
            let parent = app.SceneEventHandler

            let needed = 
                AMap.keys app.Events |> ASet.map (fun n ->
                    match n with
                        | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                        | _ -> n
                )
 
            let trafo = app.ModelTrafo

            let processor (evt : SceneEvent) (rayT : option<float>) =
                let evts = app.Events.Content |> Mod.force
                let createArtificialMove = 
                    (HMap.containsKey SceneEventKind.Enter evts || HMap.containsKey SceneEventKind.Leave evts) &&
                    (not <| HMap.containsKey SceneEventKind.Move evts)
                let evts =
                    if createArtificialMove then HMap.add SceneEventKind.Move (fun _ -> Continue, Seq.empty) evts
                    else evts
                match HMap.tryFind evt.kind evts with
                | Some cb -> 
                    match rayT with
                    | Some rayT -> cb { event = { evt with evtTrafo = trafo }; rayT = rayT }
                    | None -> cb { event = { evt with evtTrafo = trafo }; rayT = System.Double.PositiveInfinity }
                | None -> 
                    Continue, Seq.empty
                    
            let handler =
                SceneEventHandler.Create(
                    parent, sink.Invoke,
                    needed,
                    after = processor
                )

            app.Child?SceneEventHandler <- handler

        member x.SceneEventHandler(app : Sg.CaptureEventApplicator<'msg>) =
            let sink = app.MessageSink
            let parent = app.SceneEventHandler

            let needed = 
                AMap.keys app.Events |> ASet.map (fun n ->
                    match n with
                        | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                        | _ -> n
                )
 
            let trafo = app.ModelTrafo

            let processor (evt : SceneEvent) (rayT : option<float>) =
                let evts = app.Events.Content |> Mod.force
                let createArtificialMove = 
                    (HMap.containsKey SceneEventKind.Enter evts || HMap.containsKey SceneEventKind.Leave evts) &&
                    (not <| HMap.containsKey SceneEventKind.Move evts)
                let evts =
                    if createArtificialMove then HMap.add SceneEventKind.Move (fun _ -> Continue, Seq.empty) evts
                    else evts
                match HMap.tryFind evt.kind evts with
                | Some cb -> 
                    match rayT with
                    | Some rayT -> cb { event = { evt with evtTrafo = trafo }; rayT = rayT }
                    | None -> cb { event = { evt with evtTrafo = trafo }; rayT = System.Double.PositiveInfinity }
                | None -> 
                    Continue, Seq.empty
                    
            let handler =
                SceneEventHandler.Create(
                    parent, sink.Invoke,
                    needed,
                    before = processor
                )

            app.Child?SceneEventHandler <- handler
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









