namespace Aardvark.UI

open System
open System.Diagnostics

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive

[<Struct>]
type internal MappedImage =
    { name   : string
      size   : V2i
      length : int }

[<RequireQualifiedAccess>]
type internal RenderResult =
    | Jpeg    of byte[]
    | Png     of byte[]
    | Mapping of MappedImage

[<AllowNullLiteral>]
type internal ClientRenderTaskOutput(signature: IFramebufferSignature, size: V2i) =
    let runtime = signature.Runtime :?> IRuntime

    let colorFormat =
        signature.ColorAttachments
        |> Map.tryPick (fun _ att -> if att.Name = DefaultSemantic.Colors then Some att.Format else None)
        |> Option.defaultValue TextureFormat.Rgba8

    let depthStencilFormat =
        signature.DepthStencilAttachment
        |> Option.defaultValue TextureFormat.Depth24Stencil8

    let color = runtime.CreateRenderbuffer(size, colorFormat, signature.Samples)
    let depthStencil = runtime.CreateRenderbuffer(size, depthStencilFormat, signature.Samples)

    let framebuffer =
        runtime.CreateFramebuffer(
            signature,
            [
                DefaultSemantic.Colors,       color :> IFramebufferOutput
                DefaultSemantic.DepthStencil, depthStencil :> IFramebufferOutput
            ]
        )

    member _.Signature = signature
    member _.Size = size
    member _.Framebuffer = framebuffer
    member _.Color = color
    member _.DepthStencil = depthStencil

    member _.Dispose() =
        framebuffer.Dispose()
        color.Dispose()
        depthStencil.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

[<AbstractClass>]
type internal ClientRenderTask(runtime: IRuntime, client: int, getScene: IFramebufferSignature -> string -> ConcreteScene) =
    let mutable task = RenderTask.empty

    let mutable output : ClientRenderTaskOutput = null

    let mutable currentInfo  = Unchecked.defaultof<RenderClientInfo>
    let mutable currentScene = Unchecked.defaultof<ConcreteScene>
    let clearColor = AVal.init C4f.Black

    let renderTime = Stopwatch()
    let compressTime = Stopwatch()

    let getInfo() =
        { currentInfo with time = MicroTime.Now; token = AdaptiveToken.Top }

    abstract member ProcessImage : IFramebuffer * IRenderbuffer * RenderQuality -> RenderResult
    abstract member Release : unit -> unit

    member this.Run(token: AdaptiveToken, info: RenderClientInfo) =
        use _  = runtime.ContextLock

        // Update the scene if required
        if isNull currentScene || currentInfo.sceneName <> info.sceneName || currentInfo.signature <> info.signature then
            if notNull currentScene then
                Log.line $"[Client] {client}: Rebuild render task (scene: {currentInfo.sceneName} -> {info.sceneName})"
                currentScene.Scene.RemoveClientInfo(currentInfo.id)

            currentScene <- getScene info.signature info.sceneName
            currentScene.Scene.AddClientInfo(info.id, getInfo)

            let clear = runtime.CompileClear(info.signature, clearColor, AVal.constant 1.0, AVal.constant 0)
            let render = currentScene.CreateNewRenderTask()

            task.Dispose()
            task <- RenderTask.ofList [clear; render]

        currentInfo <- info

        // Recreate the output framebuffer if required
        if isNull output || output.Size <> info.size || output.Signature <> info.signature then
            if notNull output then output.Dispose()
            output <- new ClientRenderTaskOutput(info.signature, info.size)

        // Avoid marking the render task out-of-date as this would break incremental rendering
        // with multiple clients that depend on the same render task.
        currentScene.EvaluateAlways token (fun token ->
            currentScene.OutOfDate <- true

            transact (fun () ->
                clearColor.Value <- info.clearColor
                currentScene.Apply(info)
            )

            renderTime.Start()
            task.Run(token, RenderToken.Empty, output.Framebuffer)
            renderTime.Stop()
        )

        compressTime.Start()
        let data = this.ProcessImage(output.Framebuffer, output.Color, info.quality)
        compressTime.Stop()

        data

    member this.Dispose() =
        try
            this.Release()

            // Note: We only dispose the render task here, which will decrement the reference count of
            // the concrete scene. If the reference count reaches zero, a timer is started to dispose the
            // concrete scene itself.
            task.Dispose()

            if notNull currentScene then
                currentScene.Scene.RemoveClientInfo currentInfo.id
                currentScene <- Unchecked.defaultof<_>
                currentInfo <- Unchecked.defaultof<_>

            if notNull output then
                output.Dispose()
                output <- null

            renderTime.Reset()
            compressTime.Reset()
        with exn ->
            Log.error $"[Client] {client}: Render task disposal failed: {exn}"

    member _.RenderTime = renderTime.MicroTime
    member _.CompressTime = compressTime.MicroTime

    interface IDisposable with
        member this.Dispose() = this.Dispose()

