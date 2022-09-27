namespace Aardvark.UI

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Geometry
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Traceable
open Aardvark.SceneGraph
open Aardvark.Application
open Suave.Logging


module AMap =
    let keys (m : amap<'k, 'v>) : aset<'k> =
        ASet.ofReader (fun () ->
            let reader = m.GetReader()
            { new AbstractReader<HashSetDelta<'k>>(HashSetDelta.empty) with

                member x.Compute(token) =
                    let ops = reader.GetChanges token

                    ops |> HashMapDelta.toHashMap |> HashMap.map (fun key op ->
                        match op with
                            | Set _ -> +1
                            | Remove -> -1
                    ) |> HashSetDelta.ofHashMap
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
        evtTrafo        : aval<Trafo3d>
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
            member x.Child = child |> unbox |> AVal.constant

        interface IApplicator<'msg> with
            member x.Child = child

        member x.Child = child

    type MapApplicator<'inner, 'outer>(mapping : 'inner -> seq<'outer>, child : ISg<'inner>) =
        interface Aardvark.SceneGraph.IApplicator with
            member x.Child = child |> unbox |> AVal.constant

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

    module SgFSharpHelpers =

        let box<'msg> (sg : ISg) =
            sg |> Sg.Adapter :> ISg<'msg>

        let unboxed (f : Aardvark.SceneGraph.ISg -> Aardvark.SceneGraph.ISg) (inner : ISg<'msg>) =
            match inner with
            | :? Sg.Adapter<'msg> as a ->
                a.Child |> AVal.force |> f |> Sg.Adapter :> ISg<'msg>
            | _ ->
                inner |> unbox |> f |> Sg.Adapter :> ISg<'msg>

    module Sg =
        open SgFSharpHelpers

        // ================================================================================================================
        // Utilities
        // ================================================================================================================

        let noEvents (sg : ISg) : ISg<'msg> =
            match sg with
            | :? ISg<'msg> as isgMsg ->
                Log.warn "[Media] superfluous use of Sg.noEvents, returning input as is"
                isgMsg
            | _ -> box sg

        let toUntypedSg (sg : ISg<'msg>) = unbox sg

        /// Combines the scene graphs in the given adaptive set.
        let set (set : aset<ISg<'msg>>) =
            Sg.Set<'msg>(set) :> ISg<'msg>

        /// Combines the scene graphs in the given sequence.
        let ofSeq (s : seq<#ISg<'msg>>) =
            s |> Seq.cast<ISg<'msg>> |> ASet.ofSeq |> Sg.Set :> ISg<'msg>

        /// Combines the scene graphs in the given list.
        let ofList (l : list<#ISg<'msg>>) =
            l |> ofSeq

        /// Combines the scene graphs in the given array.
        let ofArray (arr : array<#ISg<'msg>>) =
            arr |> ofSeq

        /// Combines two scene graphs.
        let andAlso (sg : ISg<'msg>) (andSg : ISg<'msg>) =
            ofList [sg; andSg]

        /// Maps the messages of the scene according to the given function.
        let map (f : 'a -> 'b) (a : ISg<'a>) : ISg<'b> =
            Sg.MapApplicator<'a,'b>(f >> Seq.singleton,a) :> ISg<_>

        /// Empty scene graph.
        let empty<'msg> : ISg<'msg> = Sg.empty |> noEvents

        /// Unwraps an adaptive scene graph.
        let dynamic (s : aval<ISg<'msg>>) =
            Sg.DynamicNode(AVal.map unbox s) |> box<'msg>

        /// Toggles visibility of the scene.
        let onOff (active : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.onOff active)

        /// Inserts an arbitrary object as node in the scene graph.
        let adapter (o : obj) =
            Sg.adapter o |> box

        // ================================================================================================================
        // Picking
        // ================================================================================================================

        let pickable (p : PickShape) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickable p)

        let pickable' (p : aval<PickShape>) (sg : ISg<'msg>) =
            sg |> unboxed (fun s -> new Sg.PickableApplicator(AVal.map Pickable.ofShape p, AVal.constant s) :> ISg)

        let pickBoundingBox (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickBoundingBox)

        let requirePicking (sg : ISg<'msg>) =
            sg |> unboxed (Sg.requirePicking)

        // ================================================================================================================
        // Events
        // ================================================================================================================

        let withEvents (events : list<SceneEventKind * (SceneHit -> bool * seq<'msg>)>) (sg : ISg<'msg>) =
            Sg.EventApplicator(AMap.ofList events, sg) :> ISg<'msg>

        let withGlobalEvents (events : list<SceneEventKind * (SceneEvent -> seq<'msg>)>) (sg : ISg<'msg>) =
            Sg.GlobalEvent(AMap.ofList events, sg) :> ISg<'msg>

        module Incremental =
            let withEvents (events : amap<SceneEventKind, SceneHit -> bool * seq<'msg>>) (sg : ISg<'msg>) =
                Sg.EventApplicator(events, sg) :> ISg<'msg>

            let withGlobalEvents (events : amap<SceneEventKind, (SceneEvent -> seq<'msg>)>) (sg : ISg<'msg>) =
                Sg.GlobalEvent(events, sg) :> ISg<'msg>

        // ================================================================================================================
        // Uniforms & Textures
        // ================================================================================================================

        /// Sets the uniform with the given name to the given value.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline uniform (name : ^Name) (value : aval<'Value>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.uniform name value)

        /// Sets the uniform with the given name to the given value.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline uniform' (name : ^Name) (value : 'Value) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.uniform' name value)


        /// Sets the given texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline texture (name : ^Name) (tex : aval<'Texture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.texture name tex)

        /// Sets the given texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline texture' (name : ^Name) (tex : ITexture) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.texture' name tex)


        /// Sets the given diffuse texture.
        let diffuseTexture (tex : aval<#ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseTexture tex)

        /// Sets the given diffuse texture.
        let diffuseTexture' (tex : ITexture) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseTexture' tex)


        /// Loads and sets the given texture file to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline fileTexture (name : ^Name) (path : string) (wantMipMaps : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.fileTexture name path wantMipMaps)

        /// Loads and sets the given diffuse texture file.
        let diffuseFileTexture (path : string) (wantMipMaps : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.diffuseFileTexture path wantMipMaps)


        /// Sets the given scope-dependent texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline scopeDependentTexture (name : ^Name) (tex : Scope -> aval<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scopeDependentTexture name tex)

        /// Sets the given scope-dependent diffuse texture.
        let scopeDependentDiffuseTexture (tex : Scope -> aval<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scopeDependentDiffuseTexture tex)


        /// Sets the given runtime-dependent texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline runtimeDependentTexture (name : ^Name) (tex : IRuntime -> aval<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.runtimeDependentTexture name tex)

        /// Sets the given runtime-dependent diffuse texture.
        let runtimeDependentDiffuseTexture(tex : IRuntime -> aval<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.runtimeDependentDiffuseTexture tex)


        /// Sets the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline samplerState (name : ^Name) (state : aval<SamplerState option>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.samplerState name state)

        /// Sets the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline samplerState' (name : ^Name) (state : Option<SamplerState>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.samplerState' name state)


        /// Modifies the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline modifySamplerState (name : ^Name) (modifier : aval<SamplerState -> SamplerState>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.modifySamplerState name modifier)

        /// Modifies the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline modifySamplerState' (name : ^Name) (modifier : SamplerState -> SamplerState) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.modifySamplerState' name modifier)

        // ================================================================================================================
        // Trafos
        // ================================================================================================================

        /// Sets the model transformation.
        let trafo (m : aval<Trafo3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.trafo m)

        /// Sets the model transformation.
        let trafo' (m : Trafo3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.trafo' m)

        /// Sets the model transformation.
        let transform (m : Trafo3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.transform m)


        /// Sets the view transformation.
        let viewTrafo (m : aval<Trafo3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.viewTrafo m)

        /// Sets the view transformation.
        let viewTrafo' (m : Trafo3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.viewTrafo' m)


        /// Sets the projection transformation.
        let projTrafo (m : aval<Trafo3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.projTrafo m)

        /// Sets the projection transformation.
        let projTrafo' (m : Trafo3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.projTrafo' m)


        /// Sets the view and projection transformations according to the given camera.
        let camera (cam : aval<Camera>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.camera cam)

        /// Sets the view and projection transformations according to the given camera.
        let camera' (cam : Camera) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.camera' cam)


        /// Scales the scene by the given scaling factors.
        let scaling (s : aval<V3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scaling s)

        /// Scales the scene by the given scaling factors.
        let scaling' (s : V3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scaling' s)

        /// Scales the scene by a uniform factor.
        let scale (s : float) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scale s)


        /// Translates the scene by the given vector.
        let translation (v : aval<V3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.translation v)

        /// Translates the scene by the given vector.
        let translation' (v : V3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.translation' v)

        /// Translates the scene by the given vector.
        let translate (x : float) (y : float) (z : float) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.translate x y z)


        /// Rotates the scene by the given Euler angles.
        let rotate (rollInRadians : float) (pitchInRadians : float) (yawInRadians : float) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.rotate rollInRadians pitchInRadians yawInRadians)

        // ================================================================================================================
        // Blending
        // ================================================================================================================

        /// Sets the global blend mode for all color attachments.
        let blendMode (mode : aval<BlendMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendMode mode)

        /// Sets the global blend mode for all color attachments.
        let blendMode' (mode : BlendMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendMode' mode)


        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes (modes : aval<Map<Symbol, BlendMode>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendModes modes)

        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes' (modes : Map<Symbol, BlendMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendModes' modes)


        /// Sets the blend constant color.
        /// The color must be compatible with C4f.
        let inline blendConstant (color : aval< ^Value>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendConstant color)

        /// Sets the blend constant color.
        /// The color must be compatible with C4f.
        let inline blendConstant' (color : ^Value) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.blendConstant' color)


        /// Sets the global color write mask for all color attachments.
        let colorMask (mask : aval<ColorMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorMask mask)

        /// Sets the global color write mask for all color attachments.
        let colorMask' (mask : ColorMask) (sg : ISg<'msg>)  =
            sg |> unboxed (Sg.colorMask' mask)


        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks (masks : aval<Map<Symbol, ColorMask>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorMasks masks)

        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks' (masks : Map<Symbol, ColorMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorMasks' masks)


        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite (enabled : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorWrite enabled)

        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite' (enabled : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorWrite' enabled)


        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites (enabled : aval<Map<Symbol, bool>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorWrites enabled)

        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites' (enabled : Map<Symbol, bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorWrites' enabled)


        /// Restricts color output to the given attachments.
        let colorOutput (enabled : aval<Set<Symbol>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorOutput enabled)

        /// Restricts color output to the given attachments.
        let colorOutput' (enabled : Set<Symbol>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorOutput' enabled)

        // ================================================================================================================
        // Depth
        // ================================================================================================================

        /// Sets the depth test.
        let depthTest (test : aval<DepthTest>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthTest test)

        /// Sets the depth test.
        let depthTest' (test : DepthTest) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthTest' test)


        /// Enables or disables depth writing.
        let depthWrite (depthWriteEnabled : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthWrite depthWriteEnabled)

        /// Enables or disables depth writing.
        let depthWrite' (depthWriteEnabled : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.depthWrite' depthWriteEnabled)


        /// Sets the depth bias.
        let depthBias (bias : aval<DepthBias>) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.depthBias bias)

        /// Sets the depth bias.
        let depthBias' (bias : DepthBias) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.depthBias' bias)


        /// Enables or disables depth clamping.
        let depthClamp (clamp : aval<bool>) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.depthClamp clamp)

        /// Enables or disables depth clamping.
        let depthClamp' (clamp : bool) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.depthClamp' clamp)

        // ================================================================================================================
        // Stencil
        // ================================================================================================================

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront (mode : aval<StencilMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModeFront mode)

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront' (mode : StencilMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModeFront' mode)


        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront (mask : aval<StencilMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMaskFront mask)

        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront' (mask : StencilMask) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMaskFront' mask)


        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront (enabled : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteFront enabled)

        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront' (enabled : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteFront' enabled)


        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack (mode : aval<StencilMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModeBack mode)

        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack' (mode : StencilMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModeBack' mode)


        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack (mask : aval<StencilMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMaskBack mask)

        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack' (mask : StencilMask) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMaskBack' mask)


        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack (enabled : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteBack enabled)

        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack' (enabled : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteBack' enabled)


        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes (front : aval<StencilMode>) (back : aval<StencilMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModes front back)

        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes' (front : StencilMode) (back : StencilMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilModes' front back)


        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks (front : aval<StencilMask>) (back : aval<StencilMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMasks front back)

        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks' (front : StencilMask) (back : StencilMask) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMasks' front back)


        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites (front : aval<bool>) (back : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWrites front back)

        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites' (front : bool) (back : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWrites' front back)


        /// Sets the stencil mode.
        let stencilMode (mode : aval<StencilMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilMode mode)

        /// Sets the stencil mode.
        let stencilMode' (mode : StencilMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilMode' mode)


        /// Sets the stencil write mask.
        let stencilWriteMask (mask : aval<StencilMask>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMask mask)

        /// Sets the stencil write mask.
        let stencilWriteMask' (mask : StencilMask) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWriteMask' mask)


        /// Enables or disables stencil write.
        let stencilWrite (enabled : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWrite enabled)

        /// Enables or disables stencil write.
        let stencilWrite' (enabled : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.stencilWrite' enabled)

        // ================================================================================================================
        // Write buffers
        // ================================================================================================================

        /// Toggles color, depth and stencil writes according to the given set of symbols.
        let writeBuffers (buffers : aval<Set<WriteBuffer>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers buffers)

        /// Toggles color, depth and stencil writes according to the given set of symbols.
        let writeBuffers' (buffers : Set<WriteBuffer>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers' buffers)

        // ================================================================================================================
        // Rasterizer
        // ================================================================================================================

        /// Sets the cull mode.
        let cullMode (mode : aval<CullMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.cullMode mode)

        /// Sets the cull mode.
        let cullMode' (mode : CullMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.cullMode' mode)


        /// Sets the winding order of front faces.
        let frontFace (order : aval<WindingOrder>) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.frontFace order)

        /// Sets the winding order of front faces.
        let frontFace' (order : WindingOrder) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.frontFace' order)


        /// Sets the fill mode.
        let fillMode (mode : aval<FillMode>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.fillMode mode)

        /// Sets the fill mode.
        let fillMode' (mode : FillMode) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.fillMode' mode)


        /// Toggles multisampling for the scene.
        let multisample (mode : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.multisample mode)

        /// Toggles multisampling for the scene.
        let multisample' (mode : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.multisample' mode)


        /// Toggles conservative rasterization for the scene.
        let conservativeRaster (mode : aval<bool>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.conservativeRaster mode)

        /// Toggles conservative rasterization for the scene.
        let conservativeRaster' (mode : bool) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.conservativeRaster' mode)

        // ================================================================================================================
        // Attributes & Indices
        // ================================================================================================================

        /// Provides a vertex attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline vertexAttribute (name : ^Name) (value : aval<'Value[]>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexAttribute name value)

        /// Provides a vertex attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline vertexAttribute' (name : ^Name) (value : 'Value[]) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexAttribute' name value)

        /// Provides a vertex attribute with the given name by supplying a BufferView.
        /// The name can be a string or Symbol.
        let inline vertexBuffer (name : ^Name) (view : BufferView) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexBuffer name view)

        /// Provides a vertex attribute with the given name by supplying an untyped array.
        /// The name can be a string or Symbol.
        let inline vertexArray (name : ^Name) (value : System.Array) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexArray name value)

        /// Provides a vertex attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline vertexBufferValue (name : ^Name) (value : aval< ^Value>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexBufferValue name value)

        /// Provides a vertex attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline vertexBufferValue' (name : ^Name) (value : ^Value) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.vertexBufferValue' name value)


        /// Provides an instance attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline instanceAttribute (name : ^Name) (value : aval<'Value[]>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceAttribute name value)

        /// Provides an instance attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline instanceAttribute' (name : ^Name) (value : 'Value[]) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceAttribute' name value)

        /// Provides an index attribute with the given name by supplying a BufferView.
        /// The name can be a string or Symbol.
        let inline instanceBuffer (name : ^Name) (view : BufferView) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceBuffer name view)

        /// Provides an index attribute with the given name by supplying an untyped array.
        /// The name can be a string or Symbol.
        let inline instanceArray (name : ^Name) (value : System.Array) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceArray name value)

        /// Provides a instance attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline instanceBufferValue (name : ^Name) (value : aval< ^Value>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceBufferValue name value)

        /// Provides a instance attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline instanceBufferValue' (name : ^Name) (value : ^Value) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceBufferValue' name value)


        /// Provides the given vertex indices.
        let index<'msg, 'Value when 'Value : struct> (value : aval<'Value[]>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.index value)

        /// Provides the given vertex indices.
        let index'<'msg, 'Value when 'Value : struct> (value : 'Value[]) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.index' value)

        /// Provides vertex indices by supplying a BufferView.
        let indexBuffer (view : BufferView) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.indexBuffer view)

         /// Provides vertex indices by supplying an untyped array.
        let indexArray (value : System.Array) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.indexArray value)

        // ================================================================================================================
        // Drawing
        // ================================================================================================================

        /// Applies the given effects to the scene.
        let effect (e : seq<FShadeEffect>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.effect e)

        /// Applies the given surface to the scene.
        let surface (m : ISurface) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.surface m)

        /// Applies the given render pass.
        let pass (pass : RenderPass) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pass pass)

        /// Draws an adaptive set of managed draw calls of the given pool.
        let pool (pool : ManagedPool) (mode : IndexedGeometryMode) (calls : aset<ManagedDrawCall>) =
            mode |> Sg.pool pool calls |> box<'msg>

        /// Draws an adaptive set of indexed geometries with instance attributes.
        let geometrySetInstanced (signature : GeometrySignature) (mode : IndexedGeometryMode) (geometries : aset<GeometryInstance>) =
            geometries |> Sg.geometrySetInstanced signature mode |> box<'msg>

        /// Draws an adaptive set of indexed geometries.
        let geometrySet (mode : IndexedGeometryMode) (attributeTypes : Map<Symbol, Type>) (geometries : aset<IndexedGeometry>) =
            geometries |> Sg.geometrySet mode attributeTypes |> box<'msg>

        /// Creates a single draw call for the given geometry mode.
        let draw (mode : IndexedGeometryMode) =
            Sg.draw mode |> box<'msg>

        /// Supplies the given draw call with the given geometry mode.
        let render (mode : IndexedGeometryMode) (call : DrawCallInfo) =
            Sg.render mode call |> box<'msg>

        /// Supplies the draw calls in the given indirect buffer with the given geometry mode.
        let indirectDraw (mode : IndexedGeometryMode) (buffer : aval<IndirectBuffer>) =
            Sg.indirectDraw mode buffer |> box<'msg>

        /// Creates a draw call from the given indexed geometry.
        let ofIndexedGeometry (g : IndexedGeometry) =
            Sg.ofIndexedGeometry g |> box<'msg>

        /// Creates a draw call from the given indexed geometry, using an interleaved buffer
        /// for the vertex attributes.
        let ofIndexedGeometryInterleaved (attributes : list<Symbol>) (g : IndexedGeometry) =
            Sg.ofIndexedGeometryInterleaved attributes g |> box<'msg>

        /// Creates a draw call, supplying the given transformations as per-instance attributes with
        /// name DefaultSemantic.InstanceTrafo.
        let instancedGeometry (trafos : aval<Trafo3d[]>) (g : IndexedGeometry) =
            Sg.instancedGeometry trafos g |> box<'msg>

        // ================================================================================================================
        // Bounding boxes
        // ================================================================================================================

        /// Adaptively transforms the scene so its bounding box aligns with the given box.
        let normalizeToAdaptive (box : Box3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeToAdaptive box)

        /// Transforms the scene so its bounding box aligns with the given box.
        let normalizeTo (box : Box3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeTo box)

        /// Adaptively transforms the scene so its bounding box spans from -1 to 1 in all dimensions.
        let normalizeAdaptive (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalizeAdaptive)

        /// Transforms the scene so its bounding box spans from -1 to 1 in all dimensions.
        let normalize (sg : ISg<'msg>) =
            sg |> unboxed (Sg.normalize)

[<AutoOpen>]
module FShadeSceneGraph =

    type SgEffectBuilder<'a>() =
        inherit EffectBuilder()

        member x.Run(f : unit -> list<FShadeEffect>) =
            let surface =
                f()

            fun (sg : ISg<'a>) -> Sg.effect surface sg

    module Sg =
        /// Applies the given effects to the scene.
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

type PickTree<'msg>(sg : ISg<'msg>) =
    let objects : aset<PickObject> = sg?PickObjects(Ag.Scope.Root)
    let bvh = BvhTree.ofASet PickObject.bounds objects

    let needed = //ASet.ofList [ SceneEventKind.Click; SceneEventKind.DoubleClick; SceneEventKind.Down; SceneEventKind.Up; SceneEventKind.Move]
        objects |> ASet.collect (fun o ->
            match o.Scope.TryGetInherited "PickProcessor" with
                | Some (:? SgTools.ISceneHitProcessor<'msg> as proc) ->
                    proc.NeededEvents
                | _ ->
                    ASet.empty
        )


    let mutable last = None
    let entered = System.Collections.Generic.HashSet<_>()

    static let intersectLeaf (kind : SceneEventKind) (part : RayPart) (p : PickObject) =
        let pickable = p.Pickable |> AVal.force
        match Pickable.intersect part pickable with
            | Some t ->
                let pt = part.Ray.Ray.GetPointOnRay t
                match p.Scope.TryGetInherited "PickProcessor" with
                    | Some (:? SgTools.ISceneHitProcessor<'msg> as proc) ->
                        Some <| RayHit(t, proc)
                    | _ ->
                        None
            | None ->
                None

    member private x.Perform (evt : SceneEvent, bvh : BvhTree<PickObject>, seen : HashSet<SgTools.ISceneHitProcessor<'msg>>) =
        let intersections = bvh.Intersections(intersectLeaf evt.kind, evt.globalRay)
        use e = intersections.GetEnumerator()

        let rec run (evt : SceneEvent) (seen : HashSet<SgTools.ISceneHitProcessor<'msg>>) (contEnter : bool) =
            //let topLevel = HSet.isEmpty seen

            if e.MoveNext() then
                let hit = e.Current
                let proc = hit.Value

                if HashSet.contains proc seen then
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
                        let consumed, rest = run evt (HashSet.add proc seen) cc
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

        let oldEntered = entered |> Aardvark.Base.HashSet.toList
        entered.Clear()

        let c, msgs = run evt HashSet.empty true

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
        let bvh = bvh |> AVal.force
        x.Perform(evt,bvh,HashSet.empty)

    member x.Dispose() =
        //bvh.Dispose()
        ()
    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

module PickTree =
    let ofSg (sg : ISg<'msg>) = new PickTree<'msg>(sg)
    let perform (evt : SceneEvent) (tree : PickTree<'msg>) = tree.Perform(evt)


namespace Aardvark.UI.Semantics
open Aardvark.UI
open Aardvark.UI.SgTools
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open Aardvark.Base.Ag


type private PickObject = Aardvark.SceneGraph.``Sg Picking Extensions``.PickObject


[<AutoOpen>]
module ``Message Semantics`` =
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics
    open Aardvark.UI.Sg
    open Aardvark.UI.SgTools.MessageProcessor.Implementation

    type GlobalPicks<'msg> = amap<SceneEventKind, SceneEvent -> seq<'msg>>

    type ISg<'msg> with
        member x.RenderObjects(scope : Ag.Scope) : aset<IRenderObject> = x?RenderObjects(scope)
        member x.PickObjects(scope : Ag.Scope) : aset<PickObject> = x?PickObjects(scope)
        member x.GlobalPicks(scope : Ag.Scope) : GlobalPicks<'msg> = x?GlobalPicks(scope)
        member x.GlobalBoundingBox(scope : Ag.Scope) : aval<Box3d> = x?GlobalBoundingBox(scope)
        member x.LocalBoundingBox(scope : Ag.Scope) : aval<Box3d> = x?LocalBoundingBox(scope)

    type Ag.Scope with
        member x.MessageProcessor() : IMessageProcessor<'msg> = x?MessageProcessor
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
        match s with
            | :? ISg<'msg> as s ->
                mapping s

            | :? IApplicator as a ->
                a.Child |> ASet.bind (collectMsgSgs mapping)

            | :? IGroup as g ->
                g.Children |> ASet.collect (collectMsgSgs mapping)

            | _ ->
                ASet.empty


    [<Rule>]
    type StandardSems() =

//        member x.RenderObjects(app : IApplicator<'msg>) =
//            aset {
//                let c = app.Child
//                yield! c.RenderObjects()
//            }

        member x.RenderObjects(g : IGroup<'msg>, scope : Ag.Scope) =
            aset {
                for c in g.Children do
                    yield! c.RenderObjects(scope)
            }

//        member x.PickObjects(app : IApplicator<'msg>) =
//            aset {
//                let c = app.Child
//                yield! c.PickObjects()
//            }

        member x.PickObjects(g : IGroup<'msg>, scope : Ag.Scope) =
            aset {
                for c in g.Children do
                    yield! c.PickObjects(scope)
            }

//        member x.GlobalBoundingBox(app : IApplicator<'msg>) =
//            let c = app.Child
//            c.GlobalBoundingBox()

        member x.GlobalBoundingBox(g : IGroup<'msg>, scope : Ag.Scope) =
            let add (l : Box3d) (r : Box3d) =
                Box3d(l,r)

            let trySub (s : Box3d) (b : Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children
            |> ASet.mapA (fun c -> c.GlobalBoundingBox(scope))
            |> ASet.foldHalfGroup add trySub Box3d.Invalid

//        member x.LocalBoundingBox(app : IApplicator<'msg>) =
//            let c = app.Child
//            c.LocalBoundingBox()

        member x.LocalBoundingBox(g : IGroup<'msg>, scope : Ag.Scope) =
            let add (l : Box3d) (r : Box3d) =
                Box3d(l,r)

            let trySub (s : Box3d) (b : Box3d) =
                if b.Max.AllSmaller s.Max && b.Min.AllGreater s.Min then
                    Some s
                else
                    None

            g.Children
            |> ASet.mapA (fun c -> c.LocalBoundingBox(scope))
            |> ASet.foldHalfGroup add trySub Box3d.Invalid



        member x.GlobalPicks(g : IGroup<'msg>, scope : Ag.Scope) : GlobalPicks<'msg> =
             // usuperfast

             g.Children
                |> ASet.collect (fun g -> g.GlobalPicks(scope) |> AMap.toASet)
                |> AMap.ofASet
                |> AMap.map (fun k vs e ->
                    seq {
                        for v in vs do
                            yield! v e
                    }
                )

        member x.GlobalPicks(a : IApplicator<'msg>, scope : Ag.Scope) : GlobalPicks<'msg> =
            a.Child.GlobalPicks(scope)


        member x.GlobalPicks(a : Sg.Adapter<'msg>, scope : Ag.Scope) : GlobalPicks<'msg> =
            a.Child
            |> ASet.bind (collectMsgSgs (fun g -> g.GlobalPicks(scope) |> AMap.toASet))
            |> AMap.ofASet
            |> AMap.map (fun k vs e ->
                seq {
                    for v in vs do
                        yield! v e
                }
            )


        member x.GlobalPicks(g : Sg.GlobalEvent<'msg>, scope : Ag.Scope) : GlobalPicks<'msg> =
            let b = g.Child.GlobalPicks(scope)
            let trafo = scope.ModelTrafo

            let own = g.Events |> AMap.map (fun k l e -> l { e with evtTrafo = trafo })
            AMap.unionWith (fun k l r -> fun e -> Seq.append (l e) (r e)) own b


        member x.GlobalPicks(other : ISg<'msg>, scope : Ag.Scope) : GlobalPicks<'msg>  =
            AMap.empty

        member x.GlobalPicks(ma : MapApplicator<'i,'o>, scope : Ag.Scope) : GlobalPicks<'o> =
            let picks = ma.Child.GlobalPicks(scope)
            picks |> AMap.map (fun k v e -> v e |> Seq.collect ma.Mapping)


    [<Rule>]
    type MessageProcessorSem() =

        member x.MessageProcessor(root : Root<ISg<'msg>>, scope : Ag.Scope) =
            root.Child?MessageProcessor <- MessageProcessor.id<'msg>

        member x.MessageProcessor(app : Sg.MapApplicator<'inner, 'outer>, scope : Ag.Scope) =
            let parent = scope.MessageProcessor()
            app.Child?MessageProcessor <- parent.Map(ASet.empty, app.Mapping)

        member x.PickProcessor(root : Root<ISg<'msg>>, scope : Ag.Scope) =
            root.Child?PickProcessor <- IgnoreProcessor<obj, 'msg>.Instance :> ISceneHitProcessor

        member x.PickProcessor(app : Sg.EventApplicator<'msg>, scope : Ag.Scope) =
            let msg = scope.MessageProcessor()


            let needed =
                AMap.keys app.Events |> ASet.map (fun n ->
                    match n with
                        | SceneEventKind.Enter | SceneEventKind.Leave -> SceneEventKind.Move
                        | _ -> n
                )

            let trafo = scope.ModelTrafo

            let processor (hit : SceneHit) =
                let evts = app.Events.Content |> AVal.force
                let createArtificialMove =
                    (HashMap.containsKey SceneEventKind.Enter evts || HashMap.containsKey SceneEventKind.Leave evts) &&
                    (not <| HashMap.containsKey SceneEventKind.Move evts)
                let evts =
                    if createArtificialMove then HashMap.add SceneEventKind.Move (fun _ -> true, Seq.empty) evts
                    else evts
                match HashMap.tryFind hit.kind evts with
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




