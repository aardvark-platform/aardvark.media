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


type Event =
    {
        sender  : string
        name    : string
        args    : string[]
    }


type Message =
    | RequestImage of size : V2i
    | Rendered
    | Event of evt: Event
    | Shutdown

type Command =
    | Invalidate
    


module Pickler =
    open MBrace.FsPickler
    open MBrace.FsPickler.Json

    let json = FsPickler.CreateJsonSerializer(false, true)




module Server =
    open System.IO
    open System.Diagnostics

    let template = File.ReadAllText("template.html")
    let aardvark = File.ReadAllText("aardvark.js")

    [<AutoOpen>]
    module private GLDownload = 
        open OpenTK.Graphics
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL
        open TurboJpegWrapper
        open System.Diagnostics

        let jpeg = new ThreadLocal<_>(fun () -> new TJCompressor())

        let downloadFBO (timer : Stopwatch) (size : V2i) (ctx : Context) =
            let pbo = GL.GenBuffer()

            let rowSize = 3 * size.X
            let align = ctx.PackAlignment
            let alignedRowSize = (rowSize + (align - 1)) &&& ~~~(align - 1)
            let sizeInBytes = alignedRowSize * size.Y


            try
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)
                GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)
            
                GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Bgr, PixelType.UnsignedByte, 0n)
                let ptr = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly)
                timer.Start()
                jpeg.Value.Compress(ptr, alignedRowSize, size.X, size.Y, Drawing.Imaging.PixelFormat.Format24bppRgb, TurboJpegWrapper.TJSubsamplingOptions.TJSAMP_411, 70, TurboJpegWrapper.TJFlags.NONE)
                    
            finally
                timer.Stop()
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                GL.DeleteBuffer(pbo)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

        type Framebuffer with
            member x.DownloadJpegColor(timer : Stopwatch) =
                let ctx = x.Context
                use __ = ctx.ResourceLock

                let color = x.Attachments.[DefaultSemantic.Colors] |> unbox<Renderbuffer>

                let size = color.Size
                if color.Samples > 1 then
                    let resolved = GL.GenRenderbuffer()
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, resolved)
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgb8, color.Size.X, color.Size.Y)
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
                        ctx |> downloadFBO timer size
                    finally
                        GL.DeleteFramebuffer(fbo)
                        GL.DeleteRenderbuffer(resolved)

                else
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)
                    ctx |> downloadFBO timer size

    type RenderControl(runtime : IRuntime, targetId : string, sock : WebSocket, samples : int) =

        let send (cmd : Command) =
            let data = Pickler.json.Pickle cmd
            match sock.send Opcode.Text (ByteSegment data) true |> Async.RunSynchronously with
                | Choice1Of2 () -> 
                    ()

                | Choice2Of2 err ->
                    Log.warn "[Service:%s] could not send data %A" targetId err


        let sendImage (data : byte[]) =
            match sock.send Binary (ByteSegment data) true |> Async.RunSynchronously with
                | Choice1Of2 () -> 
                    ()

                | Choice2Of2 err ->
                    Log.warn "[Service:%s] could not send data %A" targetId err

        let signature =
            runtime.CreateFramebufferSignature(
                samples,
                [
                    DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                    DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                ]
            )

        let clearTask = runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
        let renderTask = Mod.init RenderTask.empty
        let currentSize = Mod.init V2i.II
        let time = Mod.custom (fun _ -> DateTime.Now)

        let framebuffer = runtime.CreateFramebuffer(signature, Set.empty, currentSize)
        do framebuffer.Acquire()

        let mutable frameCount = 0
        let renderTime      = Stopwatch()
        let downloadTime    = Stopwatch()
        let compressTime    = Stopwatch()
        let presentTime     = Stopwatch()

        let result : IMod<byte[]> =
            Mod.custom (fun self ->
                use __ = runtime.ContextLock
                let renderTask = renderTask.GetValue self
                let fbo = framebuffer.GetValue self

                renderTime.Start()
                let output = OutputDescription.ofFramebuffer fbo
                clearTask.Run(self, RenderToken.Empty, output)
                renderTask.Run(self, RenderToken.Empty, output)
                renderTime.Stop()

                downloadTime.Start()
                let fbo = unbox<Aardvark.Rendering.GL.Framebuffer> fbo
                let data = fbo.DownloadJpegColor(compressTime)
                downloadTime.Stop()

                presentTime.Start()
                data
            )

        let keyboard = EventKeyboard()
        let mouse = EventMouse(false)




        let mutable lastPos = PixelPosition()
        let pos x y =
            let res = PixelPosition(x,y,currentSize.Value.X, currentSize.Value.Y)
            lastPos <- res
            res

        let button b =
            match b with
                | 0 -> MouseButtons.Left
                | 1 -> MouseButtons.Middle
                | 2 -> MouseButtons.Right
                | _ -> MouseButtons.None



        let processEvent (e : Event) =
            let s = currentSize.Value

            match e.name with

                | "keydown" ->
                    let key = Int32.Parse e.args.[0] |> KeyConverter.keyFromVirtualKey
                    keyboard.KeyDown(key)

                | "keyup" ->
                    let key = Int32.Parse e.args.[0] |> KeyConverter.keyFromVirtualKey
                    keyboard.KeyUp(key)

                | "keypress" ->
                    let c = e.args.[0].[0]
                    keyboard.KeyPress(c)
                        
                | "click" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    let b = Int32.Parse e.args.[2] |> button
                    mouse.Click(pos x y, b)

                | "dblclick" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    let b = Int32.Parse e.args.[2] |> button
                    mouse.DoubleClick(pos x y, b)

                | "mousedown" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    let b = Int32.Parse e.args.[2] |> button
                    mouse.Down(pos x y, b)

                | "mouseup" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    let b = Int32.Parse e.args.[2] |> button
                    mouse.Up(pos x y, b)
                        
                | "mousemove" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    mouse.Move(pos x y)
                        
                | "mouseenter" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    mouse.Enter(pos x y)
                        
                | "mouseout" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    mouse.Leave(pos x y)
                        
                | "mousewheel" ->
                    let delta = Double.Parse(e.args.[0], System.Globalization.CultureInfo.InvariantCulture)
                    mouse.Scroll(lastPos, delta)
                       
                | _ ->
                    ()


        let eventQueue = new BlockingCollection<Event>()
        
        let worker =
            async {
                do! Async.SwitchToNewThread()
                while true do
                    let e = eventQueue.Take()
                    do
                        try processEvent e
                        with e -> Log.warn "faulted: %A" e
            }

        let invalidate() =
            async {
                do! Async.SwitchToNewThread()
                result.GetValue() |> ignore
            } |> Async.Start
            send Invalidate

        do Async.Start worker

        member x.RenderTask
            with get() = renderTask.Value
            and set t = transact (fun () -> renderTask.Value <- t)


        member x.Dispose() =
            try
                eventQueue.Dispose()

                clearTask.Dispose()
                renderTask.Value.Dispose()

                framebuffer.Release()
                runtime.DeleteFramebufferSignature signature

                renderTask.UnsafeCache <- RenderTask.empty
            with e ->
                Log.warn "[Service:%s] shutdown faulted %A" targetId e

        member x.Received (msg : Message) =
            match msg with
                | Event e ->
                    eventQueue.Add e

                | Shutdown ->
                    x.Dispose()

                | Rendered ->
                    presentTime.Stop()

                    if frameCount = 100 then
                        Log.start "statistics"

                        Log.line "render    %A" (renderTime.MicroTime / float frameCount)
                        Log.line "download  %A" ((downloadTime.MicroTime - compressTime.MicroTime) / float frameCount)
                        Log.line "compress  %A" (compressTime.MicroTime / float frameCount)
                        Log.line "present   %A" (presentTime.MicroTime / float frameCount)

                        renderTime.Reset()
                        downloadTime.Reset()
                        compressTime.Reset()
                        presentTime.Reset()
                        frameCount <- 0

                        Log.stop()


                    frameCount <- frameCount + 1



                    transact (fun () -> time.MarkOutdated())

                    lock result (fun () ->
                        if result.OutOfDate then 
                            invalidate()
                        else
                            result.AddVolatileMarkingCallback(fun () -> invalidate()) |> ignore
                    )

                | RequestImage size ->
                    try
                        if currentSize.Value <> size then
                            transact (fun () -> currentSize.Value <- size)

                        let data = result.GetValue()
                        sendImage data
                    with e ->
                        Log.warn "render faulted %A" e
                    
            ()


        interface IRenderTarget with
            member x.Runtime = runtime
            member x.FramebufferSignature = signature
            member x.RenderTask
                with get() = x.RenderTask
                and set t = x.RenderTask <- t

            member x.Samples = samples
            member x.Sizes = currentSize :> IMod<_>
            member x.Time = time

        interface IRenderControl with
            member x.Keyboard = keyboard :> IKeyboard
            member x.Mouse = mouse :> IMouse


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

    let start (runtime : IRuntime) (port : int) (content : string -> IRenderControl -> Option<IRenderTask>) =
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }


        let render (targetId : string) (s : WebSocket) (context: HttpContext) =
            let ctrl = RenderControl(runtime, targetId, s, 1)

            match content targetId ctrl with
                | Some task -> ctrl.RenderTask <- task
                | None -> ()

            socket {
                let mutable running = true
                try
                    while running do
                        let! msg = s.readMessage()
                        match msg with
                            | (Text, data) ->
                                try
                                    let msg = data |> Pickler.json.UnPickle
                                    ctrl.Received msg
                                with _ ->
                                    Log.warn "[Service:%s] bad message: %A" targetId (Encoding.UTF8.GetString data)

                            | (Close, _) ->
                                Log.line "[Service:%s] closing" targetId
                                running <- false

                            | (Binary,_) ->
                                Log.warn "[Service:%s] bad message (binary)" targetId
            
                            | _ ->
                                ()
                finally
                    ctrl.Received Shutdown
            }

        let index = 
            choose [
                GET >=> path "/" >=> OK template
                GET >=> path "/aardvark.js" >=> OK aardvark

                pathScan "/render/%s" (render >> handShake)

            ]

        startWebServer config index






