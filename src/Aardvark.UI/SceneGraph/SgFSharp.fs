namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open System

[<AutoOpen>]
module SgFSharp =

    module SgFSharpHelpers =

        let box<'msg> (sg : ISg) =
            sg |> Sg.Adapter :> ISg<'msg>

        let unboxed<'msg> (f : ISg -> ISg) (inner : ISg<'msg>) =
            match inner with
            | :? Sg.Adapter<'msg> as a ->
                a.Child |> AVal.force |> f |> Sg.Adapter :> ISg<'msg>
            | _ ->
                inner |> unbox |> f |> Sg.Adapter :> ISg<'msg>

        type SgEffectBuilder<'msg>() =
            inherit EffectBuilder()

            member x.Run(f : unit -> list<FShadeEffect>) =
                let surface = f()
                unboxed<'msg> (Sg.effect surface)
    module Sg =
        open Aardvark.UI
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
            s |> Seq.cast<ISg<'msg>> |> ASet.ofSeq |> Sg.Set<'msg> :> ISg<'msg>

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

        /// Applies the given activation function to the given scene graph.
        /// An activation function is invoked when the render objects of the scene graph are prepared.
        /// The resulting IDisposable is disposed when the render objects are disposed.
        let onActivation (f : unit -> IDisposable) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.onActivation f)

        /// Generates a scene graph depending on the scope.
        let delay (generator : Ag.Scope -> ISg<'msg>) : ISg<'msg> =
            Sg.DelayNode(fun scope -> generator scope :> ISg) |> noEvents

        // ================================================================================================================
        // Picking
        // ================================================================================================================

        let pickable (p : PickShape) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pickable p)

        let pickable' (p : aval<PickShape>) (sg : ISg<'msg>) =
            sg |> unboxed (fun s -> Sg.PickableApplicator(AVal.map Pickable.ofShape p, AVal.constant s) :> ISg)

        let pickBoundingBox (sg : ISg<'msg>) =
            sg |> unboxed Sg.pickBoundingBox

        let requirePicking (sg : ISg<'msg>) =
            sg |> unboxed Sg.requirePicking

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

            let withGlobalEvents (events : amap<SceneEventKind, SceneEvent -> seq<'msg>>) (sg : ISg<'msg>) =
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
        let inline scopeDependentTexture (name : ^Name) (tex : Ag.Scope -> aval<ITexture>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scopeDependentTexture name tex)

        /// Sets the given scope-dependent diffuse texture.
        let scopeDependentDiffuseTexture (tex : Ag.Scope -> aval<ITexture>) (sg : ISg<'msg>) =
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
        let inline samplerState (name : ^Name) (state : aval<SamplerState>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.samplerState name state)

        /// Sets the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline samplerState' (name : ^Name) (state : SamplerState) (sg : ISg<'msg>) =
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
        let rotation (rollPitchYawInRadians : aval<V3d>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.rotation rollPitchYawInRadians)

        /// Rotates the scene by the given Euler angles.
        let rotation' (rollPitchYawInRadians : V3d) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.rotation' rollPitchYawInRadians)

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
        let colorOutput (enabled : aval<#seq<Symbol>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorOutput (enabled |> AVal.map Set.ofSeq))

        /// Restricts color output to the given attachments.
        let colorOutput' (enabled : Symbol seq) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.colorOutput' (Set.ofSeq enabled))

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
        let writeBuffers (buffers : aval<#seq<WriteBuffer>>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers (buffers |> AVal.map Set.ofSeq))

        /// Toggles color, depth and stencil writes according to the given set of symbols.
        let writeBuffers' (buffers : WriteBuffer seq) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.writeBuffers' (Set.ofSeq buffers))

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
        let frontFacing (order : aval<WindingOrder>) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.frontFacing order)

        /// Sets the winding order of front faces.
        let frontFacing' (order : WindingOrder) (sg: ISg<'msg>) =
            sg |> unboxed (Sg.frontFacing' order)


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
        // Viewport
        // ================================================================================================================

        /// <summary>
        /// Sets a custom viewport for the scene.
        /// The viewport is the region of the framebuffer that will be rendered to.
        /// It determines the transformation from normalized device coordinates to framebuffer coordinates.
        /// </summary>
        /// <remarks>
        /// The default viewport is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The viewport to set. Min and Max are the framebuffer coordinates of the viewport's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the viewport to.</param>
        let viewport (region : aval<Box2i>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.viewport region)

        /// <summary>
        /// Sets a custom viewport for the scene.
        /// The viewport is the region of the framebuffer that will be rendered to.
        /// It determines the transformation from normalized device coordinates to framebuffer coordinates.
        /// </summary>
        /// <remarks>
        /// The default viewport is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The viewport to set. Min and Max are the framebuffer coordinates of the viewport's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the viewport to.</param>
        let viewport' (region : Box2i) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.viewport' region)

        /// <summary>
        /// Sets a custom scissor for the scene.
        /// The scissor is the region of the framebuffer that can be modified by the render task.
        /// Fragments with coordinates outside the scissor region will be discarded.
        /// </summary>
        /// <remarks>
        /// The default scissor is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The scissor to set. Min and Max are the framebuffer coordinates of the scissor's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the scissor to.</param>
        let scissor (region : aval<Box2i>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scissor region)

        /// <summary>
        /// Sets a custom scissor for the scene.
        /// The scissor is the region of the framebuffer that can be modified by the render task.
        /// Fragments with coordinates outside the scissor region will be discarded.
        /// </summary>
        /// <remarks>
        /// The default scissor is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The scissor to set. Min and Max are the framebuffer coordinates of the scissor's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the scissor to.</param>
        let scissor' (region : Box2i) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.scissor' region)

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
        let inline vertexArray (name : ^Name) (value : Array) (sg : ISg<'msg>) =
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
        let inline instanceArray (name : ^Name) (value : Array) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceArray name value)

        /// Provides an instance attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline instanceBufferValue (name : ^Name) (value : aval< ^Value>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.instanceBufferValue name value)

        /// Provides an instance attribute with the given name by supplying a single value.
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
        let indexArray (value : Array) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.indexArray value)

        // ================================================================================================================
        // Drawing
        // ================================================================================================================

        /// Applies the given effects to the scene.
        let shader<'msg> = SgEffectBuilder<'msg>()

        /// Applies the given effects to the scene.
        let effect (e : seq<FShadeEffect>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.effect e)
                |> shader { do! DefaultSurfaces.constantColor C4f.Zero }

        /// Applies the given surface to the scene.
        let inline surface (s : ^Surface) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.surface s)

        /// Applies the given pool of effects to the scene.
        /// The index active determines which effect is used at a time.
        let effectPool (effects : FShade.Effect[]) (active : aval<int>) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.effectPool effects active)

        /// Applies the given render pass.
        let pass (pass : RenderPass) (sg : ISg<'msg>) =
            sg |> unboxed (Sg.pass pass)

        /// Draws an adaptive set of managed draw calls of the given pool.
        let pool (pool : ManagedPool) (mode : IndexedGeometryMode) (calls : aset<ManagedDrawCall>) =
            calls |> Sg.pool pool mode |> box<'msg>

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

        /// Supplies the draw calls in the given indirect buffer with the given geometry mode.
        let inline indirectDraw' (mode : IndexedGeometryMode) (buffer : IndirectBuffer) =
            Sg.indirectDraw' mode buffer |> box<'msg>

        /// Creates a draw call from the given indexed geometry.
        let ofIndexedGeometry (g : IndexedGeometry) =
            Sg.ofIndexedGeometry g |> box<'msg>

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
            sg |> unboxed Sg.normalizeAdaptive

        /// Transforms the scene so its bounding box spans from -1 to 1 in all dimensions.
        let normalize (sg : ISg<'msg>) =
            sg |> unboxed Sg.normalize

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
            simple SceneEventKind.Leave (fun (_ : SceneHit) -> f ())

    module Global=
        let onMouseDown (f : SceneEvent -> 'msg) = SceneEventKind.Down, f >> Seq.singleton
        let onMouseMove (f : SceneEvent -> 'msg) = SceneEventKind.Move, f >> Seq.singleton
        let onMouseUp (f : SceneEvent -> 'msg)   = SceneEventKind.Up,   f >> Seq.singleton