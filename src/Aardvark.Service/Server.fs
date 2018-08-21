#nowarn "9"
namespace Aardvark.Service

open System
open System.Text
open System.Net
open System.Threading
open System.Collections.Concurrent

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application
open System.Diagnostics

open System.IO.MemoryMappedFiles
open Microsoft.FSharp.NativeInterop


#nowarn "9"

type Message =
    | RequestImage of background : C4b * size : V2i
    | RequestWorldPosition of pixel : V2i
    | Rendered
    | Shutdown
    | Change of scene : string * samples : int

type Command =
    | Invalidate
    | WorldPosition of pos : V3d

[<AutoOpen>]
module private Tools = 
    //open TurboJpegWrapper
    open System.Diagnostics
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices
    open System.Security
    open Microsoft.FSharp.NativeInterop
    
    type private Compressor =
        class
            [<DefaultValue; ThreadStatic>]
            static val mutable private instance : TJCompressor

            static member Instance =
                if isNull Compressor.instance then  
                    Compressor.instance <- new TJCompressor()

                Compressor.instance

        end
        
    [<AutoOpen>]
    module Vulkan = 
        open Aardvark.Rendering.Vulkan

        let downloadFBO (jpeg : TJCompressor) (size : V2i) (quality : int) (fbo : Framebuffer) =
            let device = fbo.Device
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[ImageAspect.Color, 0, 0]

            let tmp = device.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)
            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(color, tmp)
                do! Command.TransformLayout(color.Image, oldLayout)
            }
            
            let rowSize = 4 * size.X
            let alignedRowSize = rowSize

            let result = 
                tmp.Volume.Mapped (fun src ->
                    jpeg.Compress(
                        NativePtr.toNativeInt src.Pointer, alignedRowSize, size.X, size.Y, 
                        TJPixelFormat.RGBX, 
                        TJSubsampling.S444, 
                        quality, 
                        TJFlags.BottomUp ||| TJFlags.ForceSSE3
                    )
                )

            device.Delete tmp
            result

        let downloadFBOMS (jpeg : TJCompressor) (size : V2i) (quality : int) (fbo : Framebuffer) =
            let device = fbo.Device
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[ImageAspect.Color, 0, 0]

            let tempImage = Image.create (V3i(size,1)) 1 1 1 TextureDimension.Texture2D TextureFormat.Rgba8 (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit) device

            let tmp = device.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)
            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)
                do! Command.ResolveMultisamples(color.Image.[ImageAspect.Color, 0, 0], V3i.Zero, tempImage.[ImageAspect.Color, 0, 0], V3i.Zero, color.Image.Size)
                do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(tempImage.[ImageAspect.Color, 0, 0], tmp)
                do! Command.TransformLayout(color.Image, oldLayout)
            }
            
            let rowSize = 4 * size.X
            let alignedRowSize = rowSize

            let result = 
                tmp.Volume.Mapped (fun src ->
                    jpeg.Compress(
                        NativePtr.toNativeInt src.Pointer, alignedRowSize, size.X, size.Y, 
                        TJPixelFormat.RGBX, 
                        TJSubsampling.S444, 
                        quality, 
                        TJFlags.BottomUp ||| TJFlags.ForceSSE3
                    )
                )

            device.Delete tmp
            result

        type Framebuffer with
            member x.DownloadJpegColor(quality : int) =
                let jpeg = Compressor.Instance
                if x.Attachments.[DefaultSemantic.Colors].Image.Samples <> 1 then
                    downloadFBOMS jpeg x.Size quality x
                else
                    downloadFBO jpeg x.Size quality x

    [<AutoOpen>]
    module GL = 
        open OpenTK.Graphics
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL

        let private downloadFBO (jpeg : TJCompressor) (size : V2i) (quality : int) (ctx : Context) =
            let pbo = GL.GenBuffer()

            let rowSize = 3 * size.X
            let align = ctx.PackAlignment
            let alignedRowSize = (rowSize + (align - 1)) &&& ~~~(align - 1)
            let sizeInBytes = alignedRowSize * size.Y
            try
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)
                GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)

                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgb, PixelType.UnsignedByte, 0n)

                let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint sizeInBytes, BufferAccessMask.MapReadBit)

                jpeg.Compress(
                    ptr, alignedRowSize, size.X, size.Y, 
                    TJPixelFormat.RGB, 
                    TJSubsampling.S444, 
                    quality, 
                    TJFlags.BottomUp ||| TJFlags.ForceSSE3
                )

            finally
                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                GL.DeleteBuffer(pbo)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

        type Framebuffer with
            member x.DownloadJpegColor(quality : int) =
                let jpeg = Compressor.Instance
                let ctx = x.Context
                use __ = ctx.ResourceLock

                let color = x.Attachments.[DefaultSemantic.Colors] |> unbox<Renderbuffer>

                let size = color.Size
                if color.Samples > 1 then
                    let resolved = GL.GenRenderbuffer()
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, resolved)
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, color.Size.X, color.Size.Y)
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

                    let fbo = GL.GenFramebuffer()
                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
                    GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, resolved)

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)

                    GL.BlitFramebuffer(
                        0, 0, size.X - 1, size.Y - 1, 
                        0, 0, size.X - 1, size.Y - 1,
                        ClearBufferMask.ColorBufferBit,
                        BlitFramebufferFilter.Nearest
                    )

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)
                    
                    try
                        ctx |> downloadFBO jpeg size quality
                    finally
                        GL.DeleteFramebuffer(fbo)
                        GL.DeleteRenderbuffer(resolved)

                else
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)
                    ctx |> downloadFBO jpeg size quality

    type IFramebuffer with
        member x.DownloadJpegColor(quality : int) =
            match x with
                | :? Aardvark.Rendering.GL.Framebuffer as fbo -> fbo.DownloadJpegColor(quality)
                | :? Aardvark.Rendering.Vulkan.Framebuffer as fbo -> fbo.DownloadJpegColor(quality)
                | _ -> failwith "not implemented"
 
    type WebSocket with
        member x.readMessage() =
            socket {
                let! (t,d,fin) = x.read()
                if fin then 
                    return (t,d)
                else
                    let! (_, rest) = x.readMessage()
                    return (t, Array.append d rest)
            }

