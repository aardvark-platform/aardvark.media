namespace Aardvark.Cef.Internal

open Aardvark.Base
open Aardvark.Base.Incremental
open System
open System.Net
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Xilium.CefGlue
open Newtonsoft.Json.Linq

open Microsoft.FSharp.NativeInterop


#nowarn "9"

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
module CefExtensions =
    
    exception CefException of string

    let inline fail str = raise <| CefException ("[CEF] " + str)
    let inline failf fmt = Printf.kprintf fail fmt
    let inline check str v = if not v then System.Diagnostics.Debugger.Launch() |> ignore; System.Diagnostics.Debugger.Break(); fail str

    type CefBinaryValue with
        member x.ToArray() =
            let arr = Array.zeroCreate (int x.Size)
            let mutable offset = 0L
            let mutable remaining = arr.LongLength
            while remaining > 0L do
                let r = x.GetData(arr, remaining, offset)
                remaining <- remaining - r
                offset <- offset + r
            arr

    type CefBrowser with
        member x.Send(target : CefProcessId, v : 'a) =
            use msg = CefProcessMessage.Create(typeof<'a>.FullName)
            let arr = Pickler.binary.Pickle(v)

            msg.Arguments.SetBinary(0, CefBinaryValue.Create(arr)) 
                |> check "could not set message content"

            x.SendProcessMessage(target, msg) 
                |> check "could not send message"

    type CefV8Context with

        member x.Stringify(v : CefV8Value) =
            if v.IsString then
                v.GetStringValue()
            else
                use glob = x.GetGlobal()
                use json = glob.GetValue("JSON")
                use stringify = json.GetValue("stringify")
                use res = stringify.ExecuteFunction(json, [| v |])
                res.GetStringValue()

        member x.Use (f : unit -> 'a) =
            let mutable entered = false
            try
                entered <- x.Enter()
                f()
            finally
                if entered then
                    let exited = x.Exit()
                    if not exited then 
                        fail "could not exit CefV8Context"


type Event =
    {
        sender  : string
        name    : string
        args    : string[]
    }

module IPC = 
    
    type MessageToken private(id : int) =
        static let mutable current = 0
        static let empty = MessageToken(0)

        member private x.Id = id

        static member Null =
            empty

        static member New =
            let id = Interlocked.Increment(&current)
            MessageToken(id)
    
        member x.IsNull = id = 0
        member x.IsValid = id <> 0

        override x.ToString() = 
            if id = 0 then "tnull"
            else sprintf "t[%d]" id

        override x.GetHashCode() = id
        override x.Equals o =
            match o with
                | :? MessageToken as o -> o.Id = id
                | _ -> false

    type Exception =
        {
            line        : int
            startColumn : int
            endColumn   : int
            message     : string
        }

    type Command =
        | Execute of MessageToken * string
        | GetViewport of MessageToken * string
    
    type Reply =
        | NoFrame of MessageToken
        | Result of MessageToken * string
        | Exception of MessageToken * Exception

        member x.Token =
            match x with
                | NoFrame t -> t
                | Result(t,_) -> t
                | Exception(t,_) -> t

    let tryGet<'a> (msg : CefProcessMessage) =
        if msg.Name = typeof<'a>.FullName && msg.Arguments.Count = 1 then
            let content = msg.Arguments.GetBinary(0)
            if content.IsValid then
                let arr = content.ToArray()
                let cmd = Pickler.binary.UnPickle<'a> arr
                Some cmd
            else
                None
        else
            None

module private Interop = 
    open Aardvark.Base
    open System.Collections.Generic
    open System.Reflection
    open System.IO.MemoryMappedFiles

    type CefResult =
        | NoRet
        | Success of CefV8Value
        | Error of string

    type KeepGC() =
        inherit CefV8ArrayBufferReleaseCallback()

        override x.ReleaseBuffer(_) = ()

    type SharedMemory(browser : CefBrowser, ctx : CefV8Context) as this =
        inherit CefV8Handler()
        let functions : Dictionary<string, CefV8Value[] -> CefResult> = 
            typeof<SharedMemory>.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Seq.filter (fun mi -> mi.ReturnType = typeof<CefResult> && mi.GetParameters().Length = 1)
                |> Seq.map (fun mi -> mi.Name, FunctionReflection.buildFunction this mi)
                |> Dictionary.ofSeq

        let mappings = System.Collections.Concurrent.ConcurrentDictionary<CefV8Value,unit->unit>()

        member x.FunctionNames = functions.Keys

        member x.close(args : CefV8Value[]) =
            match mappings.TryRemove args.[0] with
                | (true,f) -> f()
                | _ -> Log.warn "could not remove mapping"
            NoRet
        
        member x.openMapping(args : CefV8Value[]) =
            if args.Length <> 2 then 
                Log.warn "[CEF Saared Memory] you come to me at runtime (2 args expected)"
                System.Diagnostics.Debugger.Launch() |> ignore
                System.Diagnostics.Debugger.Break()
            let name = args.[0].GetStringValue()
            let desiredSize = args.[1].GetIntValue() |> uint64
            
            let file = MemoryMappedFile.OpenExisting(name)
            let view = file.CreateViewAccessor()

            let handle = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
            let buffer = CefV8Value.CreateArrayBuffer(handle, desiredSize, KeepGC())
            
            let f = CefV8Value.CreateFunction("close", x)

            let obj = CefV8Value.CreateObject()
            obj.SetValue("name", args.[0]) |> check "set mapping.name"
            obj.SetValue("length", args.[1]) |> check "set mapping.length"
            obj.SetValue("buffer", buffer) |> check "set mapping.buffer"
            obj.SetValue("close", f) |> check "set mapping.close"

            let dispose () =
                view.Dispose()
                file.Dispose()
                buffer.Dispose() 

            mappings.TryAdd(obj, dispose) |> ignore

            Success obj

        override x.Execute(name, _, args, ret, exn) =
            match functions.TryGetValue(name) with
                | (true, f) ->
                    match f args with
                        | Success v -> 
                            ret <- v
                            true
                        | NoRet ->
                            true
                        | Error err ->
                            exn <- err
                            false
                | _ ->
                    exn <- sprintf "unknown function %s" name
                    false

    type Aardvark(browser : CefBrowser, ctx : CefV8Context) as this =
        inherit CefV8Handler()
        let functions : Dictionary<string, CefV8Value[] -> CefResult> = 
            typeof<Aardvark>.GetMethods(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Seq.filter (fun mi -> mi.ReturnType = typeof<CefResult> && mi.GetParameters().Length = 1)
                |> Seq.map (fun mi -> mi.Name, FunctionReflection.buildFunction this mi)
                |> Dictionary.ofSeq

        member x.FunctionNames = functions.Keys


        override x.Execute(name, _, args, ret, exn) =
            match functions.TryGetValue(name) with
                | (true, f) ->
                    match f args with
                        | Success v -> 
                            ret <- v
                            true
                        | NoRet ->
                            true
                        | Error err ->
                            exn <- err
                            false
                | _ ->
                    exn <- sprintf "unknown function %s" name
                    false


        member x.testFunction(args : CefV8Value[]) =
            let b = args.[0]

            let field = b.GetType().GetField("_self", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)

           
            let dereferenceObject (ptr : nativeint) =
                Marshal.ReadIntPtr(ptr) + 8n

           
            let readPtr (ptr : nativeint) =
                Marshal.ReadIntPtr(ptr)

            let lines =
                // _cef_v8value_t*
                let structPtr = field.GetValue(b) |> unbox<Pointer>
                let structPtr = System.IntPtr.op_Explicit(Pointer.Unbox structPtr)

                // CefV8Value*
                let cppPtr = structPtr - nativeint (2 * sizeof<nativeint>)

                // &value.firstfield
                let valuePtr = dereferenceObject cppPtr

                // scoped_refptr<Handle>*
                let refPtrPtr = cppPtr + 44n

                // &refptr.firstfield :: Handle*
                let refPtr = readPtr refPtrPtr

                // Handle*
                let handlePtr = readPtr refPtr

                // &handle.firstfield
                let persistentPtr = dereferenceObject handlePtr

                // v8::Value*
                let valuePtr = readPtr persistentPtr

                let dataPtr = readPtr (valuePtr + 7n)

                let value = Marshal.ReadInt32(dataPtr)
                printfn "%X" value


                let et = field.FieldType.GetElementType()
                let self = Marshal.PtrToStructure(structPtr, et)
                let fields = et.GetFields(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)

                fields |> Array.map (fun f ->
                    let res = field.GetValue(self)
                    sprintf "%A %s = %A" f.FieldType f.Name res
                )
                
            File.writeAllLines @"C:\Users\steinlechner\Desktop\bla.txt" lines

            NoRet

        member x.processEvent(args : CefV8Value[]) =
            if args.Length >= 2 then
                let sender = args.[0].GetStringValue()
                let evt = args.[1].GetStringValue()
                let args = args |> Array.skip 2 |> Array.map ctx.Stringify
                browser.Send(CefProcessId.Browser, { sender = sender; name = evt; args = args })
                NoRet
            else
                NoRet

type Priority =
    | Highest = 0
    | High = 1
    | Normal = 2


type TransactThread() =
    let mark = HashSet<IAdaptiveObject>()
    let sem = new SemaphoreSlim(0)

    let runner =
        async {
            do! Async.SwitchToNewThread()
            while true do
                sem.Wait()
                let objects = 
                    lock mark (fun () -> 
                        let m = mark |> HashSet.toArray
                        mark.Clear()
                        m
                    )

                try
                    if objects.Length > 0 then
                        use t = new Transaction()
                        for m in mark do t.Enqueue m
                        t.Commit()

                with e ->
                    Log.warn "exn: %A" e
                
        }

    do Async.Start runner



    member x.Mark(o : IAdaptiveObject) =
        if lock mark (fun () -> mark.Add o) then
            sem.Release() |> ignore

    member x.Mark(o : seq<IAdaptiveObject>) =
        let changed =
            lock mark (fun () -> 
                let cnt = mark.Count
                mark.UnionWith o
                mark.Count <> cnt
            ) 

        if changed then
            sem.Release() |> ignore

type MessagePump() =
    static let cmp = Func<Priority * (unit -> unit), Priority * (unit -> unit), int>(fun (l,_) (r,_) -> compare l r)

    let sem = new SemaphoreSlim(0)
    let queue = Queue<(unit -> unit)>()
    let runner() =
        while true do
            sem.Wait()
            try
                let action = 
                    lock queue (fun () -> 
                        if queue.Count > 0 then 
                            let action = queue.Dequeue()
                            action
                        else 
                            id
                    )

                action()
            with e ->
                Log.warn "exn: %A" e
                
        

    let start = ThreadStart(runner)
    let thread = Thread(start, 1 <<< 26, IsBackground = true, Name = "CefWrapperThread")
    do thread.Start()

    member x.Enqueue(action : unit -> unit) =
        lock queue (fun () ->
            queue.Enqueue(action)
        )
        sem.Release() |> ignore

    member x.Enqueue(priority : Priority, action : unit -> unit) =
        lock queue (fun () ->
            queue.Enqueue(action)
        )
        sem.Release() |> ignore

type RenderProcessHandler() =
    inherit CefRenderProcessHandler()

    let javascriptUtilities =
        """
            document.aardvark.getViewport = function(id) 
            {
                var doc = document.documentElement;
                var dx = (window.pageXOffset || doc.scrollLeft) - (doc.clientLeft || 0);
                var dy = (window.pageYOffset || doc.scrollTop)  - (doc.clientTop || 0);

                var e = document.getElementById(id);

                var w = e.offsetWidth;
                var h = e.offsetHeight;

                function getOffset(e)
                {
                    var t = e.offsetTop;
                    var l = e.offsetLeft;
                    var p = e.offsetParent;
                    if(p == null || p == undefined)
                    {
                        return { t: t, l: l };
                    }
                    else
                    {
                        var pp = getOffset(p);
                        return { t: t + pp.t, l: l + pp.l };
                    }   
                };

                var off = getOffset(e);

                var left = off.l - dx;
                var top = off.t - dy;

                return JSON.stringify({ isValid: true, x: left, y: top, w: w, h: h });

            };

            document.aardvark.openMapping = function (name, len) {
	            var mapping = document.aardvark.sharedMemory.openMapping(name,len);
	            var uint8arr = new Uint8Array(mapping.buffer);
	            var uint8Clamped = new Uint8ClampedArray(mapping.buffer);

	            var result = 
		            { 
			            readString: function() {
				            if(mapping.buffer) {
					            var i = 0;
					            var res = "";
					            while(uint8arr[i] != 0 && i < mapping.length) {
						            res += String.fromCharCode(uint8arr[i]);
						            i++;
					            }
					            return res;
				            }
				            else return "";
			            },

			            readImageData: function(sx, sy) {
				            //return new ImageData(uint8Clamped.slice(0, sx *sy * 4), sx, sy);
                            return new ImageData(uint8Clamped,sx, sy);
			            },

			            close: function() {
				            result.length = 0;
				            result.name = "";
				            result.buffer = new ArrayBuffer(0);
				            result.readString = function() { return ""; };
				            result.readImageData = function() { return null; }
				            result.close = function() { };
				            mapping.close(mapping);
			            },

			            buffer: mapping.buffer,
			            length: mapping.length,
			            name: mapping.name
		            };

	            return result;
            };


        """

    override x.OnContextCreated(browser : CefBrowser, frame : CefFrame, context : CefV8Context) =
        context.Use (fun () ->
            use scope = context.GetGlobal()
            use glob = scope.GetValue("document")

            use target = CefV8Value.CreateObject()
            glob.SetValue("aardvark", target, CefV8PropertyAttribute.DontDelete) 
                |> check "could not set global aardvark-value"

            //System.Diagnostics.Debugger.Launch()
            //System.Diagnostics.Debugger.Break()
            let aardvark = Interop.Aardvark(browser, context)
            for name in aardvark.FunctionNames do
                use f = CefV8Value.CreateFunction(name, aardvark)
                target.SetValue(name, f, CefV8PropertyAttribute.DontDelete) 
                    |> check "could not attach function to aardvark-value"

            let sharedMemoryInterop = Interop.SharedMemory(browser, context)
            use sharedMemory = CefV8Value.CreateObject()
            for name in sharedMemoryInterop.FunctionNames do
                use f = CefV8Value.CreateFunction(name, sharedMemoryInterop)
                sharedMemory.SetValue(name, f, CefV8PropertyAttribute.DontDelete) 
                    |> check "could not attach function to aardvark-value"

            target.SetValue("sharedMemory", sharedMemory) |> check "could not set sharedMemory interop instance"

            let (success, res, exn) = context.TryEval(javascriptUtilities,"javascriptUtilities.autogenerated.js", 0)


            if not success then
                Log.warn "could not register javascript utilities"

            if not (isNull res) then res.Dispose()
            if not (isNull exn) then exn.Dispose()

        )

    override x.OnProcessMessageReceived(browser : CefBrowser, sourceProcess : CefProcessId, message : CefProcessMessage) =
        try
            match IPC.tryGet<IPC.Command> message with
                | Some cmd ->
                    match cmd with
                        | IPC.Execute(token, js) ->
                            use frame = browser.GetFocusedFrame()
                            if isNull frame then
                                // sender wants a reply
                                if token.IsValid then
                                    browser.Send(sourceProcess, IPC.NoFrame token)
                            else
                                use ctx = frame.V8Context
                                ctx.Use (fun () ->
                                    let (success, res, exn) = ctx.TryEval(js, "OnProcessMessageReceived", 0)
                                
                                    // sender wants a reply
                                    if token.IsValid then
                                        if success then
                                            browser.Send(sourceProcess, IPC.Result(token, ctx.Stringify res))
                                        else
                                            let exn =
                                                {
                                                    IPC.Exception.line = exn.LineNumber
                                                    IPC.Exception.startColumn = exn.StartColumn
                                                    IPC.Exception.endColumn = exn.EndColumn
                                                    IPC.Exception.message = exn.Message
                                                }
                                            browser.Send(sourceProcess, IPC.Exception(token, exn))

                                    if not (isNull res) then res.Dispose()
                                    if not (isNull exn) then exn.Dispose()
                                )

                            true

                        | IPC.GetViewport(token, id) ->
                            if token.IsValid then
                                use frame = browser.GetFocusedFrame()
                                if isNull frame then
                                    browser.Send(sourceProcess, IPC.NoFrame token)
                                else
                                    use ctx = frame.V8Context
                                    ctx.Use (fun () ->

               
                                        let (success, res, exn) = ctx.TryEval (sprintf "aardvark.getViewport('%s')" id, "getViewport.autoGenerated,js", 0)
                            
                                        if success then
                                            if res.IsNull || res.IsUndefined then
                                                browser.Send(sourceProcess, IPC.Result(token, "{ isValid: false, x: 0, y: 0, w: 0, h: 0 }"))
                                            else
                                                let str = res.GetStringValue()
                                                browser.Send(sourceProcess, IPC.Result(token, str))

                                        else 
                                            let exn =
                                                {
                                                    IPC.Exception.line = exn.LineNumber
                                                    IPC.Exception.startColumn = exn.StartColumn
                                                    IPC.Exception.endColumn = exn.EndColumn
                                                    IPC.Exception.message = exn.Message
                                                }
                                            browser.Send(sourceProcess, IPC.Exception(token, exn))

                                        if not (isNull res) then res.Dispose()
                                        if not (isNull exn) then exn.Dispose()    
                            
                                        ()
                                    )

                            true

                | _ -> 
                    false

        with e ->
            Log.warn "RenderProcess: %A" e
            false

    override x.GetLoadHandler() =
        base.GetLoadHandler()

    override x.OnBrowserCreated(browser : CefBrowser) =
        base.OnBrowserCreated(browser)

    override x.OnBrowserDestroyed(browser : CefBrowser) =
        base.OnBrowserDestroyed(browser)

    override x.OnContextReleased(browser : CefBrowser, frame : CefFrame, context : CefV8Context) =
        base.OnContextReleased(browser, frame, context)

    override x.OnUncaughtException(browser : CefBrowser, frame : CefFrame, context : CefV8Context, exn : CefV8Exception, stackTrace : CefV8StackTrace) =
        base.OnUncaughtException(browser, frame, context, exn, stackTrace)

type LoadResult =
    | Success
    | Error of message : string * failedUrl : string


open Aardvark.Application

type BrowserKeyboard(focus : ref<bool>, flags : ref<CefEventFlags>, host : CefBrowserHost) =
    inherit EventKeyboard()

    let processKey (k : Keys) (down : bool) =
        let op f g =
            if down then f ||| g
            else f &&& ~~~g

        match k with
            | Keys.LeftCtrl | Keys.RightCtrl -> flags := op !flags CefEventFlags.ControlDown
            | Keys.LeftAlt | Keys.RightAlt -> flags := op !flags CefEventFlags.AltDown
            | Keys.LeftShift | Keys.RightShift -> flags := op !flags CefEventFlags.ShiftDown
            | _ -> ()

    member x.Flags = flags

    override x.KeyDown(k : Keys) =
        if !focus then
            base.KeyDown(k)
            let e = CefKeyEvent()
            e.EventType <- CefKeyEventType.KeyDown
            e.WindowsKeyCode <- KeyConverter.virtualKeyFromKey k
            e.NativeKeyCode <- KeyConverter.virtualKeyFromKey k
            e.Modifiers <- !flags
            host.SendKeyEvent(e)

            processKey k true

    override x.KeyUp(k : Keys) =
        if !focus then
            base.KeyUp(k)
            let e = CefKeyEvent()
            e.EventType <- CefKeyEventType.KeyUp
            e.WindowsKeyCode <- KeyConverter.virtualKeyFromKey k
            e.NativeKeyCode <- KeyConverter.virtualKeyFromKey k
            e.Modifiers <- !flags
            host.SendKeyEvent(e)

            processKey k false

    override x.KeyPress(c : char) =
        if !focus then
            base.KeyPress(c)
            let e = CefKeyEvent()
            e.WindowsKeyCode <- int c
            e.EventType <- CefKeyEventType.Char
            e.Character <- c
            e.Modifiers <- !flags
            host.SendKeyEvent(e)


type BrowserMouse(focus : ref<bool>, flags : ref<CefEventFlags>, host : CefBrowserHost) =
    inherit EventMouse(false)

    let processMouse (pp : PixelPosition) (b : MouseButtons) (down : bool) =
        let t =
            match b with
                | MouseButtons.Left -> CefMouseButtonType.Left
                | MouseButtons.Middle -> CefMouseButtonType.Middle
                | MouseButtons.Right -> CefMouseButtonType.Right
                | _ -> CefMouseButtonType.Left

        let f =
            match b with
                | MouseButtons.Left -> CefEventFlags.LeftMouseButton
                | MouseButtons.Middle -> CefEventFlags.MiddleMouseButton
                | MouseButtons.Right -> CefEventFlags.RightMouseButton
                | _ -> CefEventFlags.None

        let op f g =
            if down then f ||| g
            else f &&& ~~~g


        let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
        host.SendMouseClickEvent(e, t, not down, 1)
        flags := op !flags f


    override x.Down(pp : PixelPosition, b : MouseButtons) =
        if !focus then
            base.Down(pp, b)
            processMouse pp b true

    override x.Up(pp : PixelPosition, b : MouseButtons) =
        if !focus then
            base.Up(pp, b)
            processMouse pp b false

    override x.Click(pp : PixelPosition, b : MouseButtons) =
        if !focus then
            base.Click(pp, b)
            let t : CefMouseButtonType =
                match b with
                    | MouseButtons.Left -> CefMouseButtonType.Left
                    | MouseButtons.Middle -> CefMouseButtonType.Middle
                    | MouseButtons.Right -> CefMouseButtonType.Right
                    | _ -> CefMouseButtonType.Left

            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            host.SendMouseClickEvent(e, t, false, 1)

    override x.DoubleClick(pp : PixelPosition, b : MouseButtons) =
        if !focus then
            base.DoubleClick(pp, b)
            let t : CefMouseButtonType =
                match b with
                    | MouseButtons.Left -> CefMouseButtonType.Left
                    | MouseButtons.Middle -> CefMouseButtonType.Middle
                    | MouseButtons.Right -> CefMouseButtonType.Right
                    | _ -> CefMouseButtonType.Left

            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            
            host.SendMouseClickEvent(e, t, true, 2)

    override x.Scroll(pp : PixelPosition, delta : float) =
        if !focus then
            base.Scroll(pp, delta)
            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            host.SendMouseWheelEvent(e, 0, int delta)
            ()

    override x.Enter(pp : PixelPosition) =
        if !focus then
            base.Enter(pp)
            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            host.SendMouseMoveEvent(e, false)

    override x.Leave(pp : PixelPosition) =
        if !focus then
            base.Enter(pp)
            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            host.SendMouseMoveEvent(e, true)

    override x.Move(pp : PixelPosition) =
        if !focus then
            base.Move(pp)
            let e = CefMouseEvent(pp.Position.X, pp.Position.Y, !flags)
            host.SendMouseMoveEvent(e, false)






type Client(runtime : IRuntime, mipMaps : bool, size : IMod<V2i>) as this =
    inherit CefClient()

    let windowInfo = 
        let info = CefWindowInfo.Create()
        
        info.SetAsWindowless(0n, true)
        let s = Mod.force size
        info.Width <- s.X
        info.Height <- s.Y

        info

    let settings = 
        CefBrowserSettings(
            WindowlessFrameRate = 60
        )


    let mutable browser : CefBrowser = null
    let mutable frame : CefFrame = null
    let mutable host : CefBrowserHost = null

    let focus = ref false
    let flags = ref CefEventFlags.None
    let mutable keyboard : Option<BrowserKeyboard> = None
    let mutable mouse : Option<BrowserMouse> = None


    let browserReady = new ManualResetEventSlim(false)

    let texture = runtime.CreateStreamingTexture(mipMaps)
    let version = Mod.init 0
    

    let loadHandler = LoadHandler(this)
    let renderHandler = RenderHandler(this, size, texture)
    let loadResult = MVar.empty()

    let messagePump = MessagePump()
    let transactor = MessagePump()

    let eventSink = new System.Reactive.Subjects.Subject<Event>()
    let lockObj = obj()

    let pending = ConcurrentDict<IPC.MessageToken, System.Threading.Tasks.TaskCompletionSource<IPC.Reply>>(Dict())

    let sizeSubscription =
        size.AddMarkingCallback (fun () ->
            host.WasResized()
            //host.Invalidate(CefPaintElementType.View)
        )

    member internal x.SetBrowser(b : CefBrowser, f : CefFrame) =
        if isNull browser then
            browser <- b
            frame <- f
            host <- b.GetHost()
            //host.SetWindowlessFrameRate(60)
            
            browserReady.Set()
        elif browser <> b then
            browser <- b

    member internal x.LoadFinished(res : LoadResult) =
        MVar.put loadResult res

    member internal x.Render(f : unit -> Transaction) =
        let t = f()
        version.UnsafeCache <- version.UnsafeCache + 1

        t.Enqueue version
        transactor.Enqueue (fun () -> t.Commit())
        //transactor.Mark [texture :> IAdaptiveObject; version :> IAdaptiveObject]


    member internal x.TryGetScreenLocation(pos : V2i) : Option<V2i> =
        None

    member private x.Init() =
        lock lockObj (fun () ->
            if isNull browser then
                CefBrowserHost.CreateBrowser(windowInfo, x, settings, "about:blank")
                browserReady.Wait()

        )

    override x.OnProcessMessageReceived(browser : CefBrowser, sourceProcess : CefProcessId, message : CefProcessMessage) =
        match IPC.tryGet<Event> message with
            | Some e ->
                messagePump.Enqueue(fun () -> eventSink.OnNext e)
                true
            | _ ->
                match IPC.tryGet<IPC.Reply> message with
                    | Some r ->
                        match pending.TryRemove r.Token with
                            | (true, tcs) -> 
                                tcs.SetResult r
                            | _ -> ()
                        true
                    | None ->
                        false

    override x.GetLoadHandler() = loadHandler :> CefLoadHandler
        
    override x.GetRenderHandler() = renderHandler :> CefRenderHandler

    member x.ExecuteAsync(js : string) =
        lock lockObj (fun () ->
            x.Init()
            async {
                let token = IPC.MessageToken.New
                let tcs = new System.Threading.Tasks.TaskCompletionSource<IPC.Reply>()
                pending.[token] <- tcs
                browser.Send(CefProcessId.Renderer, IPC.Execute(token, js))

                let! res = Async.AwaitTask tcs.Task
                match res with
                    | IPC.Result(_,str) -> 
                        return str
                    | IPC.Exception(_,exn) ->
                        return failf "%A" exn
                    | IPC.NoFrame(_) ->
                        return fail "no frame"
            }
        )

    member x.Execute(js : string) =
        x.ExecuteAsync js |> Async.RunSynchronously

    member x.GetViewportAsync(id : string) =
        lock lockObj (fun () ->
            x.Init()
            async {
                let token = IPC.MessageToken.New
                let tcs = new System.Threading.Tasks.TaskCompletionSource<IPC.Reply>()
                pending.[token] <- tcs
                browser.Send(CefProcessId.Renderer, IPC.GetViewport(token, id))

                let! res = Async.AwaitTask tcs.Task
                match res with
                    | IPC.Result(_,str) -> 
                        let o = JObject.Parse(str)
                        let isValid : bool = o.GetValue("isValid") |> JToken.op_Explicit
                        if isValid then
                            let x : int = o.GetValue("x") |> JToken.op_Explicit
                            let y : int = o.GetValue("y") |> JToken.op_Explicit
                            let w : int = o.GetValue("w") |> JToken.op_Explicit
                            let h : int = o.GetValue("h") |> JToken.op_Explicit
                            return Box2i.FromMinAndSize(x, y, w, h) |> Some
                        else
                            return None

                    | IPC.Exception(_,exn) ->
                        return None
                    | IPC.NoFrame(_) ->
                        return None
            }
        )

    member x.GetViewport(id : string) =
        x.GetViewportAsync id |> Async.RunSynchronously

    member x.SetFocus (v : bool) =
        lock lockObj (fun () ->
            x.Init()
            if !focus <> v then
                focus := v
                host.SendFocusEvent(v)
        )

    member x.Keyboard =
        lock lockObj (fun () ->
            x.Init()
            match keyboard with
                | Some k -> k :> EventKeyboard
                | _ ->
                    let k = BrowserKeyboard(focus, flags, host)
                    keyboard <- Some k
                    k :> EventKeyboard
        )

    member x.Mouse =
        lock lockObj (fun () ->
            x.Init()
            match mouse with
                | Some m -> m :> EventMouse
                | _ ->
                    let m = BrowserMouse(focus, flags, host)
                    mouse <- Some m
                    m :> EventMouse
        )

    member x.ReadPixel(pos : V2i) =
        x.Init()
        renderHandler.GetPixelValue(pos).ToC4f()
        //texture.ReadPixel(pos)

    member x.Events = eventSink :> IObservable<_>

    member x.Texture = texture :> IMod<ITexture>
    member x.Version = version :> IMod<_>
    member x.Size = size

    member x.LoadUrlAsync (url : string) =
        lock lockObj (fun () ->
            x.Init()
            frame.LoadUrl url
            MVar.takeAsync loadResult
        )

    member x.LoadUrl (url : string) =
        lock lockObj (fun () ->
            x.Init()
            frame.LoadUrl url
            MVar.take loadResult
        )

    member x.LoadHtmlAsync (code : string) =
        lock lockObj (fun () ->
            x.Init()
            frame.LoadString(code, "http://aardvark.local/index.html")
            MVar.takeAsync loadResult
        )

    member x.LoadHtml (code : string) =
        lock lockObj (fun () ->
            x.Init()
            frame.LoadString(code, "http://aardvark.local/index.html")
            MVar.take loadResult
        )

    interface IDisposable with
        member x.Dispose() = 
            if isNull browser |> not then browser.Dispose(); browser <- null
            if isNull host |> not then host.Dispose(); host <- null
            base.Dispose(true)

       



and LoadHandler(parent : Client) =
    inherit CefLoadHandler()

    override x.OnLoadStart(browser : CefBrowser, frame : CefFrame, transitionType : CefTransitionType) =
        parent.SetBrowser(browser, frame)

    override x.OnLoadEnd(browser : CefBrowser, frame : CefFrame, status : int) =
        parent.LoadFinished(Success)

    override x.OnLoadError(browser : CefBrowser, frame : CefFrame, errorCode : CefErrorCode, errorText : string, failedUrl : string) =
        parent.LoadFinished(Error(errorText, failedUrl))

and RenderHandler(parent : Client, size : IMod<V2i>, texture : IStreamingTexture) =
    inherit CefRenderHandler()


    let changeCursor (cursor : nativeint) =
        try
            let cursor = new System.Windows.Forms.Cursor(cursor)
            let forms = System.Windows.Forms.Application.OpenForms
            for f in forms do
                f.BeginInvoke(new System.Action(fun () ->
                    f.Cursor <- cursor
                )) |> ignore
        with e ->
            ()

    let boxToRect (b : Box2i) =
        CefRectangle(b.Min.X, b.Min.Y, b.SizeX, b.SizeY)

    let rectToBox (r : CefRectangle) =
        Box2i.FromMinAndSize(r.X, r.Y, r.Width, r.Height)

    let mutable pixelData : byte[] = [||]
    let mutable pixelSize = V2i.Zero

    override x.GetAccessibilityHandler() =
        null

    /// <summary>
    /// Gets virtual screen-info for offscreen rendering. It's possible here to supply a rendering-scale factor/etc.
    /// </summary>
    override x.GetScreenInfo(browser : CefBrowser, info : CefScreenInfo) =
        info.Rectangle <- CefRectangle(0,0,4096, 4096)
        info.AvailableRectangle <- CefRectangle(0,0,4096, 4096)
        true

    /// <summary>
    /// Transforms a local view coordinate into a screen coordinate and returns a boolean value indicating
    /// whether the transformation was successful.
    /// </summary>
    override x.GetScreenPoint(browser : CefBrowser, viewX : int, viewY : int, screenX : byref<int>, screenY : byref<int>) =
        match parent.TryGetScreenLocation(V2i(viewX, viewY)) with
            | Some p ->
                screenX <- p.X
                screenY <- p.Y
                true
            | None ->
                false


    /// <summary>
    /// Initializes the view-rectangle and returns a boolean indicating whether the rectangle is valid.
    /// </summary>
    override x.GetViewRect(browser : CefBrowser, rect : byref<CefRectangle>) =
        let s = Mod.force size
        rect <- CefRectangle(0, 0, s.X, s.Y)
        true

    /// <summary>
    /// NO IDEA WHAT THIS IS EXACTLY
    /// </summary>
    override x.GetRootScreenRect(browser : CefBrowser, rect : byref<CefRectangle>) =
        rect <- CefRectangle(0,0,4096, 4096)
        true

    /// <summary>
    /// The Renderer tells us how large the overlay should be
    /// </summary>
    override x.OnPopupSize(browser : CefBrowser, rect : CefRectangle) =
        //renderer.OnPopupSize(browser, rectToBox rect)
        ()

    /// <summary>
    /// Paint event raised on the "main" process drawing the browser-window contents.
    /// Note that the given buffer is only accessible during the execution of this function.
    /// </summary>
    override x.OnPaint(browser : CefBrowser, elementType : CefPaintElementType, dirtyRects : CefRectangle[], buffer : nativeint, width : int, height : int) : unit =

        if elementType = CefPaintElementType.View then

            let size = V2i(width, height)
            if size <> pixelSize then 
                pixelData <- Array.zeroCreate(width * height * 4)
                pixelSize <- size

            Marshal.Copy(buffer, pixelData, 0, pixelData.Length)

            parent.Render(fun () ->
                let size = V2i(width, height)
                texture.UpdateAsync(PixFormat.ByteBGRA, V2i(width, height), buffer)
            )     

    /// <summary>
    /// Notifies the "main" process to change the cursor-symbol (hovering over text/links/etc.)
    /// </summary>
    override x.OnCursorChange(browser : CefBrowser, cursorHandle : nativeint, a, b) =
        changeCursor(cursorHandle)

    /// <summary>
    /// NO IDEA WHAT THIS IS EXACTLY
    /// </summary>
    override x.OnScrollOffsetChanged(browser : CefBrowser, a, b) =
        ()

    override x.OnImeCompositionRangeChanged(browser, selectedRange, characterBounds) =
        ()

    member x.GetPixelValue (pos : V2i) : C4b =
        if pos.X < pixelSize.X && pos.Y < pixelSize.Y && pos.X >= 0 && pos.Y >= 0 then
            let lookup = 4 * (pos.Y * pixelSize.X + pos.X)

            if lookup+3 < pixelData.Length then
                let b = pixelData.[lookup]
                let g = pixelData.[lookup+1]
                let r = pixelData.[lookup+2]
                let a = pixelData.[lookup+3]
                C4b(r,g,b,a)
            else C4b.Red
        else C4b.Green

type BrowserProcessHandler() =
    inherit CefBrowserProcessHandler()

    override x.OnBeforeChildProcessLaunch(cmd : CefCommandLine) =
//        cmd.AppendSwitch("off-screen-rendering-enabled", "1");
//        cmd.AppendSwitch("off-screen-frame-rate", "60");
//        cmd.AppendSwitch("disable-gpu-compositing");
//        cmd.AppendSwitch("disable-gpu");
//        cmd.AppendSwitch("disable-gpu-vsync");
//        cmd.AppendSwitch("enable-begin-frame-scheduling");
//        cmd.AppendSwitch("enable-media-stream");

        ()

type App() =
    inherit CefApp()

    let renderProcessHandler = lazy ( RenderProcessHandler() )
    let browserProcessHandler = lazy ( BrowserProcessHandler() )

    override x.GetRenderProcessHandler() =
        renderProcessHandler.Value :> CefRenderProcessHandler

//    override x.GetBrowserProcessHandler() =
//        browserProcessHandler.Value :> CefBrowserProcessHandler
//

    override x.GetResourceBundleHandler() =
        base.GetResourceBundleHandler()

