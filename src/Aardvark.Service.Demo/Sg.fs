namespace Aardvark.UI

open System.Runtime.CompilerServices

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application

type ISg<'msg> = interface end
  

[<AbstractClass>]
type MessageProcessor<'a>() =
    abstract member ProcessUntyped : 'a -> Option<obj>
    abstract member Choose : ('x -> Option<'a>) -> MessageProcessor<'x>

    abstract member Map : ('x -> 'a) -> MessageProcessor<'x>
    default x.Map (f : 'x -> 'a) =
        x.Choose (f >> Some)

[<AbstractClass>]
type MessageProcessor<'a, 'b>() =
    inherit MessageProcessor<'a>()
    static let unbox =
        if typeof<'a> = typeof<'b> then
            { new MessageProcessor<'a, 'b>() with
                override x.Process a = Some (unbox a)
            }
        else
            { new MessageProcessor<'a, 'b>() with
                override x.Process a = None
            }

    static member Unbox = unbox

    abstract member Process : 'a -> Option<'b>

    override x.ProcessUntyped a = 
        match x.Process a with
            | Some b -> Some (b :> obj)
            | None -> None

    override x.Map(f : 'x -> 'a) : MessageProcessor<'x> =
        { new MessageProcessor<'x, 'b>() with
            member __.Process(v) = x.Process (f v)
        } :> _

    override x.Choose(f : 'x -> Option<'a>) : MessageProcessor<'x> =
        { new MessageProcessor<'x, 'b>() with
            member __.Process(v) = 
                match f v with
                    | Some v -> x.Process v
                    | None -> None
        } :> _

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MessageProcessor =
    let id<'a> = MessageProcessor<'a, 'a>.Unbox
    
    let map (f : 'a -> 'b) (proc : MessageProcessor<'b>) =
        proc.Map(f)

    let choose (f : 'a -> Option<'b>) (proc : MessageProcessor<'b>) =
        proc.Choose(f)


type SgEventKind =
    | Enter
    | Leave
    | Move
    | Click of MouseButtons
    | DoubleClick of MouseButtons
    | Down of MouseButtons
    | Up of MouseButtons

//    | Enter of (V3d -> 'msg)
//    | Leave of (unit -> 'msg)
//    | Move of (V3d -> V3d -> 'msg)
//    | Click of (MouseButtons -> V3d -> 'msg)
//    | DoubleClick of (MouseButtons -> V3d -> 'msg)
//    | Down of (MouseButtons -> V3d -> 'msg)
//    | Up of (MouseButtons -> V3d -> 'msg)

type SgEvent = { kind : SgEventKind; position : V3d; scope : Ag.Scope }

type SgEvent<'msg> = SgEventKind * (SgEvent -> 'msg)

[<AutoOpen>]
module SgAttributes =
    module Sg =
        let onenter (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.Enter, fun evt -> cb evt.position 
                 
        let onleave (cb : unit -> 'msg) : SgEvent<'msg> =
            SgEventKind.Leave, fun evt -> cb()

        let onmousemove (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.Move, fun evt -> cb evt.position

        let onmousedown (button : MouseButtons) (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.Down button, fun evt -> cb evt.position
            
        let onmouseup (button : MouseButtons) (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.Up button, fun evt -> cb evt.position

        let onmouseclick (button : MouseButtons) (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.Click button, fun evt -> cb evt.position

        let onmousedblclick (button : MouseButtons) (cb : V3d -> 'msg) : SgEvent<'msg> =
            SgEventKind.DoubleClick button, fun evt -> cb evt.position

        let onclick (cb : unit -> 'msg) : SgEvent<'msg> =
            SgEventKind.Click MouseButtons.Left, fun evt -> cb()

        let ondblclick (cb : unit -> 'msg) : SgEvent<'msg> =
            SgEventKind.DoubleClick MouseButtons.Left, fun evt -> cb()

module Sg =
    open System
    open System.Collections
    open System.Collections.Generic

    type Adapter<'msg>(sg : ISg) =
        inherit Sg.AbstractApplicator(Mod.constant sg)
        interface ISg<'msg>


    let private box<'msg> (sg : ISg) = Adapter<'msg>(sg) :> ISg<_>
    let private unbox (sg : ISg<'msg>) = sg |> unbox<ISg>
    let private unboxed (f : ISg -> ISg) (sg : ISg<'msg>) =
        sg |> unbox |> f |> box<'msg>

    type Group<'msg>(elements : seq<ISg<'msg>>) =
        let aset = cset(elements)
        let sgASet = aset |> ASet.map unbox

        interface ISg<'msg>

        interface IGroup with
            member x.Children = sgASet

        member x.ASet = aset :> aset<_>

        member x.Add v =
            transact (fun () ->
               aset.Add v
            )

        member x.Remove v =
            transact (fun () ->
                aset.Remove v
            )

        member x.Clear() =
            transact (fun () ->
                aset.Clear()
            )

        member x.UnionWith v =
            transact (fun () ->
                aset.UnionWith v
            )

        member x.ExceptWith v =
            transact (fun () ->
                aset.ExceptWith v
            )

        member x.SymmetricExceptWith v =
            transact (fun () ->
                aset.SymmetricExceptWith v
            )

        member x.IntersectWith v =
            transact (fun () ->
                aset.IntersectWith v
            )


        member x.Count = aset.Count

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = (aset :> System.Collections.IEnumerable).GetEnumerator()

        interface IEnumerable<ISg<'msg>> with
            member x.GetEnumerator() = (aset :> seq<_>).GetEnumerator()

        interface ICollection<ISg<'msg>> with
            member x.IsReadOnly = false
            member x.Add v = x.Add v |> ignore
            member x.Remove v = x.Remove v
            member x.Clear() = x.Clear()
            member x.Contains v = aset.Contains v
            member x.Count = x.Count
            member x.CopyTo(arr, index) =
                let mutable id = index
                for e in aset do
                    arr.[id] <- e
                    id <- id + 1

        interface ISet<ISg<'msg>> with
            member x.Add v = x.Add v
            member x.UnionWith other = x.UnionWith other
            member x.IntersectWith other = x.IntersectWith other
            member x.ExceptWith other = x.ExceptWith other
            member x.SymmetricExceptWith other = x.SymmetricExceptWith other
            member x.IsSubsetOf other = (aset :> ISet<ISg<_>>).IsSubsetOf other
            member x.IsSupersetOf other = (aset :> ISet<ISg<_>>).IsSupersetOf other
            member x.IsProperSubsetOf other = (aset :> ISet<ISg<_>>).IsProperSubsetOf other
            member x.IsProperSupersetOf other = (aset :> ISet<ISg<_>>).IsProperSupersetOf other
            member x.Overlaps other = (aset :> ISet<ISg<_>>).Overlaps other
            member x.SetEquals other = (aset :> ISet<ISg<_>>).SetEquals other

        new() = Group(Seq.empty)

        new([<ParamArray>] items: ISg<'msg>[]) = Group(items |> Array.toSeq)

    type Set<'msg>(elements : aset<ISg<'msg>>) =
        inherit Sg.Set(elements |> ASet.map unbox)
        interface ISg<'msg>

    type EventApplicator<'msg>(events : Map<SgEventKind, SgEvent -> 'msg>, child : IMod<ISg<SgEvent>>) =
        inherit Sg.AbstractApplicator(child |> Mod.map unbox)
        interface ISg<'msg>
        member x.Events = events

    type MapEventApplicator<'a, 'b>(f : 'a -> 'b, child : ISg<'a>) =
        inherit Sg.AbstractApplicator(unbox child)
        interface ISg<'b>
        member x.Function = f

    let pickable (p : PickShape) (sg : ISg<'msg>) =
        sg |> unboxed (Sg.pickable p)

    let pickBoundingBox (sg : ISg<'msg>) =
        sg |> unboxed (Sg.pickBoundingBox)

    let toUntypedSg (sg : ISg<'msg>) = unbox sg

    let noEvents (sg : ISg) : ISg<'msg> = box sg

    let withEvents (events : list<SgEventKind * (SgEvent -> 'msg)>) (sg : ISg<SgEvent>) =
        EventApplicator(Map.ofList events, Mod.constant sg) :> ISg<'msg>

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

    let group (s : #seq<ISg<'msg>>) =
        Group s

    let group' (s : #seq<ISg<'msg>>) =
        Group s :> ISg<'msg>

    let set (set : aset<ISg<'msg>>) =
        Set(set) :> ISg<'msg>

    let ofSeq (s : seq<#ISg<'msg>>) =
        s |> Seq.cast<ISg<'msg>> |> ASet.ofSeq |> Set :> ISg<'msg>

    let ofList (l : list<#ISg<'msg>>) =
        l |> ofSeq

    let ofArray (arr : array<#ISg<'msg>>) =
        arr |> ofSeq
       
    let andAlso (sg : ISg<'msg>) (andSg : ISg<'msg>) = 
        ofList [sg;andSg]


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

    let scopeDependentTexture (sem : Symbol) (tex : Scope -> IMod<ITexture>) (sg : ISg<'msg>) =
        sg |> unboxed (Sg.scopeDependentTexture sem tex)

    let scopeDependentDiffuseTexture (tex : Scope -> IMod<ITexture>) (sg : ISg<'msg>) =
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

[<AbstractClass; Sealed; Extension>]
type IRuntimeExtensions private() =
    [<Extension>]
    static member CompileRender(this : IRuntime, signature : IFramebufferSignature, sg : ISg<'msg>) =
        this.CompileRender(signature, Sg.toUntypedSg sg)

type PickTree<'msg>(sg : ISg<'msg>) =
    let objects : aset<PickObject> = sg?PickObjects()
    let bvh = BvhTree.ofASet PickObject.bounds objects

    let needed = 
        objects |> ASet.collect (fun o ->
            let evts : aset<SgEventKind> = o.Scope?NeededEvents
            evts
        )

    
    static let intersectLeaf (kind : SgEventKind) (part : RayPart) (p : PickObject) =
        let pickable = p.Pickable |> Mod.force
        match Pickable.intersect part pickable with
            | Some t -> 
                let sink = Ag.tryGetInhAttribute p.Scope "MessageProcessor"
                match sink with
                    | Some (:? MessageProcessor<SgEvent, 'msg> as proc) ->
                        let pt = part.Ray.Ray.GetPointOnRay t
                        //let msg = proc.Process { kind = kind; position = pt; scope = p.Scope }
                        Some (RayHit(t, (proc, { kind = kind; position = pt; scope = p.Scope })))
                    | _ ->
                        None
            | None -> None

    let noEvents =
        { new MessageProcessor<SgEvent, 'msg>() with
            member x.Process _ = None
        }

    let mutable last = None
    
    let perform (kind : SgEventKind) (part : RayPart) (bvh : BvhTree<PickObject>) =
        match bvh.Intersect(intersectLeaf kind, part) with
            | Some (hit) ->
                let (proc, evt) = hit.Value

                let boot() =
                    [
                        match proc.Process { evt with kind = SgEventKind.Enter } with
                            | Some msg -> yield msg
                            | None -> ()

                        match proc.Process evt with
                            | Some msg -> yield msg
                            | None -> ()
                    ]
                    

                match last with
                    | Some (_, lastScope) when System.Object.ReferenceEquals(lastScope, evt.scope) ->
                        proc.Process evt |> Option.toList

                    | Some (lastProc, lastScope) ->
                        last <- Some (proc, evt.scope)
                        match lastProc.Process { kind = SgEventKind.Leave; scope = lastScope; position = V3d.Zero } with
                            | Some msg -> msg :: boot()
                            | None -> boot()
  
                    | None ->
                        last <- Some (proc, evt.scope)
                        boot()              
            | None ->
                match last with
                    | Some (lastProc, lastScope) ->
                        last <- None
                        match lastProc.Process { kind = SgEventKind.Leave; scope = lastScope; position = V3d.Zero } with
                            | Some msg -> [msg]
                            | None -> []
                    | None ->
                        []

    member x.Needed = needed

    member x.Perform(kind : SgEventKind, part : RayPart) =
        let bvh = bvh |> Mod.force
        bvh |> perform kind part
        
    member x.Dispose() =
        bvh.Dispose()

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

module PickTree =
    let ofSg (sg : ISg<'msg>) = new PickTree<'msg>(sg)
    let perform (kind : SgEventKind) (part : RayPart) (tree : PickTree<'msg>) = tree.Perform(kind, part)

namespace Aardvark.UI.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.UI

[<AutoOpen>]
module MessageSemantics =

    type ISg<'msg> with
        member x.NeededEvents : aset<SgEventKind> = x?NeededEvents
        member x.MessageProcessor : MessageProcessor<'msg> = x?MessageProcessor

    [<Semantic>]
    type MessageSem() =

        member x.NeededEvents(root : Root<ISg<'msg>>) =
            root.Child?NeededEvents <- ASet.empty<SgEventKind>

        member x.NeededEvents(app : Sg.EventApplicator<'msg>) =
            let parent = app.NeededEvents
            let mine = app.Events |> Map.toSeq |> Seq.map fst |> ASet.ofSeq

            app.Child?NeededEvents <- ASet.union parent mine

        member x.MessageProcessor(app : Sg.EventApplicator<'msg>) =
            let parent = app.MessageProcessor

            app.Child?MessageProcessor <- 
                parent |> MessageProcessor.choose (fun (evt : SgEvent) ->
                    match Map.tryFind evt.kind app.Events with
                        | Some msg -> msg evt |> Some
                        | None -> None
                )

        member x.MessageProcessor(root : Root<ISg<'msg>>) =
            root.Child?MessageProcessor <- MessageProcessor.id<'msg> :> MessageProcessor<'msg>

        member x.MessageSink(app : Sg.MapEventApplicator<'a, 'b>) =
            app.Child?MessageProcessor  <- MessageProcessor.map app.Function app.MessageProcessor
    
