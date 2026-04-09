namespace Aardvark.UI

open Aardvark.Base
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open System

#nowarn "9"

[<AutoOpen>]
module internal ``JpegDownload Extensions`` =

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
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[TextureAspect.Color, 0, 0]

            let resultBuffer = device.ReadbackMemory.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)

            let scaled =
                if size <> fbo.Size then
                    let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    device.CreateImage(size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                else
                    Unchecked.defaultof<_>

            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)
                if notNull scaled then
                    do! Command.TransformLayout(scaled, VkImageLayout.TransferDstOptimal)
                    do! Command.Blit(color, VkImageLayout.TransferSrcOptimal, scaled.[TextureAspect.Color, 0, 0], VkImageLayout.TransferDstOptimal, VkFilter.Linear)
                    do! Command.TransformLayout(scaled, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(scaled.[TextureAspect.Color, 0, 0], resultBuffer)
                else
                    do! Command.Copy(color, resultBuffer)
                do! Command.TransformLayout(color.Image, oldLayout)
            }

            let rowSize = 4 * size.X
            let alignedRowSize = rowSize

            let result =
                resultBuffer.Volume.Mapped (fun src ->
                    jpeg.Compress(
                        NativePtr.toNativeInt src.Pointer, alignedRowSize, size.X, size.Y,
                        TJPixelFormat.RGBX,
                        TJSubsampling.S444,
                        quality,
                        TJFlags.BottomUp ||| TJFlags.ForceSSE3
                    )
                )

            if notNull scaled then scaled.Dispose()
            resultBuffer.Dispose()
            result

        let downloadFBOMS (jpeg : TJCompressor) (size : V2i) (quality : int) (fbo : Framebuffer) =
            let device = fbo.Device
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[TextureAspect.Color, 0, 0]

            let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
            let resolved = device.CreateImage(size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)

            let resolvedFull =
                if size <> fbo.Size then
                    device.CreateImage(fbo.Size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                else
                    Unchecked.defaultof<_>

            let resultBuffer = device.ReadbackMemory.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)
            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)

                if notNull resolvedFull then
                    do! Command.TransformLayout(resolvedFull, VkImageLayout.TransferDstOptimal)
                    do! Command.ResolveMultisamples(color.Image.[TextureAspect.Color, 0, 0], V3i.Zero, resolvedFull.[TextureAspect.Color, 0, 0], V3i.Zero, color.Image.Size)
                    do! Command.TransformLayout(resolvedFull, VkImageLayout.TransferSrcOptimal)
                    do! Command.TransformLayout(resolved, VkImageLayout.TransferDstOptimal)
                    do! Command.Blit(
                        resolvedFull.[TextureAspect.Color, 0, 0], VkImageLayout.TransferSrcOptimal,
                        resolved.[TextureAspect.Color, 0, 0], VkImageLayout.TransferDstOptimal,
                        VkFilter.Linear
                    )
                else
                    do! Command.TransformLayout(resolved, VkImageLayout.TransferDstOptimal)
                    do! Command.ResolveMultisamples(color.Image.[TextureAspect.Color, 0, 0], V3i.Zero, resolved.[TextureAspect.Color, 0, 0], V3i.Zero, color.Image.Size)

                do! Command.TransformLayout(resolved, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(resolved.[TextureAspect.Color, 0, 0], resultBuffer)
                do! Command.TransformLayout(color.Image, oldLayout)
            }

            let rowSize = 4 * size.X
            let alignedRowSize = rowSize

            let result =
                resultBuffer.Volume.Mapped (fun src ->
                    jpeg.Compress(
                        NativePtr.toNativeInt src.Pointer, alignedRowSize, size.X, size.Y,
                        TJPixelFormat.RGBX,
                        TJSubsampling.S444,
                        quality,
                        TJFlags.BottomUp ||| TJFlags.ForceSSE3
                    )
                )

            if notNull resolvedFull then resolvedFull.Dispose()
            resolved.Dispose()
            resultBuffer.Dispose()
            result

        type Framebuffer with
            member x.DownloadJpegColor(scale : float, quality : int) =
                let resSize = V2i(max 1 (int (round (scale * float x.Size.X))), max 1 (int (round (scale * float x.Size.Y))))
                let jpeg = Compressor.Instance
                if x.Attachments.[DefaultSemantic.Colors].Image.Samples <> 1 then
                    downloadFBOMS jpeg resSize quality x
                else
                    downloadFBO jpeg resSize quality x

    [<AutoOpen>]
    module GL =
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL

        let private downloadFBO (jpeg : TJCompressor) (size : V2i) (quality : int) (ctx : Context) =
            let rowSize = 3 * size.X
            let align = ctx.PackAlignment
            let alignedRowSize = (rowSize + (align - 1)) &&& ~~~(align - 1)
            let sizeInBytes = alignedRowSize * size.Y

            let ptr = NativePtr.alloc<byte> sizeInBytes
            try
                let src = NativePtr.toNativeInt ptr
                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgb, PixelType.UnsignedByte, src)

                jpeg.Compress(
                    src, alignedRowSize, size.X, size.Y,
                    TJPixelFormat.RGB,
                    TJSubsampling.S444,
                    quality,
                    TJFlags.BottomUp ||| TJFlags.ForceSSE3
                )

            finally
                NativePtr.free ptr
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

        type Framebuffer with
            member x.DownloadJpegColor(scale : float, quality : int) =
                let jpeg = Compressor.Instance
                let ctx = x.Context
                use _ = ctx.ResourceLock

                let color = x.Attachments.[DefaultSemantic.Colors] |> unbox<Renderbuffer>

                let size = color.Size
                if color.Samples > 1 then
                    let resolved = GL.GenRenderbuffer()
                    let fbo = GL.GenFramebuffer()
                    try

                        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, resolved)
                        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, size.X, size.Y)
                        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
                        GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, resolved)

                        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)

                        GL.BlitFramebuffer(
                            0, 0, size.X, size.Y,
                            0, 0, size.X, size.Y,
                            ClearBufferMask.ColorBufferBit,
                            BlitFramebufferFilter.Nearest
                        )

                        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)

                        if scale < 1.0 then
                            let resSize = V2i(max 1 (int (round (scale * float size.X))), max 1 (int (round (scale * float size.Y))))
                            let scaled = GL.GenRenderbuffer()
                            let scaledFbo = GL.GenFramebuffer()
                            try
                                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, scaled)
                                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, resSize.X, resSize.Y)
                                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)


                                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, scaledFbo)
                                GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, scaled)


                                GL.BlitFramebuffer(
                                    0, 0, size.X, size.Y,
                                    0, 0, resSize.X, resSize.Y,
                                    ClearBufferMask.ColorBufferBit,
                                    BlitFramebufferFilter.Linear
                                )

                                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, scaledFbo)
                                ctx |> downloadFBO jpeg resSize quality
                            finally
                                GL.DeleteFramebuffer(scaledFbo)
                                GL.DeleteRenderbuffer(scaled)

                        else
                            ctx |> downloadFBO jpeg size quality
                    finally
                        GL.DeleteFramebuffer(fbo)
                        GL.DeleteRenderbuffer(resolved)

                else
                    if scale < 1.0 then
                        let resSize = V2i(max 1 (int (round (scale * float size.X))), max 1 (int (round (scale * float size.Y))))
                        let scaled = GL.GenRenderbuffer()
                        let scaledFbo = GL.GenFramebuffer()
                        try
                            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, scaled)
                            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, resSize.X, resSize.Y)
                            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)


                            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, scaledFbo)
                            GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, scaled)

                            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)

                            GL.BlitFramebuffer(
                                0, 0, size.X, size.Y,
                                0, 0, resSize.X, resSize.Y,
                                ClearBufferMask.ColorBufferBit,
                                BlitFramebufferFilter.Linear
                            )

                            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, scaledFbo)
                            ctx |> downloadFBO jpeg resSize quality
                        finally
                            GL.DeleteFramebuffer(scaledFbo)
                            GL.DeleteRenderbuffer(scaled)
                    else
                        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)
                        ctx |> downloadFBO jpeg size quality

    type IFramebuffer with
        member this.DownloadJpegColor(scale: float, quality: int) =
            match this with
            | :? Aardvark.Rendering.GL.Framebuffer as fbo     -> fbo.DownloadJpegColor(scale, quality)
            | :? Aardvark.Rendering.Vulkan.Framebuffer as fbo -> fbo.DownloadJpegColor(scale, quality)
            | _ -> raise <| ArgumentException($"Invalid framebuffer {this}.")