[<AutoOpen>]
module TimeExtensions =
    open System.Diagnostics
    let private sw = Stopwatch()
    let private start = MicroTime(TimeSpan.FromTicks(DateTime.Now.Ticks))
    do sw.Start()

    type MicroTime with
        static member Now = start + sw.MicroTime



type ClientInfo =
    {
        token : AdaptiveToken
        signature : IFramebufferSignature
        targetId : string
        sceneName : string
        session : Guid
        size : V2i
        samples : int
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
    
    let _time = Mod.init MicroTime.Zero
    let _session = Mod.init Guid.Empty
    let _size = Mod.init V2i.II
    let _viewTrafo = Mod.init Trafo3d.Identity
    let _projTrafo = Mod.init Trafo3d.Identity
    let _samples = Mod.init 1

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

    member x.runtime = _signature.Runtime
    member x.signature = _signature
    member x.size = _size :> IMod<_>
    member x.time = _time :> IMod<_>
    member x.session = _session :> IMod<_>
    member x.viewTrafo = _viewTrafo :> IMod<_>
    member x.projTrafo = _projTrafo :> IMod<_>
    member x.samples = _samples :> IMod<_>


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

    member internal x.GetConcreteScene(name : string, signature : IFramebufferSignature) =
        cache.GetOrAdd(signature, fun signature -> ConcreteScene(name, signature, x))

    abstract member Compile : ClientValues -> IRenderTask

and internal ConcreteScene(name : string, signature : IFramebufferSignature, scene : Scene) as this =
    inherit AdaptiveObject()

    static let deleteTimeout = 1000

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
            timer.Change(deleteTimeout, Timeout.Infinite) |> ignore
            
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
                member x.Perform(t,rt,o) = task.Run(t, rt, o)
                
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
                //do! Command.Copy(img.[ImageAspect.Depth, 0, 0], V3i(pixel, 0), img.[ImageAspect.Depth, 0, 0], V3i.Zero, V3i.III)
                do! Command.Copy(img.[ImageAspect.Depth, 0, 0], V3i(pixel.X, img.Size.Y - 1 - pixel.Y, 0), temp, 0L, V2i.Zero, V3i.III)
                do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
            }

            let result = temp.Memory.Mapped (fun ptr -> NativeInt.read<uint32> ptr)
            let frac = float (result &&& 0xFFFFFFu) / float ((1 <<< 24) - 1)
            img.Device.Delete temp
            frac

    let downloadDepth (pixel : V2i) (img : IBackendTexture) =
        match img with
            | :? Aardvark.Rendering.Vulkan.Image as img -> Vulkan.downloadDepth pixel img |> Some
            | _ -> None

type internal MapImage = 
    {
        name : string
        length : int
        size : V2i
    }

type internal RenderResult =
    | Jpeg of byte[]
    | Mapping of MapImage

