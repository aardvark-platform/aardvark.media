namespace Aardvark.Cef.WinForms

open System
open System.IO
open System.Net
open System.Net.WebSockets
open System.Runtime.InteropServices
open System.Diagnostics
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Collections.Concurrent

open OpenTK.Graphics.OpenGL

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Application
open Aardvark.Application.WinForms

[<AutoOpen>]
module private Helpers =
    open System.Collections.Specialized
    open System.Net.Sockets

    module NameValueCollection =
        let toSeq (nvc : NameValueCollection) =
            seq {
                for k in nvc.AllKeys do
                    yield k, nvc.[k]
            }

        let toMap (nvc : NameValueCollection) =
            nvc |> toSeq |> Map.ofSeq

    let getFreePort() =
        let l = new TcpListener(IPAddress.Loopback, 0)
        l.Start()
        let port = (unbox<IPEndPoint> l.LocalEndpoint).Port
        l.Stop()
        port

[<RequireQualifiedAccess>]
type Value =
    | String of string
    | Binary of byte[]


type Request =
    {
        path    : string
        query   : Map<string, string>
    }

type Response =
    { 
        mimeType : string
        content : Value
    }

[<AutoOpen>]
module ``WebSocket Extensions`` =
    
    let private foldReceive (initial : 's) (f : 's -> WebSocketMessageType -> byte[] -> int -> 's) (s : WebSocket) =
        let rec run (buffer : byte[]) (s : WebSocket) (initial : 's) (f : 's -> WebSocketMessageType -> byte[] -> int -> 's) =
            async {
                let! ct = Async.CancellationToken
                let! res = s.ReceiveAsync(ArraySegment(buffer), ct) |> Async.AwaitTask 
                if res.CloseStatus.HasValue then
                    return None
                else
                    let r = f initial res.MessageType buffer res.Count
                    if res.EndOfMessage then
                        return Some r
                    else
                        return! run buffer s r f
                
            }
        run (Array.zeroCreate (32 <<< 10)) s initial f


    let private receiveBytes (s : WebSocket) =
        let rec run (l : System.Collections.Generic.List<byte>) (t : Option<WebSocketMessageType>) =
            async {
                let buffer = Array.zeroCreate (32 <<< 10)
                let! ct = Async.CancellationToken
                let! res = s.ReceiveAsync(ArraySegment(buffer), ct) |> Async.AwaitTask 
            
                match t with
                    | Some t when t <> res.MessageType ->
                        return None
                    | _ -> 
                        if res.Count = buffer.Length then l.AddRange buffer
                        else l.AddRange(Array.take res.Count buffer)

                        if res.EndOfMessage then
                            return Some res.MessageType
                        else
                            return! run l (Some res.MessageType)
            }

        async {
            let buffer = System.Collections.Generic.List()
            let! t = run buffer None
            let buffer = buffer.ToArray()

            match t with
                | Some WebSocketMessageType.Binary -> 
                    return buffer |> Value.Binary |> Some

                | Some WebSocketMessageType.Text ->
                    return buffer |> Encoding.UTF8.GetString |> Value.String |> Some

                | _ -> 
                    return None

        }

    type WebSocket with
        member x.SendAsyncf (fmt : Printf.StringFormat<'a, Async<unit>>) =
            Printf.kprintf (fun str ->
                let binary = Encoding.UTF8.GetBytes str
                x.SendAsync(ArraySegment(binary), WebSocketMessageType.Text, true, Unchecked.defaultof<_>) |> Async.AwaitTask
            ) fmt

        member x.SendAsync (str : string) =
            let binary = Encoding.UTF8.GetBytes str


            x.SendAsync(ArraySegment(binary), WebSocketMessageType.Text, true, Unchecked.defaultof<_>) |> Async.AwaitTask
            
        member x.Send (binary : byte[]) =
            let blockSize = 32 <<< 10
            let mutable offset = 0
            let mutable remaining = binary.Length

            while remaining > 0 do
                let count = min blockSize remaining
                let seg = ArraySegment(binary, offset, count)
                offset <- offset + count
                remaining <- remaining - count
                x.SendAsync(seg, WebSocketMessageType.Binary, (remaining = 0), Unchecked.defaultof<_>).Wait()
     
        member x.ReceiveValueAsync() =
            let rec run (l : System.Collections.Generic.List<byte>) (t : Option<WebSocketMessageType>) =
                async {
                    let buffer = Array.zeroCreate (32 <<< 10)
                    let! ct = Async.CancellationToken
                    let! res = x.ReceiveAsync(ArraySegment(buffer), ct) |> Async.AwaitTask 
            
                    match t with
                        | Some t when t <> res.MessageType ->
                            return None
                        | _ -> 
                            if res.Count = buffer.Length then l.AddRange buffer
                            else l.AddRange(Array.take res.Count buffer)

                            if res.EndOfMessage then
                                return Some res.MessageType
                            else
                                return! run l (Some res.MessageType)
                }

            async {
                let buffer = System.Collections.Generic.List()
                let! t = run buffer None
                let buffer = buffer.ToArray()

                match t with
                    | Some WebSocketMessageType.Binary -> 
                        return buffer |> Value.Binary |> Some

                    | Some WebSocketMessageType.Text ->
                        return buffer |> Encoding.UTF8.GetString |> Value.String |> Some

                    | _ -> 
                        return None

            }
      

[<AbstractClass>]
type Server(port : int) as this =
    let listener = new HttpListener()
    let port = if port <= 0 then getFreePort() else port

    do listener.Prefixes.Add(sprintf "http://localhost:%d/" port)

    let run =
        async {
            listener.Start()
            printfn "server running on port %d" port
            try
                while true do
                    let! context = listener.GetContextAsync() |> Async.AwaitTask
                    let request = context.Request

                    let path = request.Url.LocalPath
                    let query = NameValueCollection.toMap request.QueryString
                    let req = { path = path; query = query }

                    printfn "%A" request.RemoteEndPoint

                    if request.IsWebSocketRequest then
                        let! socketCtx = context.AcceptWebSocketAsync(null) |> Async.AwaitTask
                        this.OnWebSocket(req, socketCtx.WebSocket)
                         
                    else   
                        match this.OnGet req with
                            | Some content ->
                                let target = context.Response
                                target.ContentType <- content.mimeType

                                match content.content with
                                    | Value.String str ->
                                        let bytes = Encoding.UTF8.GetBytes str
                                        target.ContentLength64 <- bytes.LongLength
                                        target.OutputStream.Write(bytes, 0, bytes.Length)

                                    | Value.Binary bytes ->
                                        target.ContentLength64 <- bytes.LongLength
                                        target.OutputStream.Write(bytes, 0, bytes.Length)
                                        
                          

                            | None ->
                                if not request.IsWebSocketRequest then
                                    context.Response.StatusCode <- 404
                                    context.Response.OutputStream.Close()

            with :? HttpListenerException ->
                ()

        }

    let cancel = new CancellationTokenSource()
    let listenerTask = Async.StartAsTask(run, cancellationToken = cancel.Token)

    abstract member OnGet : Request -> Option<Response>
    abstract member OnWebSocket : Request * WebSocket -> unit

    member x.Port = port

    member private x.Dispose(disposing : bool) =
        cancel.Cancel()
        listener.Stop()
        if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose true




module Pickler = 
    open MBrace.FsPickler
    open MBrace.FsPickler.Json

    let binary = FsPickler.CreateBinarySerializer()
    let json = FsPickler.CreateJsonSerializer(false, true)
    
    let ctx =
        System.Runtime.Serialization.StreamingContext()


    let init() =
        let t0 : list<int> = [1;2;3] |> binary.Pickle |> binary.UnPickle
        let t1 : list<int> = [1;2;3] |> json.PickleToString |> json.UnPickleOfString
        if t0 <> t1 then
            failwith "[CEF] could not initialize picklers"

[<AutoOpen>]
module private Messages = 
    [<RequireQualifiedAccess>]
    type EventMessage =
        {
            sender  : string
            name    : string
            args    : string[]
        }
        
    [<RequireQualifiedAccess>]
    type Message =
        { 
            id : int
            kind : string
            payload : string
        }



type MessagePump<'a>(f : 'a -> unit) =
    let queue = new BlockingCollection<'a>()
    
    let run =
        async {
            do! Async.SwitchToNewThread()
            while true do
                let v = queue.Take()
                try f v
                with _ -> ()
        }

    do Async.Start run

    member x.Enqueue(v : 'a) =
        queue.Add v

    member x.Dispose() =
        queue.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module MessagePump =
    let create (f : 'a -> unit) =
        new MessagePump<'a>(f)


type AardvarkServer(runitme : IRuntime, port : int) =
    inherit Server(port)

    static let style = File.ReadAllText @"static\style.css"
    static let boot = File.ReadAllText @"static\bootstrap.js"
    static let mainPage = File.ReadAllText @"static\template.html"

    let events = Event<Event>()
    let eventPump = MessagePump.create (events.Trigger)
    let clients = ConcurrentDictionary<Guid, AardvarkClient>()

    let content = Dictionary<string, IRenderControl -> IRenderTask>()

    member x.Item
        with get (name : string) = content.[name]
        and set (name : string) v = content.[name] <- v

    member x.TryGetRenderContent name =
        match content.TryGetValue name with
            | (true, f) -> Some f
            | _ -> None

    member x.Clients = clients.Values

    member internal x.Trigger (e : Event) =
        eventPump.Enqueue e

    member internal x.Remove (cid : Guid) =
        clients.TryRemove cid |> ignore

    [<CLIEvent>]
    member x.Events = events.Publish

    override x.OnGet (request : Request) =
        match request.path with
            | "/"               -> Some { mimeType = "text/html"; content = Value.String mainPage }
            | "/bootstrap.js"   -> Some { mimeType = "text/javascript"; content = Value.String boot }
            | "/style.css"      -> Some { mimeType = "text/css"; content = Value.String style }
            | _                 -> None

    override x.OnWebSocket(request : Request, socket : WebSocket) =
        let comp = request.path.Split([|'/'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
        match comp with
            | ["events"] ->
                let id = Guid.NewGuid()
                socket.SendAsync (Pickler.json.PickleToString { Message.id = 0; Message.kind = "sessionid"; Message.payload = string id }) |> Async.RunSynchronously
                let c = AardvarkClient(runitme, x, id, socket)
                clients.TryAdd(id, c) |> ignore

            | ["render"; id; session] ->
                let clientId = Guid.Parse session
                match clients.TryGetValue clientId with
                    | (true, c) ->
                        c.RegisterRender(id, socket)
                    | _ -> 
                        ()

            | _ ->
                ()
        ()

and AardvarkClient internal(runtime : IRuntime, server : AardvarkServer, id : Guid, eventSocket : WebSocket) as this =
    let events = Event<Event>()
    let eventPump = MessagePump.create (fun e -> events.Trigger e; server.Trigger e)

    let closed() = server.Remove id

    let mutable currentId = 0
    let newId() = Interlocked.Increment(&currentId)

    let pending = ConcurrentDictionary<int, TaskCompletionSource<string>>()

    let processEventValue (id : Guid) (v : Value) =
        match v with
            | Value.String value ->
                let c = value.[0]
                let value = value.Substring(1)
                if c = 'e' then
                    let e : EventMessage = Pickler.json.UnPickleOfString value
                    eventPump.Enqueue {
                        client  = this
                        sender  = e.sender
                        name    = e.name
                        args    = e.args
                    }

                elif c = 'm' then
                    let r : Message = Pickler.json.UnPickleOfString value
                    match pending.TryRemove r.id with
                        | (true, tcs) -> 
                            match r.kind with
                                | "value" -> tcs.SetResult r.payload
                                | _ -> tcs.SetException(Exception r.payload)
                        | _ -> ()

            | _ ->
                ()

    let rec listen() =
        async {
            let! value = eventSocket.ReceiveValueAsync()
            match value with
                | Some v ->
                    try processEventValue id v
                    with e -> Log.warn "bad event value %A" v

                    do! listen()
                | None ->
                    closed()
        }

    do Async.Start (listen())

    let controls = ConcurrentDictionary<string, AardvarkClientRenderControl>()


    member internal x.RegisterRender(target : string, socket : WebSocket) =
        Log.warn "rendering %A requested on client %A" target id

        let ctrl = new AardvarkClientRenderControl(runtime, x, target)
        match server.TryGetRenderContent target with
            | Some task ->
                ctrl.RenderTask <- task ctrl
            | None ->
                ()

        let rec run () =
            async {
                let! value = socket.ReceiveValueAsync()
                match value with
                    | Some (Value.String value) -> 
                        let c = value.[0]
                        let value = value.Substring 1
                        if c = 'r' then
                            let size : V2i = Pickler.json.UnPickleOfString value
                            let result = ctrl.Render size
                            let data = result.Volume.Data
                            socket.Send(data)

                        elif c = 'g' then
                            ctrl.Received()

                        elif c = 'd' then
                            ctrl.Rendered()

                        do! run()
                    | _ ->
                        ()
            }

        Async.Start (run())




    [<CLIEvent>]
    member x.Events = events.Publish
    member x.Server = server
    member x.Id = id

    member x.EvalAsync (js : string) =
        async {
            let mid = newId()
            let tcs = TaskCompletionSource()
            pending.[mid] <- tcs
            let cmd = { Message.id = mid; Message.kind = "eval"; Message.payload = js } |> Pickler.json.PickleToString
            do! eventSocket.SendAsync(cmd)
            return! Async.AwaitTask tcs.Task
        }

    member x.StartAsync (js : string) =
        let cmd = { Message.id = 0; Message.kind = "eval"; Message.payload = js } |> Pickler.json.PickleToString
        eventSocket.SendAsync cmd

    member x.Start (js : string) =
        x.StartAsync js |> Async.RunSynchronously

    member x.Eval (js : string) =
        x.EvalAsync js |> Async.RunSynchronously

and Event =
    {
        client  : AardvarkClient
        sender  : string
        name    : string
        args    : string[]
    }

and AardvarkClientRenderControl (runtime : IRuntime, parent : AardvarkClient, id : string) =
    
    
    let backgroundColor = Mod.init C4f.Black
    let currentSize = Mod.init V2i.II

    let time = Mod.custom (fun _ -> DateTime.Now)
    
    let mutable frameCounter = 0
    let totalTime = Stopwatch()

    let outputThing = AdaptiveObject()

    let signature =
        runtime.CreateFramebufferSignature(
            1, [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]
        )

    let framebuffer = runtime.CreateFramebuffer(signature, Set.ofList [], currentSize)
    do framebuffer.Acquire()

    let clearTask = runtime.CompileClear(signature, backgroundColor, Mod.constant 1.0)
    let mutable renderTask = RenderTask.empty

    let renderResult =
        Mod.custom (fun self ->

            use t = runtime.ContextLock

            let fbo = framebuffer.GetValue self
            clearTask.Run(self, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
            renderTask.Run(self, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

            let fbo = unbox<Framebuffer> fbo
            let color = unbox<Renderbuffer> fbo.Attachments.[DefaultSemantic.Colors]



            let size = color.Size
            let sizeInBytes = 4 * size.X * size.Y 

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Handle)
            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelPackBuffer, b)
            GL.BufferStorage(BufferTarget.PixelPackBuffer, 4n * nativeint sizeInBytes, 0n, BufferStorageFlags.MapReadBit)
            
            GL.ReadPixels(0, 0, size.X, size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, 0n)

            let ptr = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly)
            let image = PixImage<byte>(Col.Format.RGBA, size)
            Marshal.Copy(ptr, image.Volume.Data, 0, int sizeInBytes)
            GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
            
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            GL.DeleteBuffer(b)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            image
        )


    let queue = new System.Collections.Concurrent.BlockingCollection<Choice<unit, System.Threading.Tasks.TaskCompletionSource<PixImage<byte>>>>()

    let renderer = 
        async {
            do! Async.SwitchToNewThread()
            while true do
                let e = queue.Take()

                try
                    match e with
                        | Choice2Of2 tcs ->
                            let res = renderResult.GetValue(outputThing)
                            tcs.SetResult res
                        | Choice1Of2 () ->
                            AdaptiveSystemState.popReadLocks []
                            outputThing.OutOfDate <- false
                            transact (fun () -> time.MarkOutdated())
                with e ->
                    Log.warn "%A" e
        }

    do Async.Start renderer

    let cb = 
        outputThing.AddMarkingCallback(fun () ->
            totalTime.Start()
            parent.Start (sprintf "render('%s');" id)
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

    let eventSubscription =
        parent.Events.Subscribe (fun e ->
            if e.sender = id then
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



        )


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


    let transferWatch = Stopwatch()

    member internal x.Received() =
        transferWatch.Stop()
        ()
        
    member internal x.Rendered() =
        queue.Add(Choice1Of2 ())
        totalTime.Stop()

        frameCounter <- frameCounter + 1

        if frameCounter >= 100 then
            Log.line "transfer: %A" (transferWatch.MicroTime / float frameCounter)
            Log.line "total:    %A (%.2ffps)" (totalTime.MicroTime / float frameCounter) (float frameCounter / totalTime.MicroTime.TotalSeconds)
            totalTime.Reset()
            transferWatch.Reset()
            frameCounter <- 0

    member internal x.Render(size : V2i) : PixImage<byte> =
        if size <> currentSize.Value then
            transact (fun () -> currentSize.Value <- size)

        let tcs = System.Threading.Tasks.TaskCompletionSource<PixImage<byte>>()
        queue.Add(Choice2Of2 tcs)

        let res = tcs.Task.Result
        transferWatch.Start()
        res

    member x.Background
        with get() = backgroundColor.Value
        and set v = transact (fun () -> backgroundColor.Value <- v)

    member x.Dispose() =
        eventSubscription.Dispose()
        framebuffer.Release()
        clearTask.Dispose()
        renderTask.Dispose()

    member x.RenderTask
        with get() = renderTask
        and set v =
            renderTask.Dispose()
            renderTask <- v

    member x.Sizes = currentSize :> IMod<_>

    member x.FramebufferSignature = signature

    member x.Time = time

    member x.Samples = 1
    
    member x.Runtime = runtime
    
    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse


    interface IRenderTarget with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask 
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Runtime = x.Runtime
        member x.Sizes = x.Sizes
        member x.Samples = x.Samples
        member x.Time = x.Time

    interface IRenderControl with
        member x.Keyboard = x.Keyboard
        member x.Mouse = x.Mouse

    interface IDisposable with
        member x.Dispose() = x.Dispose()

