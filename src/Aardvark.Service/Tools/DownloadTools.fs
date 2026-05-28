#nowarn "9"
namespace Aardvark.Service


open System
open System.Text
open System.Net
open System.Threading
open System.Collections.Concurrent

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open Aardvark.Application
open System.Diagnostics

open Microsoft.FSharp.NativeInterop
open System.Threading.Tasks

//#nowarn "9"
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
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[TextureAspect.Color, 0, 0]

            let tmp = device.ReadbackMemory.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)

            let small =
                if size <> fbo.Size then
                    let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
                    device.CreateImage(size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage) |> Some
                else
                    None

            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)
                match small with
                | Some small ->
                    do! Command.TransformLayout(small, VkImageLayout.TransferDstOptimal)
                    do! Command.Blit(color, VkImageLayout.TransferSrcOptimal, small.[TextureAspect.Color, 0, 0], VkImageLayout.TransferDstOptimal, VkFilter.Linear)
                    do! Command.TransformLayout(small, VkImageLayout.TransferSrcOptimal)
                    do! Command.Copy(small.[TextureAspect.Color, 0, 0], tmp)
                | None ->
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

            small |> Option.iter (fun s -> s.Dispose())
            tmp.Dispose()
            result

        let downloadFBOMS (jpeg : TJCompressor) (size : V2i) (quality : int) (fbo : Framebuffer) =
            let device = fbo.Device
            let color = fbo.Attachments.[DefaultSemantic.Colors].Image.[TextureAspect.Color, 0, 0]

            let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit
            let tempImage = device.CreateImage(size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)

            let full =
                if size <> fbo.Size then
                    device.CreateImage(fbo.Size.XYI, 1, 1, 1, TextureDimension.Texture2D, VkFormat.R8g8b8a8Unorm, usage)
                    |> Some
                else
                    None

            let tmp = device.ReadbackMemory.CreateTensorImage<byte>(V3i(size, 1), Col.Format.RGBA, false)
            let oldLayout = color.Image.Layout
            device.perform {
                do! Command.TransformLayout(color.Image, VkImageLayout.TransferSrcOptimal)

                match full with
                | Some full ->
                    do! Command.TransformLayout(full, VkImageLayout.TransferDstOptimal)
                    do! Command.ResolveMultisamples(color.Image.[TextureAspect.Color, 0, 0], V3i.Zero, full.[TextureAspect.Color, 0, 0], V3i.Zero, color.Image.Size)
                    do! Command.TransformLayout(full, VkImageLayout.TransferSrcOptimal)
                    do! Command.Blit(full.[TextureAspect.Color, 0, 0], VkImageLayout.TransferSrcOptimal, tempImage.[TextureAspect.Color, 0, 0], VkImageLayout.TransferDstOptimal, VkFilter.Linear)
                | None -> 
                    do! Command.ResolveMultisamples(color.Image.[TextureAspect.Color, 0, 0], V3i.Zero, tempImage.[TextureAspect.Color, 0, 0], V3i.Zero, color.Image.Size)
                do! Command.TransformLayout(tempImage, VkImageLayout.TransferSrcOptimal)
                do! Command.Copy(tempImage.[TextureAspect.Color, 0, 0], tmp)
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

            full |> Option.iter (fun f -> f.Dispose())
            tempImage.Dispose()
            tmp.Dispose()
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
        open OpenTK.Graphics
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
                use __ = ctx.ResourceLock

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
        member x.DownloadJpegColor(scale : float, quality : int) =
            match x with
                | :? Aardvark.Rendering.GL.Framebuffer as fbo -> fbo.DownloadJpegColor(scale, quality)
                | :? Aardvark.Rendering.Vulkan.Framebuffer as fbo -> fbo.DownloadJpegColor(scale, quality)
                | _ -> failwith "not implemented"
 