[<AbstractClass>]
type internal ClientRenderTask internal(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene) =
    let runtime = server.runtime
    let mutable task = RenderTask.empty
    
    let targetSize = Mod.init V2i.II
    
    let mutable currentSize = -V2i.II
    let mutable currentSignature = Unchecked.defaultof<IFramebufferSignature>
    
    let mutable depth : Option<IRenderbuffer> = None 
    let mutable color : Option<IRenderbuffer> = None
    let mutable target : Option<IFramebuffer> = None 

    static let mutable threadCount = 0


    let deleteFramebuffer() =
        target     |> Option.iter runtime.DeleteFramebuffer
        depth      |> Option.iter runtime.DeleteRenderbuffer
        color      |> Option.iter runtime.DeleteRenderbuffer
        target <- None
        color <- None
        depth <- None
        currentSignature <- Unchecked.defaultof<IFramebufferSignature>
        currentSize <- -V2i.II

    let recreateFramebuffer (size : V2i) (signature : IFramebufferSignature) =
        deleteFramebuffer()

        currentSize <- size
        currentSignature <- signature

        let depthSignature =
            match signature.DepthAttachment with
                | Some att -> att
                | _ -> { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
                
        let colorSignature =
            match Map.tryFind 0 signature.ColorAttachments with
                | Some (sem, att) when sem = DefaultSemantic.Colors -> att
                | _ -> { format = RenderbufferFormat.Rgba8; samples = 1 }

        let d = runtime.CreateRenderbuffer(currentSize, depthSignature.format, depthSignature.samples)
        let c = runtime.CreateRenderbuffer(currentSize, colorSignature.format, colorSignature.samples)
        let newTarget =
            runtime.CreateFramebuffer(
                signature,
                [
                    DefaultSemantic.Colors, c :> IFramebufferOutput
                    DefaultSemantic.Depth, d :> IFramebufferOutput
                ]
            )


        depth <- Some d
        color <- Some c
        target <- Some newTarget
        newTarget

    let rec getFramebuffer (size : V2i) (signature : IFramebufferSignature) =
        match target with
            | Some t ->
                if currentSize <> size || currentSignature <> signature then
                    recreateFramebuffer size signature
                else
                    t
            | None ->
                recreateFramebuffer size signature
                    
    let renderTime = Stopwatch()
    let compressTime = Stopwatch()
    let mutable frameCount = 0

    let mutable currentScene : Option<string * ConcreteScene * ModRef<C4f>> = None

    let mutable lastInfo : Option<ClientInfo> = None

    let getInfo() =
        match lastInfo with
            | Some lastInfo ->
                let now = { lastInfo with time = MicroTime.Now; token = AdaptiveToken.Top }
                match server.getState now with
                    | Some cam -> Some (now, cam)
                    | None -> None
            | _ ->
                None

    let rebuildTask (name : string) (signature : IFramebufferSignature) =
        match currentScene with
            | Some (oldName, scene,clear) -> Log.warn "rebuild(%s <> %s)"  oldName name
            | _ -> ()

        transact (fun () -> task.Dispose())
        let newScene = getScene signature name
        let clearColor = Mod.init C4f.Black
        let clear = runtime.CompileClear(signature, clearColor, Mod.constant 1.0)
        let render = newScene.CreateNewRenderTask()
        task <- RenderTask.ofList [clear; render]

        // needs to hold
        let lastInfo = lastInfo.Value
        newScene.Scene.AddClientInfo(lastInfo.session, lastInfo.targetId, getInfo)

        currentScene <- Some (name, newScene, clearColor)
        newScene, task, clearColor

    let getSceneAndTask (name : string) (signature : IFramebufferSignature) =
        match currentScene with
            | Some(sceneName, scene, clear) ->
                if sceneName <> name || scene.FramebufferSignature <> signature then
                    match lastInfo with
                        | Some info -> scene.Scene.RemoveClientInfo(info.session, info.targetId)
                        | _ -> ()
                    rebuildTask name signature
                else
                    scene, task, clear
            | None ->
                rebuildTask name signature

    member x.DownloadDepth(pixel : V2i) =
        match target with
            | Some fbo ->
                match Map.tryFind DefaultSemantic.Depth fbo.Attachments with
                    | Some (:? IBackendTextureOutputView as t) ->
                        if pixel.AllGreaterOrEqual 0 && pixel.AllSmaller t.Size.XY then
                            ReadPixel.downloadDepth pixel t.texture
                        else
                            None
                    | _ ->
                        None
            | None ->
                None

    member x.GetWorldPosition(pixel : V2i) =
        match target with
            | Some fbo ->
                match Map.tryFind DefaultSemantic.Depth fbo.Attachments with
                    | Some (:? IBackendTextureOutputView as t) ->
                        if pixel.AllGreaterOrEqual 0 && pixel.AllSmaller t.Size.XY then
                            match ReadPixel.downloadDepth pixel t.texture with
                                | Some depth ->
                                    let tc = (V2d pixel + V2d(0.5, 0.5)) / V2d t.Size.XY

                                    let ndc = V3d(2.0 * tc.X - 1.0, 1.0 - 2.0 * tc.Y, 2.0 * float depth - 1.0)
                                    match currentScene with
                                        | Some (_,cs,_) ->
                                            let view = cs.State.viewTrafo |> Mod.force
                                            let proj = cs.State.projTrafo |> Mod.force

                                            let vp = proj.Backward.TransformPosProj ndc 
                                            let wp = view.Backward.TransformPos vp
                                            Some wp
                                        | None ->
                                            None
                                | None ->
                                    None

                        else
                            None
                    | _ ->
                        None
            | None ->
                None
        
    abstract member ProcessImage : IFramebuffer * IRenderbuffer -> RenderResult
    abstract member Release : unit -> unit

    member x.Run(token : AdaptiveToken, info : ClientInfo, state : ClientState) =
        lastInfo <- Some info

        let scene, task, clearColor = getSceneAndTask info.sceneName info.signature

        let mutable t = Unchecked.defaultof<IDisposable>
        let mutable target = Unchecked.defaultof<IFramebuffer>

        lock scene (fun () ->
            if Interlocked.Increment(&threadCount) > 1 then
                Log.warn "[Media Server] threadCount > 1"
            t <- runtime.ContextLock
            target <- getFramebuffer info.size info.signature
            let innerToken = token.Isolated
            let token = ()

            transact (fun () -> clearColor.Value <- info.clearColor)
            
            try
                scene.EvaluateAlways innerToken (fun innerToken ->
                    scene.OutOfDate <- true
                    renderTime.Start()
                    transact (fun () -> scene.Apply(info, state))

                    task.Run(innerToken, RenderToken.Empty, OutputDescription.ofFramebuffer target)
                    renderTime.Stop()
                    innerToken.Release()
                )
                
            finally
                //printfn "race here"
                innerToken.Release()
                if scene.State.viewTrafo.ReaderCount > 0 then
                    printfn "[Media.Server] bad hate"
//                let real = scene.State.projTrafo |> Mod.force
//                let should = state.projTrafo
//                if real <> should then
//                    Log.warn "bad"
                Interlocked.Decrement(&threadCount) |> ignore
        )
        compressTime.Start()
        let data = x.ProcessImage(target, color.Value)
        //let data =
        //    match gpuCompressorInstance with
        //        | Some gpuCompressorInstance ->
        //            if info.samples > 1 then
        //                runtime.ResolveMultisamples(color.Value, resolved.Value, ImageTrafo.Rot0)
        //            else
        //                runtime.Copy(color.Value,resolved.Value.[TextureAspect.Color,0,0])
        //            gpuCompressorInstance.Compress(resolved.Value.[TextureAspect.Color,0,0])
        //        | None -> 
        //            target.DownloadJpegColor()

        compressTime.Stop()
        t.Dispose()
        frameCount <- frameCount + 1
        data

    member x.Dispose() =
        try 
            match lastInfo, currentScene with
                | Some i, Some(_,s,_) ->
                    s.Scene.RemoveClientInfo(i.session, i.targetId)
                    lastInfo <- None
                | _ -> 
                    ()
            deleteFramebuffer()
            task.Dispose()
            renderTime.Reset()
            compressTime.Reset()
            frameCount <- 0
            currentScene <- None
            lastInfo <- None
            x.Release()
        with e -> 
            Log.warn "[Media] render server disposal failed (alread disposed?)"

    member x.RenderTime = renderTime.MicroTime
    member x.CompressTime = compressTime.MicroTime
    member x.FrameCount = frameCount

    interface IDisposable with
        member x.Dispose() =
            x.Dispose()

type internal JpegClientRenderTask internal(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene, quality : Quantization * int) =
    inherit ClientRenderTask(server, getScene)
    
    let quantization,quality = quality
    let runtime = server.runtime
    let mutable gpuCompressorInstance : Option<JpegCompressorInstance> = None
    let mutable resolved : Option<IBackendTexture> = None

    let recreate  (fmt : RenderbufferFormat) (size : V2i) =
        match server.compressor with
            | Some compressor -> 
                match gpuCompressorInstance with
                    | None -> 
                        ()
                    | Some old -> 
                        old.Dispose()
                Log.line "[Media.Server] creating GPU image compressor for size: %A" size
                let instance = compressor.NewInstance(size, quantization)
                gpuCompressorInstance <- Some instance

                resolved |> Option.iter runtime.DeleteTexture
                let r = runtime.CreateTexture(size, TextureFormat.ofRenderbufferFormat fmt, 1, 1)
                resolved <- Some r
                Some r

            | _ ->
                None

    override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer) =
        let resolved = 
            match resolved with
                | Some r when r.Size.XY = color.Size -> Some r
                | _ -> recreate color.Format color.Size
        let data =
            match gpuCompressorInstance with
                | Some gpuCompressorInstance ->
                    let resolved = resolved.Value
                    if color.Samples > 1 then
                        runtime.ResolveMultisamples(color, resolved, ImageTrafo.Rot0)
                    else
                        runtime.Copy(color,resolved.[TextureAspect.Color,0,0])
                    gpuCompressorInstance.Compress(resolved.[TextureAspect.Color,0,0])
                | None -> 
                    target.DownloadJpegColor(quality)
        Jpeg data

    override x.Release() =
        gpuCompressorInstance |> Option.iter (fun i -> i.Dispose())
        resolved |> Option.iter runtime.DeleteTexture
        gpuCompressorInstance <- None
        resolved <- None

    new(server,getScene) = new JpegClientRenderTask(server,getScene,(Quantization.photoshop80,80))
        