type internal JpegClientRenderTask(runtime: IRuntime, compressor: JpegCompressor voption, client: int,
                                   getScene: IFramebufferSignature -> string -> ConcreteScene,
                                   quality: RenderQuality) =
    inherit ClientRenderTask(runtime, client, getScene)

    let mutable quality = quality
    let mutable gpuCompressorInstance = Unchecked.defaultof<JpegCompressorInstance>
    let mutable resolved = Unchecked.defaultof<IBackendTexture>

    let recreate (format: TextureFormat) (size: V2i) =
        match compressor with
        | ValueSome compressor ->
            if notNull gpuCompressorInstance then gpuCompressorInstance.Dispose()

            Log.line "[Server] Creating GPU image compressor for size: %A" size
            gpuCompressorInstance <- compressor.NewInstance(size, Quantization.photoshop90)

            if notNull resolved then resolved.Dispose()
            resolved <- runtime.CreateTexture2D(size, format)

        | _ ->
            ()

    new(runtime: IRuntime, compressor: JpegCompressor voption, client: int, getScene: IFramebufferSignature -> string -> ConcreteScene) =
        new JpegClientRenderTask(runtime, compressor, client, getScene, RenderQuality.full)

    member x.Quality
        with get() = quality
        and set q = quality <- q

    override _.ProcessImage(target: IFramebuffer, color: IRenderbuffer, renderQuality: RenderQuality) =
        let size = max V2i.One (V2i ((V2d color.Size * renderQuality.scale) + 0.5))

        if isNull resolved || resolved.Size.XY <> size then
            recreate color.Format size

        let data =
            if notNull gpuCompressorInstance then
                if color.Samples > 1 || quality.scale <> 1.0 then
                    runtime.ResolveMultisamples(color, resolved)
                else
                    runtime.Copy(color, resolved.[TextureAspect.Color, 0, 0])

                if quality.quality <> renderQuality.quality then
                    quality <- renderQuality
                    gpuCompressorInstance.Quality <- Quantization.ofQuality quality.quality

                gpuCompressorInstance.Compress(resolved.[TextureAspect.Color,0,0])
            else
                target.DownloadJpegColor(quality.scale, int (round quality.quality))

        RenderResult.Jpeg data

    override x.Release() =
        if notNull gpuCompressorInstance then gpuCompressorInstance.Dispose()
        if notNull resolved then resolved.Dispose()
        gpuCompressorInstance <- Unchecked.defaultof<_>
        resolved <- Unchecked.defaultof<_>

type internal PngClientRenderTask(runtime: IRuntime, client: int, getScene: IFramebufferSignature -> string -> ConcreteScene) =
    inherit ClientRenderTask(runtime, client, getScene)

    let mutable resolved = Unchecked.defaultof<IBackendTexture>

    let recreate  (format: TextureFormat) (size: V2i) =
        if notNull resolved then resolved.Dispose()
        resolved <- runtime.CreateTexture2D(size, format)

    override _.ProcessImage(_: IFramebuffer, color: IRenderbuffer, _) =
        if isNull resolved || resolved.Size.XY <> color.Size then
            recreate color.Format color.Size

        if color.Samples > 1 then
            runtime.ResolveMultisamples(color, resolved)
        else
            runtime.Copy(color, resolved.[TextureAspect.Color, 0, 0])

        let pi = runtime.Download(resolved).ToPixImage<byte>().ToFormat(Col.Format.RGBA)
        use stream = new System.IO.MemoryStream()
        pi.Save(stream, PixFileFormat.Png)
        RenderResult.Png (stream.ToArray())

    override x.Release() =
        if notNull resolved then resolved.Dispose()
        resolved <- Unchecked.defaultof<_>

type internal MappedClientRenderTask(runtime: IRuntime, client: int, getScene: IFramebufferSignature -> string -> ConcreteScene) =
    inherit ClientRenderTask(runtime, client, getScene)

    let mutable mapping : ISharedMemory = null
    let mutable downloader : RawDownload.IDownloader = null

    static let randomString() =
        let str = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        str.TrimEnd('=').Replace("/", "-")

    let recreateMapping (desiredSize: int64) =
        if notNull mapping then mapping.Dispose()
        let name = randomString()
        mapping <- SharedMemory.create name desiredSize

    let recreateDownloader (runtime: IRuntime) (samples: int)  =
        if notNull downloader then downloader.Dispose()

        downloader <-
            match runtime with
            | :? Aardvark.Rendering.Vulkan.Runtime -> RawDownload.Vulkan.createDownloader runtime samples
            | :? Aardvark.Rendering.GL.Runtime     -> RawDownload.GL.createDownloader runtime samples
            | _ -> raise <| ArgumentException($"Invalid runtime {runtime}.")

    override x.ProcessImage(target : IFramebuffer, color : IRenderbuffer, _) =
        let desiredMapSize =
            let s = int64 color.Size.X * int64 color.Size.Y * 4L
            if s < 32768L then 32768L
            else Fun.NextPowerOfTwo s

        if isNull mapping || mapping.Size <> desiredMapSize then
            recreateMapping desiredMapSize

        if isNull downloader || downloader.Multisampled <> (color.Samples > 1) then
            recreateDownloader runtime color.Samples

        downloader.Download(target, mapping.Pointer)

        RenderResult.Mapping {
            name   = mapping.Name
            size   = color.Size
            length = int mapping.Size
        }

    override x.Release() =
        if notNull downloader then downloader.Dispose()
        downloader <- null

        if notNull mapping then mapping.Dispose()
        mapping <- null