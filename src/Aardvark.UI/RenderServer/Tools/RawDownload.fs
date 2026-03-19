namespace Aardvark.UI

open System
open Aardvark.Base
open Aardvark.Rendering

module internal ReadPixel =
    module private Vulkan =
        open Aardvark.Rendering.Vulkan

        let downloadDepth (pixel : V2i) (img : Image) =
            let temp = img.Device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (uint64 sizeof<uint32>)
            img.Device.perform {
                do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
                //do! Command.TransformLayout(temp, VkImageLayout.TransferDstOptimal)
                //do! Command.Copy(img.[TextureAspect.Depth, 0, 0], V3i(pixel, 0), img.[TextureAspect.Depth, 0, 0], V3i.Zero, V3i.III)
                do! Command.Copy(img.[TextureAspect.Depth, 0, 0], V3i(pixel.X, img.Size.Y - 1 - pixel.Y, 0), temp, 0L, V2i.Zero, V3i.III)
                do! Command.TransformLayout(img, VkImageLayout.DepthStencilAttachmentOptimal)
            }

            let result = temp.Memory.Mapped NativeInt.read<uint32>
            let frac = float (result &&& 0xFFFFFFu) / float ((1 <<< 24) - 1)
            temp.Dispose()
            frac

    let downloadDepth (pixel: V2i) (texture: IBackendTexture) =
        match texture with
        | :? Aardvark.Rendering.Vulkan.Image as img -> Vulkan.downloadDepth pixel img |> ValueSome
        | _ -> ValueNone

module internal RawDownload =
    open System.Runtime.InteropServices

    [<AllowNullLiteral>]
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

            let getTempBuffer (size : uint64) =
                //let size = Fun.NextPowerOfTwo size
                match tempBuffer with
                    | Some b when b.Size = size -> b
                    | _ ->
                        tempBuffer |> Option.iter (fun i -> i.Dispose())
                        let b = device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size
                        tempBuffer <- Some b
                        b

            member x.Runtime = runtime :> IRuntime

            member x.Multisampled = true

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let image = fbo.Attachments.[DefaultSemantic.Colors].Image
                let lineSize = 4UL * uint64 image.Size.X
                let sizeInBytes = lineSize * uint64 image.Size.Y

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

            let getTempBuffer (size : uint64) =
                //let size = Fun.NextPowerOfTwo size
                match tempBuffer with
                    | Some b when b.Size = size -> b
                    | _ ->
                        tempBuffer |> Option.iter (fun i -> i.Dispose())
                        let b = device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size
                        tempBuffer <- Some b
                        b

            member x.Runtime = runtime :> IRuntime

            member x.Multisampled = false

            member x.Download(fbo : IFramebuffer, dst : nativeint) =
                let fbo = unbox<Framebuffer> fbo
                let image = fbo.Attachments.[DefaultSemantic.Colors].Image
                let lineSize = 4UL * uint64 image.Size.X
                let sizeInBytes = lineSize * uint64 image.Size.Y

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
                let lineSize = 4UL * uint64 image.Size.X
                let size = lineSize * uint64 image.Size.Y
                let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                let tempImage = device.CreateImage(image.Size, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                use temp = device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

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
                let lineSize = 4UL * uint64 image.Size.X
                let size = lineSize * uint64 image.Size.Y
                use temp = device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

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

                use _ = ctx.ResourceLock
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

                use _ = ctx.ResourceLock
                let size = fbo.Size
                let temp = getFramebuffer size

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo.Handle)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, temp)

                GL.BlitFramebuffer(
                    0, 0, size.X, size.Y,
                    0, 0, size.X, size.Y,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Nearest
                )

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, temp)

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, dst)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

            member x.Dispose() =
                use _ = runtime.Context.ResourceLock

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

            use _ = ctx.ResourceLock
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
                    0, 0, size.X, size.Y,
                    0, 0, size.X, size.Y,
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