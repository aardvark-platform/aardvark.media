namespace Aardvark.UI

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open Aardvark.Base

type WebSocketOpCode =
    | Ping   = 0
    | Pong   = 1
    | Text   = 2
    | Binary = 3
    | Close  = 4

type SocketBuffer(initialSize: int) =
    let mutable buffer = Array.zeroCreate<byte> initialSize
    let mutable position = 0

    member this.Grow([<Optional; DefaultParameterValue(1)>] minGrowBy: int) =
        if minGrowBy > 0 then
            let newSize =
                if minGrowBy >= buffer.Length then buffer.Length + minGrowBy
                else buffer.Length * 2

            Array.Resize(&buffer, newSize)

    member _.Resize(size: int, [<Optional; DefaultParameterValue(false)>] discard: bool) =
        if size <> buffer.Length then
            if discard then
                buffer <- Array.zeroCreate<byte> size
            else
                Array.Resize(&buffer, size)

    member this.Write(data: byte[]) =
        let available = buffer.Length - position
        this.Grow(data.Length - available)
        Buffer.BlockCopy(data, 0, buffer, position, data.Length)
        &position += data.Length

    member _.Position
        with get() = position
        and set value = position <- value

    member _.Size = buffer.Length
    member _.Array = buffer
    member _.Data = ArraySegment(buffer, 0, position)
    member _.Available = ArraySegment(buffer, position, buffer.Length - position)

type IWebSocket =
    inherit IDisposable
    abstract member Send : opCode: WebSocketOpCode * data: byte[] * endOfMessage: bool * cancellationToken: CancellationToken -> Task
    abstract member Receive : buffer: SocketBuffer * cancellationToken: CancellationToken -> Task<WebSocketOpCode>

[<AutoOpen>]
module ``IWebSocket Extensions`` =

    type IWebSocket with
        member inline this.SendPong(cancellationToken: CancellationToken) =
            this.Send(WebSocketOpCode.Pong, Array.empty, true, cancellationToken)