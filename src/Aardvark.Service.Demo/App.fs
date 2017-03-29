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




    module FileSystem =
        open System.IO
        open System.Text.RegularExpressions

        let diskRx = Regex @"^(?<name>[a-zA-Z_]+):((\\)?)$"

        type FileSystemKind =
            | Folder = 0
            | File = 1
            | Disk = 2
            | DVD = 3
            | Network = 4
            | Removable = 5
            | Unknown = 6

        type FileSystemEntry =
            {
                kind : FileSystemKind
                size : int64
                path : string
                name : string
                lastAccessTime : DateTime
                lastWriteTime : DateTime
                creationTime : DateTime
                hasSubFolders : bool
                hidden : bool
            }

        let cleanPath (path : string) =
            let path = Path.GetFullPath path
            let components = path.Split([| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |], StringSplitOptions.RemoveEmptyEntries) |> Array.toList

            let components =
                match components with
                    | h :: rest ->
                        match Environment.OSVersion with
                            | Windows ->
                                let m = diskRx.Match h
                                if m.Success then m.Groups.["name"].Value :: rest
                                else h :: rest
                            | _ ->
                                h :: rest
                    | [] ->
                        []
                        
            "/" + String.concat "/" components

        let localPath (path : string) =
            let components = path.Split([|'/'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
            
            let components = 
                match Environment.OSVersion, components with
                    | Windows,  h :: rest   -> (h + ":\\") :: rest
                    | _,        h :: rest   -> h :: rest
                    | _                     -> []
                    
            let path = Path.Combine(List.toArray components)
            path


        let getEntries (path : string) =
            let path = localPath path

            if System.IO.Directory.Exists path then
                try 
                    let all = System.IO.Directory.GetFileSystemEntries(path)
                    all |> Array.map (fun path ->
                        let kind, info, size, hasSubFolders = 
                            if System.IO.Directory.Exists(path) then 
                                let info = DirectoryInfo(path)
                                let hasChildren = 
                                    try info.GetDirectories().Length > 0
                                    with _ -> false

                                FileSystemKind.Folder, info :> FileSystemInfo, -1L, hasChildren

                            else 
                                let info = System.IO.FileInfo(path)
                                FileSystemKind.File, info :> FileSystemInfo, info.Length, false


                        let att = info.Attributes

                        {
                            kind = kind
                            size = size
                            path = cleanPath path
                            name = System.IO.Path.GetFileName path
                            lastAccessTime = info.LastAccessTimeUtc
                            lastWriteTime = info.LastWriteTimeUtc
                            creationTime = info.CreationTimeUtc
                            hasSubFolders = hasSubFolders
                            hidden = att.HasFlag(FileAttributes.Hidden) || att.HasFlag(FileAttributes.System)
                        }
                    )
                with _ ->
                    [||]
            else    
                [||]

        let getDrives() =
            System.IO.DriveInfo.GetDrives() |> Array.map (fun d ->
                let kind =
                    match d.DriveType with
                        | IO.DriveType.Fixed -> FileSystemKind.Disk
                        | IO.DriveType.CDRom -> FileSystemKind.DVD
                        | IO.DriveType.Network -> FileSystemKind.Network
                        | IO.DriveType.Removable -> FileSystemKind.Removable
                        | _ -> FileSystemKind.Unknown

                let driveName =
                    let m = diskRx.Match d.Name
                    if m.Success then m.Groups.["name"].Value + ":"
                    else d.Name

                if d.IsReady then
                    let name =
                        if String.IsNullOrWhiteSpace d.VolumeLabel then driveName
                        else d.VolumeLabel + " (" + driveName + ")"

                    let info = DirectoryInfo(d.Name)
                    let hasSubFolders = 
                        try info.GetDirectories().Length > 0
                        with _ -> false

                    {
                        kind = kind
                        size = d.TotalSize
                        path = cleanPath d.Name
                        name = name
                        lastAccessTime = DateTime.MinValue
                        lastWriteTime = DateTime.MinValue
                        creationTime = DateTime.MinValue
                        hasSubFolders = hasSubFolders
                        hidden = false
                    }
                else
                    {
                        kind = kind
                        size = -1L
                        path = cleanPath d.Name
                        name = driveName
                        lastAccessTime = DateTime.MinValue
                        lastWriteTime = DateTime.MinValue
                        creationTime = DateTime.MinValue
                        hasSubFolders = false
                        hidden = false
                    }
            )

        let fs  =
            request (fun r ->
                let data = 
                    match r.queryParam "path" with
                        | Choice2Of2 _ | Choice1Of2 "/" ->
                            match Environment.OSVersion with
                                | Windows -> getDrives()
                                | _ -> getEntries "/"

                        | Choice1Of2 path -> 
                            getEntries path

                let data = Pickler.json.PickleToString data
                OK data >=> Writers.setMimeType "application/json"
            )


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


        let start (runtime : IRuntime) (port : int) (perform : list<'action> -> unit) (ui : Ui<'action>) =
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

            let messageQueue = System.Collections.Generic.List<'action>(1024)
//            let messagesPending = new AutoResetEvent(false)
//            let mutable messageQueue : list<'action> = [] //new System.Collections.Concurrent.BlockingCollection<'action>()
            let emit (msg : list<'action>) = 
                match msg with
                    | [] -> 
                        ()
                    | _ -> 
                        lock messageQueue (fun () ->
                            messageQueue.AddRange msg
                            Monitor.Pulse messageQueue
                        )

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

                                    let msg = cb size cam DateTime.Now
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
                                    let lines = code.Split([| "\r\n" |], System.StringSplitOptions.None)
                                    lock runtime (fun () -> 
                                        Log.start "update"
                                        for l in lines do Log.line "%s" l
                                        Log.stop()
                                    )

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

                let histogram = Dict<int, ref<int>>()
                let mutable steps = 0
                let mutable total = 0

                let processor =
                    async {
                        do! Async.SwitchToNewThread()
                        while running do
                            Monitor.Enter messageQueue
                            while messageQueue.Count = 0 do
                                Monitor.Wait messageQueue |> ignore

                            
                            let h = histogram.GetOrCreate(messageQueue.Count, fun _ -> ref 0)
                            h := !h + 1
                            steps <- steps + 1

                            if steps % 200 = 0 then
                                Log.start "updates"
                                let entries = histogram |> Seq.map (fun (KeyValue(k,v)) -> k,float !v / float steps) |> Seq.toList |> List.sortBy fst
                                for (cnt, f) in entries do
                                    let w = f * 10.0
                                    let width = w |> floor |> int
                                    let frac = w % float width

                                    let c = 
                                        if frac < 0.25 then ""
                                        elif frac < 0.5 then "."
                                        elif frac < 0.75 then "+"
                                        else "#"

                                    let str = System.String('#', width) + c
                                    Log.line "%d: %s" cnt str

                                steps <- 0
                                histogram.Clear()
                                Log.stop()

                            let v = CSharpList.toList messageQueue
                            messageQueue.Clear()
                            Monitor.Exit messageQueue


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
                                            emit action
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
             

            let browser = 
                if Environment.MachineName.ToLower() = "monster64" then
                    fun () -> File.readAllText @"E:\Development\aardvark-media\src\Aardvark.Service.Demo\browser.html"
                else
                    use stream = typeof<App<_,_,_>>.Assembly.GetManifestResourceStream("browser.html")
                    let reader = new System.IO.StreamReader(stream)
                    let str = reader.ReadToEnd()
                    fun () -> str

            let parts = 
                [
                    GET >=> path "/main/" >=> OK main
                    GET >=> path "/main/events" >=> handShake events
                    GET >=> path "/fs" >=> FileSystem.fs
                    GET >=> path "/browser" >=> (fun ctx -> ctx |> OK (browser()))
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
                                            emit messages
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
        let perform (msg : list<'msg>) =
            let newImut = msg |> List.fold app.cupdate imut.Value
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

