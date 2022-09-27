#nowarn "1337"
namespace Aardvark.Service


open System
open System.Threading

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open Aardvark.Application
open System.Diagnostics


open Aardvark.Service


module Internals =

    [<AbstractClass>]
    type ClientRenderTask(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene) =
        let runtime = server.runtime
        let mutable task = RenderTask.empty
    
        let targetSize = AVal.init V2i.II
    
        let mutable currentSize = -V2i.II
        let mutable currentSignature = Unchecked.defaultof<IFramebufferSignature>
    
        let mutable depth : Option<IRenderbuffer> = None 
        let mutable color : Option<IRenderbuffer> = None
        let mutable target : Option<IFramebuffer> = None 

        static let mutable threadCount = 0


        let deleteFramebuffer() =
            target     |> Option.iter Disposable.dispose
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

            let depthStencilFormat =
                signature.DepthStencilAttachment
                |> Option.defaultValue TextureFormat.Depth24Stencil8
                
            let colorFormat =
                match Map.tryFind 0 signature.ColorAttachments with
                | Some att when att.Name = DefaultSemantic.Colors -> att.Format
                | _ -> TextureFormat.Rgba8

            let d = runtime.CreateRenderbuffer(currentSize, depthStencilFormat, signature.Samples)
            let c = runtime.CreateRenderbuffer(currentSize, colorFormat, signature.Samples)
            let newTarget =
                runtime.CreateFramebuffer(
                    signature,
                    [
                        DefaultSemantic.Colors, c :> IFramebufferOutput
                        DefaultSemantic.DepthStencil, d :> IFramebufferOutput
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

        let mutable currentScene : Option<string * ConcreteScene * cval<C4f>> = None

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
            let clearColor = AVal.init C4f.Black
            let clear = runtime.CompileClear(signature, clearColor, AVal.constant 1.0, AVal.constant 0)
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
                    match Map.tryFind DefaultSemantic.DepthStencil fbo.Attachments with
                        | Some (:? ITextureLevel as t) ->
                            if pixel.AllGreaterOrEqual 0 && pixel.AllSmaller t.Size.XY then
                                ReadPixel.downloadDepth pixel t.Texture
                            else
                                None
                        | _ ->
                            None
                | None ->
                    None

        member x.GetWorldPosition(pixel : V2i) =
            match target with
                | Some fbo ->
                    match Map.tryFind DefaultSemantic.DepthStencil fbo.Attachments with
                        | Some (:? ITextureLevel as t) ->
                            if pixel.AllGreaterOrEqual 0 && pixel.AllSmaller t.Size.XY then
                                match ReadPixel.downloadDepth pixel t.Texture with
                                    | Some depth ->
                                        let tc = (V2d pixel + V2d(0.5, 0.5)) / V2d t.Size.XY

                                        let ndc = V3d(2.0 * tc.X - 1.0, 1.0 - 2.0 * tc.Y, 2.0 * float depth - 1.0)
                                        match currentScene with
                                            | Some (_,cs,_) ->
                                                let view = cs.State.viewTrafo |> AVal.force
                                                let proj = cs.State.projTrafo |> AVal.force

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
        
        abstract member ProcessImage : IFramebuffer * IRenderbuffer * RenderQuality -> RenderResult
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
                transact (fun () -> clearColor.Value <- info.clearColor)
            
                try
                    scene.EvaluateAlways token (fun token ->
                        scene.OutOfDate <- true
                        renderTime.Start()
                        transact (fun () -> scene.Apply(info, state))

                        task.Run(token, RenderToken.Empty, OutputDescription.ofFramebuffer target)
                        renderTime.Stop()
                    )
                
                finally
                    Interlocked.Decrement(&threadCount) |> ignore
            )
            compressTime.Start()
            let data = x.ProcessImage(target, color.Value, info.quality)
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

    type JpegClientRenderTask(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene, quality : RenderQuality) =
        inherit ClientRenderTask(server, getScene)
    
        let mutable quality = quality
        let mutable quantization = Quantization.ofQuality quality.quality
        let mutable quantizationQuality = quality.quality
        let runtime = server.runtime
        let mutable gpuCompressorInstance : Option<JpegCompressorInstance> = None
        let mutable resolved : Option<IBackendTexture> = None

        let recreate  (fmt : TextureFormat) (size : V2i) =
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
                    let r = runtime.CreateTexture2D(size, fmt, 1, 1)
                    resolved <- Some r
                    Some r

                | _ ->
                    None

        member x.Quality
            with get() = quality
            and set q =
                quality <- q


        override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer, renderQuality : RenderQuality) =
            quality <- renderQuality
            let resolved = 
                let resSize = V2i(max 1 (int (round (quality.scale * float color.Size.X))), max 1 (int (round (quality.scale * float color.Size.Y))))
                match resolved with
                    | Some r when r.Size.XY = resSize -> Some r
                    | _ -> recreate color.Format resSize
            let data =
                match gpuCompressorInstance with
                    | Some gpuCompressorInstance ->
                        let resolved = resolved.Value
                        if color.Samples > 1 || quality.scale <> 1.0 then
                            runtime.ResolveMultisamples(color, resolved, ImageTrafo.Identity)
                        else
                            runtime.Copy(color,resolved.[TextureAspect.Color,0,0])

                        let qual = quality.quality
                        if quantizationQuality <> qual then
                            let q = Quantization.ofQuality qual
                            quantization <- q
                            gpuCompressorInstance.Quality <- q
                            quantizationQuality <- qual


                        gpuCompressorInstance.Compress(resolved.[TextureAspect.Color,0,0])
                    | None -> 
                        target.DownloadJpegColor(quality.scale, int (round quality.quality))
            Jpeg data

        override x.Release() =
            gpuCompressorInstance |> Option.iter (fun i -> i.Dispose())
            resolved |> Option.iter runtime.DeleteTexture
            gpuCompressorInstance <- None
            resolved <- None

        new(server,getScene) = new JpegClientRenderTask(server,getScene, RenderQuality.full)

    type PngClientRenderTask(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene) =
        inherit ClientRenderTask(server, getScene)     

        let runtime = server.runtime
        let mutable resolved : Option<IBackendTexture> = None

        let recreate  (fmt : TextureFormat) (size : V2i) =
            resolved |> Option.iter runtime.DeleteTexture
            let r = runtime.CreateTexture2D(size, fmt, 1, 1)
            resolved <- Some r
            r


        override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer, _) =
            let resolved = 
                match resolved with
                    | Some r when r.Size.XY = color.Size -> r
                    | _ -> recreate color.Format color.Size
            let data =
                if color.Samples > 1 then
                    runtime.ResolveMultisamples(color, resolved, ImageTrafo.Identity)
                else
                    runtime.Copy(color,resolved.[TextureAspect.Color,0,0])

            let pi = runtime.Download(resolved).ToPixImage<byte>().ToFormat(Col.Format.RGBA)
            use stream = new System.IO.MemoryStream()
            pi.Save(stream, PixFileFormat.Png)
            RenderResult.Png (stream.ToArray())

        override x.Release() =
            resolved |> Option.iter runtime.DeleteTexture
            resolved <- None


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
                            tempImage |> Option.iter (fun i -> i.Dispose())
                            let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.ColorAttachmentBit
                            let t = device.CreateImage(size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                            tempImage <- Some t
                            t

                let getTempBuffer (size : int64) =
                    //let size = Fun.NextPowerOfTwo size
                    match tempBuffer with
                        | Some b when b.Size = size -> b
                        | _ ->
                            tempBuffer |> Option.iter (fun i -> i.Dispose())
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
                        do! Command.SyncPeersDefault(image,VkImageLayout.TransferSrcOptimal) // inefficient but needed. why? tempImage has peers
                        do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                        do! Command.TransformLayout(tempImage, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(image.[TextureAspect.Color, 0, 0], V3i.Zero, tempImage.[TextureAspect.Color, 0, 0], V3i.Zero, image.Size)
                        if device.IsDeviceGroup then 
                            do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                            do! Command.SyncPeersDefault(tempImage,VkImageLayout.TransferSrcOptimal)
                        else
                            do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                        do! Command.Copy(tempImage.[TextureAspect.Color, 0, 0], V3i.Zero, tempBuffer, 0L, V2i.Zero, image.Size)
                        do! Command.TransformLayout(image, l)
                    }

                    tempBuffer.Memory.Mapped (fun ptr ->
                        Marshal.Copy(ptr, dst, nativeint sizeInBytes)
                    )

                member x.Dispose() =
                    tempImage |> Option.iter (fun i -> i.Dispose())
                    tempBuffer |> Option.iter (fun i -> i.Dispose())


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
                            tempBuffer |> Option.iter (fun i -> i.Dispose())
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
                        if device.IsDeviceGroup then 
                            do! Command.SyncPeersDefault(image,VkImageLayout.TransferSrcOptimal)
                        else
                            do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                        do! Command.Copy(image.[TextureAspect.Color, 0, 0], V3i.Zero, tempBuffer, 0L, V2i.Zero, image.Size)
                        do! Command.TransformLayout(image, l)
                    }

                    tempBuffer.Memory.Mapped (fun ptr ->
                        Marshal.Copy(ptr, dst, nativeint sizeInBytes)
                    )

                member x.Dispose() =
                    tempBuffer |> Option.iter (fun i -> i.Dispose())


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
                    let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    let tempImage = device.CreateImage(image.Size, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                    use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

                    let l = image.Layout
                    device.perform {
                        do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                        do! Command.TransformLayout(tempImage, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(image.[TextureAspect.Color, 0, 0], tempImage.[TextureAspect.Color, 0, 0])
                        do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                        do! Command.Copy(tempImage.[TextureAspect.Color, 0, 0], V3i.Zero, temp, 0L, V2i.Zero, tempImage.Size)
                        do! Command.TransformLayout(image, l)
                    }

                    temp.Memory.Mapped (fun ptr ->
                        Marshal.Copy(ptr, dst, nativeint size)
                    )

                    tempImage.Dispose()
                else
                    let lineSize = 4L * int64 image.Size.X
                    let size = lineSize * int64 image.Size.Y
                    use temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

                    let l = image.Layout
                    device.perform {
                        do! Command.TransformLayout(image, VkImageLayout.TransferSrcOptimal)
                        do! Command.Copy(image.[TextureAspect.Color, 0, 0], V3i.Zero, temp, 0L, V2i.Zero, image.Size)
                        do! Command.TransformLayout(image, l)
                    }

                    temp.Memory.Mapped (fun ptr ->
                        Marshal.Copy(ptr, dst, size)
                    )

        module GL =
            open OpenTK.Graphics.OpenGL4
            open Aardvark.Rendering.GL

            type SSDownloader(runtime : Runtime) =

                member x.Download(fbo : IFramebuffer, dst : nativeint) =
                    let fbo = unbox<Framebuffer> fbo
                    let ctx = runtime.Context

                    use __ = ctx.ResourceLock
                    let size = fbo.Size

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
                    GL.ReadBuffer(ReadBufferMode.ColorAttachment0)

                    GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, dst)

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

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
                    GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, dst)

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

                member x.Dispose() =
                    use __ = runtime.Context.ResourceLock

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

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, dst)

                //let lineSize = nativeint rowSize
                //let mutable src = ptr
                //let mutable dst = dst + nativeint sizeInBytes - lineSize

                //for _ in 0 .. size.Y-1 do
                //    Marshal.Copy(src, dst, lineSize)
                //    src <- src + lineSize
                //    dst <- dst - lineSize

                if tmpFbo >= 0 then GL.DeleteFramebuffer(tmpFbo)
                if tmpRbo >= 0 then GL.DeleteRenderbuffer(tmpRbo)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)



    type MappedClientRenderTask(server : Server, getScene : IFramebufferSignature -> string -> ConcreteScene) =
        inherit ClientRenderTask(server, getScene)
        let runtime = server.runtime

        let mutable mapping : Option<ISharedMemory> = None
        let mutable downloader : Option<RawDownload.IDownloader> = None
        static let mutable currentId = 0

        static let randomString() =
            let str = System.Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            str.TrimEnd('=').Replace("/", "-")


        let recreateMapping (desiredSize : int64) =
            match mapping with
                | Some m -> m.Dispose()
                | None -> ()

            let name = randomString()
            let m = SharedMemory.create name desiredSize
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

        override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer, _) =
            let desiredMapSize =
                let s = int64 color.Size.X * int64 color.Size.Y * 4L
                if s < 32768L then 32768L
                else Fun.NextPowerOfTwo s

            let mapping =
                match mapping with
                    | Some m when m.Size = desiredMapSize -> m
                    | _ -> recreateMapping desiredMapSize

            //RawDownload.download runtime target color.Samples mapping.data

            let downloader =
                let isMS = color.Samples > 1
                match downloader with
                    | Some d when d.Multisampled = isMS -> d
                    | _ -> recreateDownloader runtime color.Samples

            downloader.Download(target, mapping.Pointer)

            Mapping { name = mapping.Name; size = color.Size; length = int mapping.Size }

        override x.Release() =
            downloader |> Option.iter (fun d -> d.Dispose())
            downloader <- None
            match mapping with
                | Some m -> 
                    m.Dispose()
                    mapping <- None
                | None ->
                    ()
