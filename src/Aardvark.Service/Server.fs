#nowarn "9"
#nowarn "1337"
namespace Aardvark.Service

open System
open System.Threading
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open Aardvark.Application
open System.Diagnostics

type Command =
    | Invalidate
    | WorldPosition of pos : V3d

type RenderQuality =
    {
        quality     : float
        scale       : float
        framerate   : float
    }
    
module RenderQuality =
    let full =
        { 
            quality = 90.0
            scale = 1.0
            framerate = 60.0
        }


[<AutoOpen>]
module TimeExtensions =
    open System.Diagnostics
    let private sw = Stopwatch()
    let private start = MicroTime(TimeSpan.FromTicks(DateTime.Now.Ticks))
    do sw.Start()

    type MicroTime with
        static member Now = start + sw.MicroTime

module Config =
    let mutable deleteTimeout = 1000

type ClientInfo =
    {
        token : AdaptiveToken
        signature : IFramebufferSignature
        targetId : string
        sceneName : string
        session : Guid
        size : V2i
        samples : int
        quality : RenderQuality
        time : MicroTime
        clearColor : C4f
    }

type ClientState =
    {
        viewTrafo   : Trafo3d
        projTrafo   : Trafo3d
    }
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClientState =
    let pickRay (pp : PixelPosition) (state : ClientState) =
        let n = pp.NormalizedPosition
        let ndc = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0)
        let ndcNeg = V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, -1.0)

        let p = state.projTrafo.Backward.TransformPosProj ndc
        let pNeg = state.projTrafo.Backward.TransformPosProj ndcNeg

        let viewDir = (p - pNeg) |> Vec.normalize
        let ray = Ray3d(pNeg, viewDir)
        ray.Transformed(state.viewTrafo.Backward)


type ClientValues internal(_signature : IFramebufferSignature) =
    
    let _time = AVal.init MicroTime.Zero
    let _session = AVal.init Guid.Empty
    let _size = AVal.init V2i.II
    let _viewTrafo = AVal.init Trafo3d.Identity
    let _projTrafo = AVal.init Trafo3d.Identity
    let _samples = AVal.init 1

    member internal x.Update(info : ClientInfo, state : ClientState) =
        _time.Value <- info.time
        _session.Value <- info.session

        _size.Value <- info.size
        _viewTrafo.Value <- state.viewTrafo
        _projTrafo.Value <- state.projTrafo
        _samples.Value <- info.samples
//
//        _size.MarkOutdated()
//        _viewTrafo.MarkOutdated()
//        _projTrafo.MarkOutdated()
//        _samples.MarkOutdated()
//        _session.MarkOutdated()

    member x.runtime : IRuntime = unbox _signature.Runtime
    member x.signature = _signature
    member x.size = _size :> aval<_>
    member x.time = _time :> aval<_>
    member x.session = _session :> aval<_>
    member x.viewTrafo = _viewTrafo :> aval<_>
    member x.projTrafo = _projTrafo :> aval<_>
    member x.samples = _samples :> aval<_>


[<AbstractClass>]
type Scene() =
    let cache = ConcurrentDictionary<IFramebufferSignature, ConcreteScene>()
    let clientInfos = ConcurrentDictionary<Guid * string, unit -> Option<ClientInfo * ClientState>>()


    member internal x.AddClientInfo(session : Guid, id : string, getter : unit -> Option<ClientInfo * ClientState>) =
        clientInfos.TryAdd((session, id), getter) |> ignore
        
    member internal x.RemoveClientInfo(session : Guid, id : string) =
        clientInfos.TryRemove((session, id)) |> ignore

    member x.TryGetClientInfo(session : Guid, id : string) : Option<ClientInfo * ClientState> =
        match clientInfos.TryGetValue ((session, id)) with
            | (true, getter) -> getter()
            | _ -> None

    member x.GetConcreteScene(name : string, signature : IFramebufferSignature) =
        cache.GetOrAdd(signature, fun signature -> ConcreteScene(name, signature, x))

    abstract member Compile : ClientValues -> IRenderTask