type private MappingInfo =
    {
        name : string
        file : MemoryMappedFile
        view : MemoryMappedViewAccessor
        size : int64
        data : nativeint
    }
    
module internal RawDownload =
    open System.Runtime.InteropServices

    type IDownloader =
        inherit IDisposable
        abstract member Runtime : IRuntime
        abstract member Multisampled : bool
        abstract member Download : fbo : IFramebuffer * dst : nativeint -> unit
        

    module Vulkan =
        open Aardvark.Rendering.Vulkan

        type MSDownloader(runtime : Runtime) =
            let device = runtime.Device

            let mutable tempImage : Option<Image> = None
            let mutable tempBuffer : Option<Buffer> = None

            let getTempImage(size : V2i) =
                //let size = V2i(Fun.NextPowerOfTwo size.X, Fun.NextPowerOfTwo size.Y)
                match tempImage with
                    | Some t when t.Size.XY = size -> t
                    | _ ->
                        tempImage |> Option.iter device.Delete
                        let t = Image.create (V3i(size,1)) 1 1 1 TextureDimension.Texture2D TextureFormat.Rgba8 (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit) device
                        tempImage <- Some t
                        t

            let getTempBuffer (size : int64) =
                //let size = Fun.NextPowerOfTwo size
                match tempBuffer with
                    | Some b when b.Size = size -> b
                    | _ ->
                        tempBuffer |> Option.iter device.Delete
                        let b = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size
                        tempBuffer <- Some b
                        b

            member x.Runtime = runtime :> IRuntime

            member x.Multisampled = true

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let image = fbo.Attachments.[DefaultSemantic.Colors].Image
                let lineSize = 4L * int64 image.Size.X
                let sizeInBytes = lineSize * int64 image.Size.Y

                let tempImage = getTempImage image.Size.XY
                let tempBuffer = getTempBuffer sizeInBytes
                
                let l = image.Layout
                device.perform {
                    do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(tempImage, VkImageLayout.TransferDstOptimal)
                    do! Command.ResolveMultisamples(image.[ImageAspect.Color, 0, 0], V3i.Zero, tempImage.[ImageAspect.Color, 0, 0], V3i.Zero, image.Size)
                    do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(tempImage.[ImageAspect.Color, 0, 0], V3i.Zero, tempBuffer, 0L, V2i.Zero, image.Size)
                    do! Command.TransformLayout(image, l)
                }

                tempBuffer.Memory.Mapped (fun ptr ->
                    Marshal.Copy(ptr, dst, nativeint sizeInBytes)
                )

            member x.Dispose() =
                tempImage |> Option.iter device.Delete
                tempBuffer |> Option.iter device.Delete


            interface IDownloader with
                member x.Dispose() = x.Dispose()
                member x.Runtime = x.Runtime
                member x.Multisampled = x.Multisampled
                member x.Download(fbo, dst) = x.Download(fbo, dst)

        type SSDownloader(runtime : Runtime) =
            let device = runtime.Device
            
            let mutable tempBuffer : Option<Buffer> = None
            
            let getTempBuffer (size : int64) =
                //let size = Fun.NextPowerOfTwo size
                match tempBuffer with
                    | Some b when b.Size = size -> b
                    | _ ->
                        tempBuffer |> Option.iter device.Delete
                        let b = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size
                        tempBuffer <- Some b
                        b

            member x.Runtime = runtime :> IRuntime

            member x.Multisampled = false

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let image = fbo.Attachments.[DefaultSemantic.Colors].Image
                let lineSize = 4L * int64 image.Size.X
                let sizeInBytes = lineSize * int64 image.Size.Y
                
                let tempBuffer = getTempBuffer sizeInBytes
                
                let l = image.Layout
                device.perform {
                    do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(image.[ImageAspect.Color, 0, 0], V3i.Zero, tempBuffer, 0L, V2i.Zero, image.Size)
                    do! Command.TransformLayout(image, l)
                }

                tempBuffer.Memory.Mapped (fun ptr ->
                    Marshal.Copy(ptr, dst, nativeint sizeInBytes)
                )

            member x.Dispose() =
                tempBuffer |> Option.iter device.Delete


            interface IDownloader with
                member x.Dispose() = x.Dispose()
                member x.Runtime = x.Runtime
                member x.Multisampled = x.Multisampled
                member x.Download(fbo, dst) = x.Download(fbo, dst)

        let createDownloader (runtime : IRuntime) (samples : int) =
            if samples = 1 then new SSDownloader(unbox runtime) :> IDownloader
            else new MSDownloader(unbox runtime) :> IDownloader

        let download (runtime : IRuntime) (fbo : IFramebuffer) (samples : int) (dst : nativeint) =
            let runtime = unbox<Runtime> runtime
            let fbo = unbox<Framebuffer> fbo
            let image = fbo.Attachments.[DefaultSemantic.Colors].Image
            let device = runtime.Device
            
            if samples > 1 then
                let lineSize = 4L * int64 image.Size.X
                let size = lineSize * int64 image.Size.Y
                let tempImage = Image.create image.Size 1 1 1 TextureDimension.Texture2D TextureFormat.Rgba8 (VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit) device
                use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

                let l = image.Layout
                device.perform {
                    do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(tempImage, VkImageLayout.TransferDstOptimal)
                    do! Command.ResolveMultisamples(image.[ImageAspect.Color, 0, 0], tempImage.[ImageAspect.Color, 0, 0])
                    do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(tempImage.[ImageAspect.Color, 0, 0], V3i.Zero, temp, 0L, V2i.Zero, tempImage.Size)
                    do! Command.TransformLayout(image, l)
                }

                temp.Memory.Mapped (fun ptr ->
                    Marshal.Copy(ptr, dst, nativeint size)
                )

                Image.delete tempImage device
            else
                let lineSize = 4L * int64 image.Size.X
                let size = lineSize * int64 image.Size.Y
                use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

                let l = image.Layout
                device.perform {
                    do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(image.[ImageAspect.Color, 0, 0], V3i.Zero, temp, 0L, V2i.Zero, image.Size)
                    do! Command.TransformLayout(image, l)
                }

                temp.Memory.Mapped (fun ptr ->
                    Marshal.Copy(ptr, dst, size)
                )

    module GL =
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL

        type SSDownloader(runtime : Runtime) =
            
            //let mutable pbo : Option<int * int64> = None

            //let getPBO (size : int64) =
            //    //let size = Fun.NextPowerOfTwo size
            //    match pbo with
            //        | Some (h,s) when s = size -> h
            //        | _ ->
            //            pbo |> Option.iter (fun (h,_) -> GL.DeleteBuffer(h))
            //            let b = GL.GenBuffer()
            //            GL.BindBuffer(BufferTarget.PixelPackBuffer, b)
            //            GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint size, 0n, BufferStorageFlags.MapReadBit)
            //            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            //            pbo <- Some (b, size)
            //            b

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let ctx = runtime.Context
            
                use __ = ctx.ResourceLock
                let size = fbo.Size
                let rowSize = 4L * int64 size.X
                let sizeInBytes = rowSize * int64 size.Y

                let pbo = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)
                GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)

                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, 0n)

                let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint sizeInBytes, BufferAccessMask.MapReadBit)
                
                Marshal.Copy(ptr, dst, sizeInBytes)

                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.DeleteBuffer(pbo)

            member x.Dispose() =
                ()

            member x.Multisampled = false

            member x.Runtime = runtime :> IRuntime
            
            interface IDownloader with
                member x.Dispose() = x.Dispose()
                member x.Runtime = x.Runtime
                member x.Multisampled = x.Multisampled
                member x.Download(fbo, dst) = x.Download(fbo, dst)

        type MSDownloader(runtime : Runtime) =
            
            //let mutable pbo : Option<int * int64> = None
            let mutable resolved : Option<int * int * V2i> = None

            //let getPBO (size : int64) =
                //let size = Fun.NextPowerOfTwo size
                //match pbo with
                //    | Some (h,s) when s = size -> h
                //    | _ ->
                //        pbo |> Option.iter (fun (h,_) -> GL.DeleteBuffer(h))
                //        let b = GL.GenBuffer()
                //        GL.BindBuffer(BufferTarget.PixelPackBuffer, b)
                //        GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint size, 0n, BufferStorageFlags.MapReadBit)
                //        GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                //        pbo <- Some (b, size)
                //        b

            let getFramebuffer (size : V2i) =
                //let size = V2i(Fun.NextPowerOfTwo size.X, Fun.NextPowerOfTwo size.Y)
                match resolved with
                    | Some (f,_,s) when s = size -> f
                    | _ ->
                        match resolved with
                            | Some (f,r,_) ->
                                GL.DeleteFramebuffer(f)
                                GL.DeleteRenderbuffer(r)
                            | None ->
                                ()

                        let f = GL.GenFramebuffer()
                        let r = GL.GenRenderbuffer()

                        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, r)
                        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, size.X, size.Y)
                        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
                
                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, f)
                        GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, r)
                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                        
                        resolved <- Some(f,r,size)
                        f

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let ctx = runtime.Context
            
                use __ = ctx.ResourceLock
                let size = fbo.Size
                let rowSize = 4L * int64 size.X
                let sizeInBytes = rowSize * int64 size.Y

                let pbo = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)
                GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)

                let temp = getFramebuffer size

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, temp)

                GL.BlitFramebuffer(
                    0, 0, size.X - 1, size.Y - 1, 
                    0, 0, size.X - 1, size.Y - 1,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Nearest
                )
                
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, temp)

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)

                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, 0n)

                let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint sizeInBytes, BufferAccessMask.MapReadBit)
                
                Marshal.Copy(ptr, dst, sizeInBytes)

                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.DeleteBuffer(pbo)

            member x.Dispose() =
                use __ = runtime.Context.ResourceLock
                //pbo |> Option.iter (fst >> GL.DeleteBuffer)
                //pbo <- None
                
                match resolved with
                    | Some (f,r,_) ->
                        GL.DeleteFramebuffer(f)
                        GL.DeleteRenderbuffer(r)
                        resolved <- None
                    | None ->
                        ()


            member x.Multisampled = false

            member x.Runtime = runtime :> IRuntime
            
            interface IDownloader with
                member x.Dispose() = x.Dispose()
                member x.Runtime = x.Runtime
                member x.Multisampled = x.Multisampled
                member x.Download(fbo, dst) = x.Download(fbo, dst)

        let createDownloader (runtime : IRuntime) (samples : int) : IDownloader =
            if samples = 1 then new SSDownloader(unbox runtime) :> IDownloader
            else new MSDownloader(unbox runtime) :> IDownloader

        let download (runtime : IRuntime) (fbo : IFramebuffer) (samples : int) (dst : nativeint) =
            let runtime = unbox<Runtime> runtime
            let fbo = unbox<Framebuffer> fbo
            let ctx = runtime.Context
            
            use __ = ctx.ResourceLock
            let size = fbo.Size

            let mutable tmpFbo = -1
            let mutable tmpRbo = -1
            if samples > 1 then
                let tmp = GL.GenFramebuffer()
                let rbo = GL.GenRenderbuffer()

                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo)
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, size.X, size.Y)
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
                
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, tmp)
                GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, rbo)
                
                GL.BlitFramebuffer(
                    0, 0, size.X - 1, size.Y - 1, 
                    0, 0, size.X - 1, size.Y - 1,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Nearest
                )
                
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, tmp)
                
                tmpFbo <- tmp
                tmpRbo <- rbo

            else
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
              


            let pbo = GL.GenBuffer()

            let rowSize = 4 * size.X
            let sizeInBytes = rowSize * size.Y
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)
            GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)

            GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, 0n)

            let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint sizeInBytes, BufferAccessMask.MapReadBit)
                
            Marshal.Copy(ptr, dst, sizeInBytes)
            //let lineSize = nativeint rowSize
            //let mutable src = ptr
            //let mutable dst = dst + nativeint sizeInBytes - lineSize

            //for _ in 0 .. size.Y-1 do
            //    Marshal.Copy(src, dst, lineSize)
            //    src <- src + lineSize
            //    dst <- dst - lineSize

            GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            GL.DeleteBuffer(pbo)



            if tmpFbo >= 0 then GL.DeleteFramebuffer(tmpFbo)
            if tmpRbo >= 0 then GL.DeleteRenderbuffer(tmpRbo)
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            
        

    let download (runtime : IRuntime) (fbo : IFramebuffer) (samples : int) (dst : nativeint) =  
        match runtime with
            | :? Aardvark.Rendering.Vulkan.Runtime ->
                Vulkan.download runtime fbo samples dst

            | :? Aardvark.Rendering.GL.Runtime ->
                GL.download runtime fbo samples dst
                
            | _ ->
                failwith "bad runtime"


