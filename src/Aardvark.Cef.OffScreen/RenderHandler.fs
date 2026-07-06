namespace Aardvark.Cef.OffScreen

open System.Buffers
open System.Collections.Concurrent
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open FSharp.Data.Adaptive
open CefSharp
open CefSharp.OffScreen
open System

#nowarn "9"

type internal CefRect = Structs.Rect
type internal CefRange = Structs.Range
type internal CefCursorType = Enums.CursorType
type internal CefCursorInfo = Structs.CursorInfo
type internal CefScreenInfo = Structs.ScreenInfo
type internal CefTextInputMode = Enums.TextInputMode
type internal CefDragOperationsMask = Enums.DragOperationsMask

type CursorChangedArgs =
    { Handle     : nativeint
      Type       : CefCursorType
      CustomInfo : CefCursorInfo
      Cursor     : Cursor }

[<Struct>]
type internal Frame =
    val Size : V2i
    val Data : byte[]

    new (width, height) =
        let sizeInBytes = width * height * 4
        let data = ArrayPool<byte>.Shared.Rent sizeInBytes
        { Size = V2i(width, height); Data = data }

    member inline this.SizeInBytes =
        this.Size.X * this.Size.Y * 4

    member this.Dispose() =
        ArrayPool<byte>.Shared.Return this.Data

    interface IDisposable with
        member this.Dispose() = this.Dispose()

[<AllowNullLiteral>]
type internal AardvarkRenderHandler(host: IBrowserHost, runtime: IRuntime, size: aval<V2i>, mipMap: bool) =
    let version = AVal.init 0
    let texture = runtime.CreateStreamingTexture(mipMap)

    let resizeCallback =
        size.AddCallback(fun _ ->
            host.WasResized()
        )

    let cursorChanged = Event<CursorChangedArgs>()

    let mutable currentFrame : Frame voption = ValueNone
    let frameQueue = new BlockingCollection<Frame>(boundedCapacity = 3)
    let frameCancellationTokenSource = new CancellationTokenSource()

    let processFrames() =
        try
            for frame in frameQueue.GetConsumingEnumerable frameCancellationTokenSource.Token do
                try
                    use pData = fixed frame.Data
                    let t = texture.UpdateAsync(PixFormat.ByteBGRA, frame.Size, pData.Address)
                    useTransaction t (fun () -> version.Value <- version.Value + 1)
                    t.Commit()
                    t.Dispose()
                with exn ->
                    Log.error $"[CEF] Offscreen texture upload faulted: {exn}"

                currentFrame |> ValueOption.iter _.Dispose()
                currentFrame <- ValueSome frame
        with
        | :? OperationCanceledException ->
            ()

    let processingThread = Thread(processFrames, Name = "AardvarkRenderHandler (CEF)", IsBackground = true)
    do processingThread.Start()

    member _.Version = version :> aval<int>
    member _.Texture = texture :> aval<ITexture>
    member _.Size = size

    [<CLIEvent>]
    member _.CursorChanged = cursorChanged.Publish

    member _.Dispose() =
        resizeCallback.Dispose()

        frameCancellationTokenSource.Cancel()
        frameQueue.CompleteAdding()
        processingThread.Join()

        let mutable frame = Unchecked.defaultof<Frame>
        while frameQueue.TryTake &frame do
            frame.Dispose()

        currentFrame |> ValueOption.iter _.Dispose()
        currentFrame <- ValueNone

        frameCancellationTokenSource.Dispose()
        texture.Dispose()
        frameQueue.Dispose()

    member _.GetScreenInfo() =
        Nullable<CefScreenInfo>()

    member _.GetScreenPoint(viewX: int, viewY: int, screenX: byref<int>, screenY: byref<int>) =
        screenX <- viewX
        screenY <- viewY
        false

    member _.GetViewRect() =
        let size = size.GetValue()
        CefRect(0, 0, size.X, size.Y)

    member _.OnCursorChange(cursorHandle: nativeint, cursorType: CefCursorType, customCursorInfo: CefCursorInfo) =
        let cursor =
            match cursorType with
            | Enums.CursorType.None -> Cursor.None 
            | Enums.CursorType.Hand -> Cursor.Hand       
            | Enums.CursorType.Wait -> Cursor.Wait       
            | Enums.CursorType.IBeam -> Cursor.Text       
            | Enums.CursorType.Cross -> Cursor.Crosshair  
            | Enums.CursorType.NotAllowed -> Cursor.NotAllowed 
            | Enums.CursorType.EastWestResize -> Cursor.ResizeH    
            | Enums.CursorType.NorthSouthResize -> Cursor.ResizeV    
            | Enums.CursorType.NortheastSouthwestResize -> Cursor.ResizeNESW 
            | Enums.CursorType.NorthwestSoutheastResize -> Cursor.ResizeNWSE 
            | _ -> Cursor.Default

        cursorChanged.Trigger {
            Handle     = cursorHandle
            Type       = cursorType 
            CustomInfo = customCursorInfo
            Cursor     = cursor
        }

    member _.OnPaint(typ: PaintElementType, _: CefRect, buffer: nativeint, width: int, height: int) =
        if typ = PaintElementType.View && width > 0 && height > 0 then
            let frame = new Frame(width, height)
            let sizeInBytes = int64 frame.SizeInBytes

            use pFrameData = fixed frame.Data
            Buffer.MemoryCopy(buffer, pFrameData.Address, sizeInBytes, sizeInBytes)

            if not <| frameQueue.TryAdd frame then
                frame.Dispose()

    member x.GetPixelValue(pixel: V2i) =
        match currentFrame with
        | ValueSome frame when Vec.allGreaterOrEqual pixel 0 && Vec.allSmaller pixel frame.Size ->
            let offset = 4 * (pixel.Y * frame.Size.X + pixel.X)

            if offset + 3 < frame.SizeInBytes then
                let b = frame.Data.[offset]
                let g = frame.Data.[offset + 1]
                let r = frame.Data.[offset + 2]
                let a = frame.Data.[offset + 3]
                ValueSome <| C4b(r, g, b, a)
            else
                ValueNone
        | _ ->
            ValueNone

    interface IRenderHandler with
        member this.Dispose() = this.Dispose()
        member this.GetScreenInfo() = this.GetScreenInfo()
        member this.GetScreenPoint(viewX, viewY, screenX, screenY) = this.GetScreenPoint(viewX, viewY, &screenX, &screenY)
        member this.GetViewRect() = this.GetViewRect()
        member this.OnAcceleratedPaint(_, _, _) = ()
        member this.OnCursorChange(cursor, typ, customCursorInfo) = this.OnCursorChange(cursor, typ, customCursorInfo)
        member this.OnImeCompositionRangeChanged(_, _) = ()
        member this.OnPaint(typ, dirtyRect, buffer, width, height) = this.OnPaint(typ, dirtyRect, buffer, width, height)
        member this.OnPopupShow _ = ()
        member this.OnPopupSize _ = ()
        member this.OnVirtualKeyboardRequested(_, _) = ()
        member this.StartDragging(_, _, _, _) = false
        member this.UpdateDragCursor _ = ()