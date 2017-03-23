namespace Aardvark.UI

open System
open System.Text
open System.Collections.Generic


open Suave
open Suave.Http
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.Incremental
open Aardvark.Service

type App<'model, 'mmodel, 'msg> =
    {
        view : 'mmodel -> Ui<'msg>
        update : 'model -> 'msg -> 'model
        initial : 'model
    }

type MApp<'model, 'mmodel, 'msg> =
    {
        cview : 'mmodel -> Ui<'msg>
        cupdate : 'model -> 'msg -> 'model
        cinit : 'model -> 'mmodel
        capply : 'mmodel -> 'model -> unit
        cinitial : 'model
    }

[<AutoOpen>]
module ``UI Extensions`` =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Ui =
        open System.Threading

        let private main = File.readAllText @"template.html"
       
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


        type Renderable<'msg>(runtime : IRuntime, signature : IFramebufferSignature, sg : ISg<'msg>) =
            inherit AdaptiveObject()

            static let cache = Dict<Ui<'msg>, Renderable<'msg>>()

            static let noCamera = 
                let view = CameraView(V3d.OIO, V3d.Zero, V3d.OOI, V3d.OIO, V3d.IOO)
                let proj = Frustum.ortho (Box3d(-V3d.III, V3d.III))
                Camera.create view proj

            let mutable refCount = 0

            let cam = Mod.init noCamera

            let sg = sg |> Sg.camera cam
            let clear = runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
            let render = runtime.CompileRender(signature, sg)
   
            let task =
                RenderTask.ofList [
                    clear
                    render
                ]

            let mutable runCount = 0

            static member Get(runtime : IRuntime, signature : IFramebufferSignature, ui : Ui<'msg>, sg : ISg<'msg>) =
                lock cache (fun () ->
                    cache.GetOrCreate(ui, fun ui -> Renderable<'msg>(runtime, signature, sg))
                )

            member x.Runtime = runtime

            member x.FramebufferSignature = signature

            member x.Render(token : AdaptiveToken, camera : Camera, output : OutputDescription) =
                x.EvaluateAlways token (fun token ->
                    x.OutOfDate <- true
                    runCount <- runCount + 1

                    let innerToken = token.Isolated
                    try
                        if runCount > 1 then
                            printfn "hate"

                        let old = cam.Value
                        transact (fun () -> cam.Value <- camera)

                        task.Run(innerToken, RenderToken.Empty, output)
                        task.Outputs.Add x |> ignore

                        runCount <- runCount - 1
                    finally
                        innerToken.Release()
                )

            member x.GetResult(cam : IMod<Camera>) =
                RenderableResult(x, cam)

        and RenderableResult<'msg>(r : Renderable<'msg>, camera : IMod<Camera>) =
            inherit Server.AbstractRenderResult()
            let size = Mod.init V2i.II
            let framebuffer = r.Runtime.CreateFramebuffer(r.FramebufferSignature, Set.empty, size)
            do framebuffer.Acquire()

            let innerCamera = Mod.init (Mod.force camera)
            let mutable inEvaluate = false

            member x.Size = size :> IMod<_>
            member x.Camera = innerCamera :> IMod<_>

            override x.Mark() =
                if inEvaluate then false
                else true

            override x.PerformRender(token : AdaptiveToken, s : V2i) =
                inEvaluate <- true

                let cam = camera.GetValue token
                let frustum = cam |> Camera.frustum |> Frustum.withAspect (float s.X / float s.Y)
                let innerCam = Camera.create (Camera.cameraView cam) frustum

                transact (fun () -> 
                    size.Value <- s
                    innerCamera.Value <- innerCam
                )

                let fbo = framebuffer.GetValue()
                let output = OutputDescription.ofFramebuffer fbo

                r.Render(token, innerCam, output)

                inEvaluate <- false
                fbo

            override x.Dispose() =
                transact (fun () ->
                    framebuffer.Release()
                )

        type RenderResult<'msg>(runtime : IRuntime, signature : IFramebufferSignature, sg : ISg<'msg>, cam : IMod<Camera>) =
            inherit Server.AbstractRenderResult()
            let size = Mod.init V2i.II
            let framebuffer = runtime.CreateFramebuffer(signature, Set.empty, size)
            do framebuffer.Acquire()
            

            let cam =
                adaptive {
                    let! s = size
                    let! cam = cam
                    let frustum = cam |> Camera.frustum |> Frustum.withAspect (float s.X / float s.Y)
                    return Camera.create (Camera.cameraView cam) frustum
                }

            let sg = sg |> Sg.camera cam
            let clear = runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
            let render = runtime.CompileRender(signature, sg)

            member x.Size = size :> IMod<_>
            member x.Camera = cam

            override x.PerformRender(token : AdaptiveToken, s : V2i) =
                transact (fun () -> size.Value <- s)
                let fbo = framebuffer.GetValue()
                let output = OutputDescription.ofFramebuffer fbo
                clear.Run(token, RenderToken.Empty, output)
                render.Run(token, RenderToken.Empty, output)
                fbo

            override x.Dispose() =
                transact (fun () ->
                    clear.Dispose()
                    render.Dispose()
                    framebuffer.Release()
                )

        let start (runtime : IRuntime) (port : int) (perform : 'action -> unit) (ui : Ui<'action>) =
            let noCamera = 
                let view = CameraView(V3d.OIO, V3d.Zero, V3d.OOI, V3d.OIO, V3d.IOO)
                let proj = Frustum.ortho (Box3d(-V3d.III, V3d.III))
                Camera.create view proj
    
            let state =
                {
                    newTimeHandlers = Dictionary()
                    timeHandlers = Dictionary()
                    handlers = Dictionary()
                    scenes = Dictionary()
                    references = Dictionary()
                    activeChannels = Dictionary()
                }

            let cameras = Dictionary<string, IMod<V2i> * IMod<Camera>>()

            let messageQueue = new System.Collections.Concurrent.BlockingCollection<'action>()
            let emit (msg : 'action) = messageQueue.Add msg

            let events (s : WebSocket) (ctx : HttpContext) =
                let mutable existingChannels = Dictionary<string * string, Channel>()

                let reader = ui.GetReader()
                let self =
                    Mod.custom (fun self ->
                        let performUpdate (s : WebSocket) =
                            lock reader (fun () ->
                                
                                let expr = 
                                    lock state (fun () -> 
                                        state.newTimeHandlers.Clear()
                                        reader.Update(self, Body, state)
                                    )

                                let code = expr |> JSExpr.toString
                                let code = code.Trim [| ' '; '\r'; '\n'; '\t' |]

                                let newReferences = state.references.Values |> Seq.toArray
                                state.references.Clear()

                                for (KeyValue(id, cb)) in state.newTimeHandlers do
                                    let size, cam = 
                                        match cameras.TryGetValue id with
                                            | (true, (s,c)) -> Mod.force s, Mod.force c
                                            | _ -> V2i.Zero, noCamera

                                    for msg in cb size cam DateTime.Now do
                                        emit msg


                                let code = 
                                    if newReferences.Length > 0 then
                                        let args = 
                                            newReferences |> Seq.map (fun r -> 
                                                sprintf "{ kind: \"%s\", name: \"%s\", url: \"%s\" }" (if r.kind = Script then "script" else "stylesheet") r.name r.url
                                            ) |> String.concat "," |> sprintf "[%s]" 
                                        let code = String.indent 1 code
                                        sprintf "aardvark.addReferences(%s, function() {\r\n%s\r\n});" args code
                                    else
                                        code

                                if code <> "" then
                                    //let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                                    //lock runtime (fun () -> 
                                    //    Log.start "update"
                                    //    for l in lines do Log.line "%s" l
                                    //    Log.stop()
                                    //)

                                    let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("x" + code))) true |> Async.RunSynchronously
                                    match res with
                                        | Choice1Of2 () ->
                                            ()
                                        | Choice2Of2 err ->
                                            failwithf "[WS] error: %A" err

                                let newChannels = Dictionary()
                                for KeyValue((id,name),channel) in state.activeChannels do
                                    newChannels.[(id,name)] <- channel
                                    existingChannels.Remove(id, name) |> ignore
                                    let message = channel.GetMessage(self, id)
                                    match message with
                                        | Some message ->
                                            let res = s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("c" + Pickler.json.PickleToString message))) true |> Async.RunSynchronously
                                            match res with
                                                | Choice1Of2 () ->
                                                    ()
                                                | Choice2Of2 err ->
                                                    failwithf "[WS] error: %A" err
                                        | None -> 
                                            ()

                                for KeyValue((id,name),channel) in existingChannels do
                                    let suicide = { targetId = id; channel = name; data = "commit-suicide" }
                                    s.send Opcode.Text (ByteSegment(Encoding.UTF8.GetBytes("c" + Pickler.json.PickleToString suicide))) true |> Async.RunSynchronously |> ignore

                                    channel.Dispose()

                                existingChannels <- newChannels
                        )

                        performUpdate s
                    )

                let pending = MVar.create()
                let subsription = self.AddMarkingCallback(MVar.put pending)

            
                let mutable running = true
                let updater =
                    async {
                        while running do
                            let! _ = MVar.takeAsync pending
                            if running then
                                self.GetValue(AdaptiveToken.Top)
                    }

                Async.Start updater

                let processor =
                    async {
                        do! Async.SwitchToNewThread()
                        while running do
                            let v = messageQueue.Take()
                            perform v
                    }

                Async.Start processor


                socket {
                    try
                        while running do
                            let! msg = s.readMessage()
                            match msg with
                                | (Opcode.Text, str) ->
                                    let event : Event = Pickler.json.UnPickle str

                                    match state.handlers.TryGetValue ((event.sender, event.name)) with
                                        | (true, f) ->
                                            let size, camera = 
                                                match cameras.TryGetValue event.sender with
                                                    | (true, (size, cam)) -> (Mod.force size, Mod.force cam)
                                                    | _ -> V2i.Zero,noCamera
                                            let action = f size camera (Array.toList event.args)
                                            for a in action do emit a
                                        | _ ->
                                            ()

                                | (Opcode.Close,_) ->
                                    running <- false
                                    MVar.put pending ()
                        
                                | _ ->
                                    ()
                    finally
                        reader.Destroy(state, JSExpr.GetElementById reader.Id) |> ignore
                }

            let parts = 
                [
                    GET >=> path "/main/" >=> OK main
                    GET >=> path "/main/events" >=> handShake events
                ]

            Server.start runtime port parts (fun id signature ->
                match state.scenes.TryGetValue id with
                    | (true, (ui, cam, sg)) -> 
                        let renderable = Renderable<'action>.Get(runtime, signature, ui, sg)
                        let res = renderable.GetResult(cam)

                        res.OnRendered.Add(fun size ->
                            let (exists, handler) = lock state (fun () -> state.timeHandlers.TryGetValue id)
                            if exists then
                                match handler size (Mod.force cam) DateTime.Now with
                                    | [] -> ()
                                    | messages -> 
                                        System.Threading.Tasks.Task.Run(fun () ->
                                            for msg in messages do emit msg
                                        ) |> ignore
                        )

                        //let res = RenderResult(runtime, signature, sg, cam)

                        do lock cameras (fun () -> cameras.[id] <- (res.Size, res.Camera))

                        Some (res :> Server.AbstractRenderResult)
                    | _ -> 
                        None
            )
     
     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module App = 

    let startMApp (runtime : IRuntime) (port : int) (app : MApp<'m, 'mm, 'msg>) =
        let imut = Mod.init app.cinitial
        let mut = app.cinit app.cinitial
        let perform (msg : 'msg) =
            let newImut = app.cupdate imut.Value msg
            transact (fun () ->
                imut.Value <- newImut
                app.capply mut newImut
            )

        let view = app.cview mut

        Ui.start runtime port perform view

    let inline start<'model, 'msg, 'mmodel when 'mmodel : (static member Create : 'model -> 'mmodel) and 'mmodel : (member Update : 'model -> unit)> (runtime : IRuntime) (port : int) (app : App<'model, 'mmodel, 'msg>) =   
        let capp = 
            {
                cview = app.view
                cupdate = app.update
                cinit = fun m -> (^mmodel : (static member Create : 'model -> 'mmodel) (m))
                capply = fun mm m -> (^mmodel : (member Update : 'model -> unit) (mm,m))
                cinitial = app.initial
            }

        startMApp runtime port capp