and ConcreteScene(name : string, signature : IFramebufferSignature, scene : Scene) as this =
    inherit AdaptiveObject()

    let mutable refCount = 0
    let mutable task : Option<IRenderTask> = None

    let state = ClientValues(signature)


    let destroy (o : obj) =
        let deadTask = 
            lock this (fun () ->
                if refCount = 0 then
                    match task with
                        | Some t -> 
                            task <- None
                            Some t
                        | None -> 
                            Log.error "[Scene] %s: invalid state" name
                            None
                else
                    None
            )

        match deadTask with
            | Some t ->
                // TODO: fix in rendering
                transact ( fun () -> t.Dispose() )
                Log.line "[Scene] %s: destroyed" name
            | None ->
                ()
    
    let timer = new Timer(TimerCallback(destroy), null, Timeout.Infinite, Timeout.Infinite)

    let create() =
        refCount <- refCount + 1
        if refCount = 1 then
            match task with
                | Some task -> 
                    // refCount was 0 but task was not deleted
                    timer.Change(Timeout.Infinite, Timeout.Infinite) |> ignore
                    task
                | None ->
                    // refCount was 0 and there was no task
                    Log.line "[Scene] %s: created" name
                    let t = scene.Compile state
                    task <- Some t
                    t
        else
            match task with
                | Some t -> t
                | None -> failwithf "[Scene] %s: invalid state" name
        
    let release() =
        refCount <- refCount - 1
        if refCount = 0 then
            timer.Change(Config.deleteTimeout, Timeout.Infinite) |> ignore
            
    member x.Scene = scene
                    
    member internal x.Apply(info : ClientInfo, s : ClientState) =
        state.Update(info, s)

    member internal x.State = state

    member internal x.CreateNewRenderTask() =
        lock x (fun () ->
            let task = create()

            { new AbstractRenderTask() with
                member x.FramebufferSignature = task.FramebufferSignature
                member x.Runtime = task.Runtime
                member x.PerformUpdate(t,rt) = task.Update(t, rt)
                member x.Perform(t,rt,o) =  task.Run(t, rt, o)
                
                member x.Release() = 
                    lock x (fun () ->
                        release()
                    )
                
                member x.Use f = task.Use f
    
            } :> IRenderTask
        )

    member x.FramebufferSignature = signature

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Scene =

    [<AutoOpen>]
    module Implementation = 
        type EmptyScene() =
            inherit Scene()
            override x.Compile(_) = RenderTask.empty

        type ArrayScene(scenes : Scene[]) =
            inherit Scene()
            override x.Compile(v) =
                scenes |> Array.map (fun s -> s.Compile v) |> RenderTask.ofArray

        type CustomScene(compile : ClientValues -> IRenderTask) =
            inherit Scene()
            override x.Compile(v) = compile v
            

    let empty = EmptyScene() :> Scene

    let custom (compile : ClientValues -> IRenderTask) = CustomScene(compile) :> Scene

    let ofArray (scenes : Scene[]) = ArrayScene(scenes) :> Scene
    let ofList (scenes : list<Scene>) = ArrayScene(List.toArray scenes) :> Scene
    let ofSeq (scenes : seq<Scene>) = ArrayScene(Seq.toArray scenes) :> Scene
    
        
type Server =
    {
        runtime         : IRuntime
        content         : string -> Option<Scene>
        rendered        : ClientInfo -> unit
        getState        : ClientInfo -> Option<ClientState>
        compressor      : Option<JpegCompressor>
        fileSystemRoot  : Option<string>
    }
    

module private ReadPixel =
    module private Vulkan =
        open Aardvark.Rendering.Vulkan

        let downloadDepth (pixel : V2i) (img : Image) =
            let temp = img.Device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 sizeof<uint32>)
            img.Device.perform {
                do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
                //do! Command.TransformLayout(temp, VkImageLayout.TransferDstOptimal)
                //do! Command.Copy(img.[TextureAspect.Depth, 0, 0], V3i(pixel, 0), img.[TextureAspect.Depth, 0, 0], V3i.Zero, V3i.III)
                do! Command.Copy(img.[TextureAspect.Depth, 0, 0], V3i(pixel.X, img.Size.Y - 1 - pixel.Y, 0), temp, 0L, V2i.Zero, V3i.III)
                do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
            }

            let result = temp.Memory.Mapped (fun ptr -> NativeInt.read<uint32> ptr)
            let frac = float (result &&& 0xFFFFFFu) / float ((1 <<< 24) - 1)
            temp.Dispose()
            frac

    let downloadDepth (pixel : V2i) (img : IBackendTexture) =
        match img with
            | :? Aardvark.Rendering.Vulkan.Image as img -> Vulkan.downloadDepth pixel img |> Some
            | _ -> None


[<CompilerMessage("internal use", 1337, IsHidden = true)>]
type MapImage = 
    {
        name : string
        length : int
        size : V2i
    }

[<CompilerMessage("internal use", 1337, IsHidden = true)>]
type RenderResult =
    | Jpeg of byte[]
    | Mapping of MapImage
    | Png of byte[]