type internal MappedClientRenderTask internal(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene) =
    inherit ClientRenderTask(server, getScene)
    let runtime = server.runtime

    let mutable mapping : Option<MappingInfo> = None
    let mutable downloader : Option<RawDownload.IDownloader> = None
    
    let recreateMapping (desiredSize : int64) =
        match mapping with
            | Some m ->
                m.view.Dispose()
                m.file.Dispose()
            | None ->
                ()

        let name = Guid.NewGuid() |> string
        let file = MemoryMappedFile.CreateNew(name, desiredSize)
        let view = file.CreateViewAccessor()

        let m =
            {
                name = name
                file = file
                view = view
                size = desiredSize
                data = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
            }
        mapping <- Some m
        m

    let recreateDownloader (runtime : IRuntime) (samples : int)  =
        downloader |> Option.iter (fun d -> d.Dispose())
        let d = 
            match runtime with
                | :? Aardvark.Rendering.Vulkan.Runtime -> RawDownload.Vulkan.createDownloader runtime samples
                | :? Aardvark.Rendering.GL.Runtime -> RawDownload.GL.createDownloader runtime samples
                | _ -> failwith "unknown runtime"
        downloader <- Some d
        d

    override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer) =
        let desiredMapSize = Fun.NextPowerOfTwo (int64 color.Size.X * int64 color.Size.Y * 4L)

        let mapping =
            match mapping with
                | Some m when m.size = desiredMapSize -> m
                | _ -> recreateMapping desiredMapSize

        //RawDownload.download runtime target color.Samples mapping.data

        let downloader =
            let isMS = color.Samples > 1
            match downloader with
                | Some d when d.Multisampled = isMS -> d
                | _ -> recreateDownloader runtime color.Samples

        downloader.Download(target, mapping.data)

        Mapping { name = mapping.name; size = color.Size; length = int mapping.size }

    override x.Release() =
        downloader |> Option.iter (fun d -> d.Dispose())
        downloader <- None
        match mapping with
            | Some m ->
                m.view.Dispose()
                m.file.Dispose()
                mapping <- None
            | None ->
                ()

