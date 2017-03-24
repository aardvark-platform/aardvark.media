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
    | Subscribe of eventName : string
    | Unsubscribe of eventName : string
    


module Pickler =
    open MBrace.FsPickler
    open MBrace.FsPickler.Json

    let json = FsPickler.CreateJsonSerializer(false, true)

type IServerRenderControl =
    inherit IRenderControl
    abstract member Disposed : IEvent<unit>

module Server =
    open System.IO
    open System.Diagnostics

    let private tryFindResource = 
        let self = typeof<IServerRenderControl>.Assembly
        let names = self.GetManifestResourceNames()
        let cache = System.Collections.Concurrent.ConcurrentDictionary<string, Option<string>>()
        fun (name : string) ->
            cache.GetOrAdd(name, fun name ->
                try
                    use stream = self.GetManifestResourceStream(name)
                    let reader = new StreamReader(stream)
                    let content = reader.ReadToEnd()
                    Some content
                with _ ->
                    None
            )




    let template = tryFindResource "template.html" |> Option.get
    let aardvarkjs = tryFindResource "aardvark.js" |> Option.get
    let aardvarkcss = tryFindResource "aardvark.css" |> Option.get

    [<AutoOpen>]
    module private GLDownload = 
        open OpenTK.Graphics
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL
        //open TurboJpegWrapper
        open System.Diagnostics
        open System.Runtime.CompilerServices
        open System.Runtime.InteropServices
        open System.Security
        open Microsoft.FSharp.NativeInterop

        let downloadFBO (jpeg : TJCompressor) (size : V2i) (ctx : Context) =
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
                    TJSubsampling.S420, 
                    90, 
                    TJFlags.BottomUp ||| TJFlags.ForceSSE3
                )

            finally
                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                GL.DeleteBuffer(pbo)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)

        type Framebuffer with
            member x.DownloadJpegColor(jpeg : TJCompressor) =
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
                        ctx |> downloadFBO jpeg size
                    finally
                        GL.DeleteFramebuffer(fbo)
                        GL.DeleteRenderbuffer(resolved)

                else
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, x.Handle)
                    ctx |> downloadFBO jpeg size

    type ServerRenderTask(runtime : IRuntime, signature : IFramebufferSignature, camera : IMod<Camera>, sg : IMod<Trafo3d> -> IMod<Trafo3d> -> aset<IRenderObject>) =
        inherit AbstractRenderTask()

        let aspect = Mod.init 1.0

        let view = camera |> Mod.map (Camera.cameraView >> CameraView.viewTrafo)

        let proj =
            Mod.map2 (fun camera aspect -> camera.frustum |> Frustum.withAspect aspect |> Frustum.projTrafo) camera aspect

        let objects = sg view proj
        let task = runtime.CompileRender(signature, BackendConfiguration.Default, objects)

        member x.Camera = camera

        override x.Use(f : unit -> 'a) =
            task.Use(f)

        override x.Runtime = Some runtime
        override x.FramebufferSignature = Some signature
        override x.PerformUpdate(token, rt) =
            task.Update(token, rt)

        override x.Perform(token, rt, output) =
            let a = float output.viewport.SizeX / float output.viewport.SizeY
            transact (fun () -> aspect.Value <- a)
            task.Run(token, rt, output)

        override x.Dispose() =
            task.Dispose()

    type RenderControl(runtime : IRuntime, targetId : string, sock : WebSocket, samples : int) =
        let mutable camera = Mod.constant (Camera.create (CameraView.lookAt V3d.III V3d.Zero V3d.OOI) (Frustum.perspective 60.0 0.1 100.0 1.0))
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

        let mutable frameCount = 0

        let jpeg = new TJCompressor()

        let ctx = Aardvark.Rendering.GL.ContextHandle.create()
        let glRuntime = unbox<Aardvark.Rendering.GL.Runtime> runtime


        let result : IMod<byte[]> =
            Mod.custom (fun self ->
                
                let renderTask = renderTask.GetValue self
                let fbo = framebuffer.GetValue self

                let output = OutputDescription.ofFramebuffer fbo
                clearTask.Run(self, RenderToken.Empty, output)
                renderTask.Run(self, RenderToken.Empty, output)

                let fbo = unbox<Aardvark.Rendering.GL.Framebuffer> fbo

                let data = fbo.DownloadJpegColor(jpeg)

                data
            )

//        let keyboard = EventKeyboard()
//        let mouse = EventMouse(false)




        let lastPos = PixelPosition() |> Mod.init

        let pickRay =
            lazy (
                Mod.map2 (fun c p -> Camera.pickRay c p |> FastRay3d |> Aardvark.Base.Geometry.RayPart) camera lastPos
            )
        

//        let pos x y =
//            let res = PixelPosition(x,y,currentSize.Value.X, currentSize.Value.Y)
//            lastPos <- res
//            res
//
//        let button b =
//            match b with
//                | 0 -> MouseButtons.Left
//                | 1 -> MouseButtons.Middle
//                | 2 -> MouseButtons.Right
//                | _ -> MouseButtons.None



        let processEvent (e : Event) =
            let s = currentSize.Value

            match e.name with
                | "mousemove" ->
                    let x = Int32.Parse e.args.[0]
                    let y = Int32.Parse e.args.[1]
                    transact (fun () -> lastPos.Value <- PixelPosition(x,y,currentSize.Value.X, currentSize.Value.Y))

                | _ ->
                    ()
//                | "keydown" ->
//                    let key = Int32.Parse e.args.[0] |> KeyConverter.keyFromVirtualKey
//                    keyboard.KeyDown(key)
//
//                | "keyup" ->
//                    let key = Int32.Parse e.args.[0] |> KeyConverter.keyFromVirtualKey
//                    keyboard.KeyUp(key)
//
//                | "keypress" ->
//                    let c = e.args.[0].[0]
//                    keyboard.KeyPress(c)
//                        
//                | "click" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    let b = Int32.Parse e.args.[2] |> button
//                    mouse.Click(pos x y, b)
//
//                | "dblclick" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    let b = Int32.Parse e.args.[2] |> button
//                    mouse.DoubleClick(pos x y, b)
//
//                | "mousedown" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    let b = Int32.Parse e.args.[2] |> button
//                    mouse.Down(pos x y, b)
//
//                | "mouseup" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    let b = Int32.Parse e.args.[2] |> button
//                    mouse.Up(pos x y, b)
//                        
//                | "mousemove" ->
//                    
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    mouse.Move(pos x y)
//                        
//                | "mouseenter" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    mouse.Enter(pos x y)
//                        
//                | "mouseout" ->
//                    let x = Int32.Parse e.args.[0]
//                    let y = Int32.Parse e.args.[1]
//                    mouse.Leave(pos x y)
//                        
//                | "mousewheel" ->
//                    let delta = Double.Parse(e.args.[0], System.Globalization.CultureInfo.InvariantCulture)
//                    mouse.Scroll(lastPos, delta)
//                       
//                | _ ->
//                    ()

        let sem = new SemaphoreSlim(0)
        let eventQueue = new ConcurrentQueue<Event>()
        
        let worker =
            async {
                while true do
                    do! Async.AwaitTask(sem.WaitAsync())
                    match eventQueue.TryDequeue() with
                        | (true, e) -> 
                            do
                                try processEvent e
                                with e -> Log.warn "faulted: %A" e
                        | _ ->
                            ()
            }

        let invalidate() =
            send Invalidate

        do Async.Start worker

        let renderQueue : MVar<V2i> = MVar.empty()
        let renderer =
            async {
                do! Async.SwitchToNewThread()
                do
                    let mutable acquired = false
                    use __ = glRuntime.Context.RenderingLock ctx
                    
                    while true do
                        let size = MVar.take renderQueue
                        if size.AllGreater 0 then
                            try
                                if currentSize.Value <> size then
                                    transact (fun () -> currentSize.Value <- size)

                                if not acquired then
                                    framebuffer.Acquire()
                                    acquired <- true

                                let data = result.GetValue()
                                sendImage data
                            with e ->
                                Log.warn "render faulted %A" e
                
            }

        do Async.Start renderer

        member x.PickRay =
            pickRay.Value
        
        member x.Camera
            with get() = camera
            and set c = camera <- c

        member x.RenderTask
            with get() = renderTask.Value
            and set t = transact (fun () -> renderTask.Value <- t)


        member x.Dispose() =
            try
                transact (fun () ->
                    sem.Dispose()

                    clearTask.Dispose()
                    renderTask.Value.Dispose()

                    framebuffer.Release()
                    runtime.DeleteFramebufferSignature signature
                )
                renderTask.UnsafeCache <- RenderTask.empty
            with e ->
                Log.warn "[Service:%s] shutdown faulted %A" targetId e

        member x.Received (msg : Message) =
            match msg with
                | Event e ->
                    eventQueue.Enqueue e
                    sem.Release() |> ignore

                | Shutdown ->
                    x.Dispose()

                | Rendered ->
                    frameCount <- frameCount + 1



                    transact (fun () -> time.MarkOutdated())

                    lock result (fun () ->
                        if result.OutOfDate then 
                            invalidate()
                        else
                            result.AddVolatileMarkingCallback(fun () -> invalidate()) |> ignore
                    )

                | RequestImage size ->
                    MVar.put renderQueue size
                    
            ()
            
        member x.FramebufferSignature = signature

        interface IRenderTarget with
            member x.Runtime = runtime
            member x.FramebufferSignature = signature
            member x.RenderTask
                with get() = x.RenderTask
                and set t = x.RenderTask <- t

            member x.Samples = samples
            member x.Sizes = currentSize :> IMod<_>
            member x.Time = time


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

    [<AbstractClass>]
    type AbstractRenderResult() =
        inherit AdaptiveObject()
        


        [<DefaultValue; ThreadStatic>]
        static val mutable private TJCompressor : TJCompressor
        
        static let compressor() =
            let current = AbstractRenderResult.TJCompressor
            if isNull current then
                let res = new TJCompressor()
                AbstractRenderResult.TJCompressor <- res
                res
            else
                current
        
        let rendered = new Event<V2i>()
        member x.OnRendered = rendered.Publish



        abstract member PerformRender : AdaptiveToken * V2i -> IFramebuffer
        abstract member Dispose : unit -> unit

        member x.Presented(size : V2i) =
            rendered.Trigger(size)

        member x.Render(token : AdaptiveToken, size : V2i) =
            x.EvaluateAlways token (fun token ->
                let res = x.PerformRender(token, size)
                res
            )

        member x.RenderJpeg(token : AdaptiveToken, size : V2i) =
            let res = x.Render(token, size)
            match res with
                | :? Aardvark.Rendering.GL.Framebuffer as fbo ->
                    fbo.DownloadJpegColor(compressor())
                | _ ->
                    failwith "not implemented"

    let start (runtime : IRuntime) (port : int) (additional : list<WebPart<HttpContext>>) (content : string -> IFramebufferSignature -> Option<AbstractRenderResult>) =
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }

        let signature =
            runtime.CreateFramebufferSignature(
                1, [
                    DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                    DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
                ]
            )

        let render (targetId : string) (s : WebSocket) (context: HttpContext) =
            let request = context.request
            let p = request.query |> Map.ofList
            printfn "%A" p

            match content targetId signature with
                | Some result ->
                    let lastSize = Mod.init V2i.II
                    let jpegData = 
                        Mod.custom (fun token ->
                            let size = lastSize.GetValue token
                            result.RenderJpeg(token, size)
                        )

                    let mutable running = true
                    let renderSize : MVar<V2i> = MVar.empty()

                    let caller = AdaptiveObject()
                    let token = AdaptiveToken(caller, System.Collections.Generic.HashSet())
                    let sub = caller.AddMarkingCallback (fun () -> MVar.put renderSize lastSize.Value)

                    let renderer =
                        async {
                            while running do
                                let! size = MVar.takeAsync renderSize
                                if size.AllGreater(0) then
                                    transact (fun () -> lastSize.Value <- size)
                                    let data = jpegData.GetValue(token)
                                    let! res = s.send Binary (ByteSegment(data)) true
                                    match res with
                                        | Choice1Of2 () -> ()
                                        | Choice2Of2 err ->
                                            Log.error "rendering faulted %A" err
                                else
                                    ()

                        }

                    Async.Start renderer

                    socket {
                        try
                            while running do
                                let! (code, data) = s.readMessage()
                                    
                                match code with
                                    | Text ->
                                        try
                                            let msg : Message = data |> Pickler.json.UnPickle
                                            match msg with
                                                | RequestImage size ->
                                                    MVar.put renderSize size

                                                | Rendered ->
                                                    caller.OutOfDate <- false
                                                    token.Release()
                                                    result.Presented(lastSize.Value)

                                                | Shutdown ->
                                                    running <- false
                                                    MVar.put renderSize V2i.Zero

                                                | Event _ ->
                                                    ()
                                        with _ ->
                                            Log.warn "[Service:%s] bad message: %A" targetId (Encoding.UTF8.GetString data)
                                    
                                    | Close ->
                                        running <- false
                                        MVar.put renderSize V2i.Zero

                                    | _ ->
                                        ()
                        
                        finally
                            sub.Dispose()
                            token.Release()
                            result.Dispose()
                    }
                | None ->
                    socket {
                        ()
                    }

        let index = 
            choose [
                yield GET >=> path "/" >=> OK template
                yield GET >=> path "/aardvark.js" >=> OK aardvarkjs
                yield GET >=> path "/aardvark.css" >=> OK aardvarkcss
                yield pathScan "/render/%s" (render >> handShake)
                yield! additional
            ]

        startWebServer config index