type internal ClientCreateInfo =
    {
        server          : Server
        session         : Guid
        id              : string
        sceneName       : string
        samples         : int
        socket          : WebSocket
        useMapping      : bool
        getSignature    : int -> IFramebufferSignature
    }

type internal Client(updateLock : obj, createInfo : ClientCreateInfo, getState : ClientInfo -> ClientState, getContent : IFramebufferSignature -> string -> ConcreteScene) as this =
    static let mutable currentId = 0
 
    static let newTask (info : ClientCreateInfo) getContent =
        if info.useMapping then
            new MappedClientRenderTask(info.server, getContent) :> ClientRenderTask
        else
            new JpegClientRenderTask(info.server, getContent) :> ClientRenderTask

    let id = Interlocked.Increment(&currentId)
    let sender = AdaptiveObject()
    let requestedSize : MVar<C4b * V2i> = MVar.empty()
    let mutable createInfo = createInfo
    let mutable task = newTask createInfo getContent
    let mutable running = false
    let mutable disposed = 0

    let mutable frameCount = 0
    let roundTripTime = Stopwatch()
    let invalidateTime = Stopwatch()

    let send (cmd : Command) =
        let data = Pickler.json.Pickle cmd

        let res = createInfo.socket.send Opcode.Text (ByteSegment data) true |> Async.RunSynchronously
        match res with
            | Choice1Of2 () -> ()
            | Choice2Of2 err ->
                Log.warn "[Client] %d: send of %A faulted (stopping): %A" id cmd err
                this.Dispose()
                //failwithf "[Client] %d: %A" id err

    let subscribe() =
        sender.AddMarkingCallback(fun () ->
            invalidateTime.Start()
            send Invalidate
        )

    let mutable info =
        {
            token = Unchecked.defaultof<AdaptiveToken>
            signature = createInfo.getSignature createInfo.samples
            targetId = createInfo.id
            sceneName = createInfo.sceneName
            session = createInfo.session
            samples = createInfo.samples
            size = V2i.II
            time = MicroTime.Now
            clearColor = C4f.Black
        }

    let mutable subscription = subscribe()


    let renderLoop() =
        while running do
            let (background, size) = MVar.take requestedSize
            if size.AllGreater 0 then
                lock updateLock (fun () ->
                    sender.EvaluateAlways AdaptiveToken.Top (fun token ->
                        let info = Interlocked.Change(&info, fun info -> { info with token = token; size = size; time = MicroTime.Now; clearColor = background.ToC4f() })
                        try
                            let state = getState info
                            let data = task.Run(token, info, state)
                            match data with
                                | Jpeg data -> 
                                    let res = createInfo.socket.send Opcode.Binary (ByteSegment data) true |> Async.RunSynchronously
                                    match res with
                                        | Choice1Of2() -> ()
                                        | Choice2Of2 err ->
                                            running <- false
                                            Log.warn "[Client] %d: could not send render-result due to %A (stopping)" id err
                                
                                | Mapping img ->
                                    let data = Pickler.json.Pickle img
                                    let res = createInfo.socket.send Opcode.Text (ByteSegment data) true |> Async.RunSynchronously
                                    match res with
                                        | Choice1Of2() -> ()
                                        | Choice2Of2 err ->
                                            running <- false
                                            Log.warn "[Client] %d: could not send render-result due to %A (stopping)" id err
                                    
                        with e ->
                            running <- false
                            Log.error "[Client] %d: rendering faulted with %A (stopping)" id e
                    
                    )
                )

                


    let mutable renderThread = new Thread(ThreadStart(renderLoop), IsBackground = true, Name = "ClientRenderer_" + string createInfo.session)


    member x.Info = info
    member x.FrameCount = frameCount
    member x.FrameTime = roundTripTime.MicroTime
    member x.InvalidateTime = invalidateTime.MicroTime
    member x.RenderTime = task.RenderTime
    member x.CompressTime = task.CompressTime


    member x.Revive(newInfo : ClientCreateInfo) =
        if Interlocked.Exchange(&disposed, 0) = 1 then
            Log.line "[Client] %d: revived" id
            createInfo <- newInfo
            task <- newTask newInfo getContent
            subscription <- subscribe()
            renderThread <- new Thread(ThreadStart(renderLoop), IsBackground = true, Name = "ClientRenderer_" + string info.session)

    member x.Dispose() =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            task.Dispose()
            subscription.Dispose()
            running <- false
            MVar.put requestedSize (C4b.Black, V2i.Zero)
            frameCount <- 0
            roundTripTime.Reset()
            invalidateTime.Reset()

    member x.Run =
        running <- true
        renderThread.Start()
        socket {
            
            Log.line "[Client] %d: running %s" id info.sceneName
            try
                while running do
                    let! (code, data) = createInfo.socket.readMessage()

                    match code with
                        | Opcode.Text ->
                            try
                                
                                if data.Length > 0 && data.[0] = uint8 '#' then
                                    let str = System.Text.Encoding.UTF8.GetString(data, 1, data.Length - 1)
                                    match str with
                                        | "ping" -> 
                                            do! createInfo.socket.send Opcode.Pong (ByteSegment([||])) true
                                        | _ ->
                                            Log.warn "bad opcode: %A" str
                                else
                                    let msg : Message = Pickler.json.UnPickle data

                                    match msg with
                                        | RequestImage(background, size) ->
                                            invalidateTime.Stop()
                                            roundTripTime.Start()
                                            MVar.put requestedSize (background, size)

                                        | RequestWorldPosition pixel ->
                                            let wp = 
                                                match task.GetWorldPosition pixel with
                                                    | Some d -> d
                                                    | None -> V3d.Zero

                                            send (WorldPosition wp)

                                        | Rendered ->
                                            roundTripTime.Stop()
                                            frameCount <- frameCount + 1

                                        | Shutdown ->
                                            running <- false

                                        | Change(scene, samples) ->
                                            let signature = createInfo.getSignature samples
                                            Interlocked.Change(&info, fun info -> { info with samples = samples; signature = signature; sceneName = scene }) |> ignore

                            with e ->
                                Log.warn "[Client] %d: unexpected message %A" id (Encoding.UTF8.GetString data)

                        | Opcode.Binary ->
                            Log.warn "[Client] %d: unexpected binary message" id
                        | Opcode.Close ->
                            running <- false
                        | Opcode.Ping -> 
                            do! createInfo.socket.send Opcode.Pong (ByteSegment([||])) true
                        | _ ->
                            ()

            finally
                Log.line "[Client] %d: stopped" id
                x.Dispose()
        }

    interface IDisposable with
        member x.Dispose() = x.Dispose()





[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Server =
    open System.IO
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Runtime.CompilerServices

    [<AutoOpen>]
    module private Utils = 
        type ClientStatistics =
            {
                session         : Guid
                name            : string
                frameCount      : int
                invalidateTime  : float
                renderTime      : float
                compressTime    : float
                frameTime       : float
            }

        type Client with
            member internal x.GetStatistics() =
                {
                    session = x.Info.session
                    name = x.Info.sceneName
                    frameCount = x.FrameCount
                    invalidateTime = x.InvalidateTime.TotalSeconds
                    renderTime = x.RenderTime.TotalSeconds
                    compressTime = x.CompressTime.TotalSeconds
                    frameTime = x.FrameTime.TotalSeconds
                }

        let (|Int|_|) (str : string) =
            match Int32.TryParse str with
                | (true, v) -> Some v
                | _ -> None

        let noState =
            {
                viewTrafo = Trafo3d.Identity
                projTrafo = Trafo3d.Identity
            }

    let empty (useGpuCompression : bool) (r : IRuntime) =
        let compressor =
            if useGpuCompression then new JpegCompressor(r) |> Some
            else None
        {
            runtime = r
            content = fun _ -> None
            getState = fun _ -> None
            compressor = compressor
            fileSystemRoot = None
        }

    let withState (get : ClientInfo -> ClientState) (server : Server) =
        { server with getState = get >> Some }

    let withContent (get : string -> Option<Scene>) (server : Server) =
        { server with content = get }

    let addScenes (find : string -> Option<Scene>) (server : Server) =
        { 
            server with
                content = fun n ->
                    match find n with
                        | Some s -> Some s
                        | None -> server.content n
        }

    let addScene (name : string) (scene : Scene) (server : Server) =
        { 
            server with
                content = fun n ->
                    if n = name then Some scene
                    else server.content n
        }

    let add (name : string) (create : ClientValues -> IRenderTask) (server : Server) =
        addScene name (Scene.custom create) server

    let toWebPart (updateLock : obj) (info : Server) =
        let clients = Dict<Guid * string, Client>()

        let signatures = ConcurrentDictionary<int, IFramebufferSignature>()

        let getSignature (samples : int) =
            signatures.GetOrAdd(samples, fun samples ->
                info.runtime.CreateFramebufferSignature(
                    samples,
                    [
                        DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                        DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                    ]
                )
            )

        let getState (ci : ClientInfo) =
            match info.getState ci with
                | Some c -> c
                | None -> noState



        let content signature id  =
            match info.content id with   
                | Some scene -> scene.GetConcreteScene(id, signature)
                | None -> Scene.empty.GetConcreteScene(id, signature)

        let render (targetId : string) (ws : WebSocket) (context: HttpContext) =
            let request = context.request
            let args = request.query |> List.choose (function (n,Some v) -> Some(n,v) | _ -> None) |> Map.ofList
            
            let sceneName =
                match Map.tryFind "scene" args with
                    | Some scene -> scene
                    | _ -> targetId

            let samples =
                match Map.tryFind "samples" args with
                    | Some (Int samples) -> samples
                    | _ -> 1

            let sessionId =
                match Map.tryFind "session" args with
                    | Some id -> Guid.Parse id
                    | _ -> Guid.NewGuid()

            let useMapping =
                match Map.tryFind "mapped" args with
                    | Some "false" -> false
                    | Some "true" -> true
                    | _ -> false

            let createInfo =
                {
                    server          = info
                    session         = sessionId
                    id              = targetId
                    sceneName       = sceneName
                    samples         = samples
                    socket          = ws
                    useMapping      = useMapping
                    getSignature    = getSignature
                }            

            let client = 
                let key = (sessionId, targetId)
                lock clients (fun () ->
                    clients.GetOrCreate(key, fun (sessionId, targetId) ->
                        Log.line "[Server] created client for (%A/%s), mapping %s" sessionId targetId (if useMapping then "enabled" else "disabled")
                        new Client(updateLock, createInfo, getState, content)
                    )
                )

            client.Revive(createInfo)
            client.Run

        let statistics (ctx : HttpContext) =
            let request = ctx.request
            let args = request.query |> List.choose (function (n,Some v) -> Some(n,v) | _ -> None) |> Map.ofList

            let clients = 
                match Map.tryFind "session" args, Map.tryFind "name" args with
                    | Some sid, Some name -> 
                        match Guid.TryParse sid with
                            | (true, sid) -> 
                                match clients.TryGetValue((sid, name)) with
                                    | (true, c) -> [| c |]
                                    | _ -> [||]
                            | _ ->
                                [||]
                    | _ -> 
                        clients.Values |> Seq.toArray

            let stats = clients  |> Array.filter (fun v -> not (isNull (v :> obj))) |> Array.map (fun c -> c.GetStatistics()) |> Array.filter (fun s -> s.frameCount > 0)
            let json = Pickler.json.PickleToString stats
            ctx |> (OK json >=> Writers.setMimeType "text/json")

        let screenshot (sceneName : string) (context: HttpContext) =
            let request = context.request
            let args = request.query |> List.choose (function (n,Some v) -> Some(n,v) | _ -> None) |> Map.ofList

            let samples = 
                match Map.tryFind "samples" args with
                    | Some (Int s) -> s
                    | _ -> 1

            let signature = getSignature samples

            match Map.tryFind "w" args, Map.tryFind "h" args with
                | Some (Int w), Some (Int h) when w > 0 && h > 0 ->
                    let scene = content signature sceneName
                    use task = new JpegClientRenderTask(info, content, (Quantization.photoshop100,100))

                    let clientInfo = 
                        {
                            token = AdaptiveToken.Top
                            signature = signature
                            targetId = ""
                            sceneName = sceneName
                            session = Guid.Empty
                            size = V2i(w,h)
                            samples = samples
                            time = MicroTime.Now
                            clearColor = C4f.Black
                        }

                    let state = getState clientInfo
                    let data = 
                        match task.Run(AdaptiveToken.Top, clientInfo, state) with
                            | Jpeg d -> d
                            | _ -> failwith "that was unexpected"

                    context |> (ok data >=> Writers.setMimeType "image/jpeg")
                | _ ->
                    context |> BAD_REQUEST "no width/height specified"

        choose [
            yield Reflection.assemblyWebPart typeof<Client>.Assembly
            yield pathScan "/render/%s" (render >> handShake)
            yield path "/stats.json" >=> statistics 
            yield pathScan "/screenshot/%s" screenshot

            match info.fileSystemRoot with
                | Some root ->
                    let fileSystem = 
                        if root = "/" then new FileSystem()
                        else new FileSystem(root)
                    yield path "/fs" >=> FileSystem.toWebPart fileSystem
                | None ->
                    ()
        ]

    let run (port : int) (server : Server) =
        server |> toWebPart (obj())  |> List.singleton |> WebPart.runServer port

    let start (port : int) (server : Server) =
        server |> toWebPart (obj())  |> List.singleton |> WebPart.startServer port
    
    // c# friendly to start app directly
    let StartWebPart (port:int) (webPart:WebPart) =
        webPart |> List.singleton |> WebPart.startServer port