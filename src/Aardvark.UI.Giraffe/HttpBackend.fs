namespace Aardvark.UI.Giraffe

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open Giraffe

open Aardvark.Base
open Aardvark.UI

module internal WebSocketMessageType =

    let ofWebSocketOpCode =
        LookupTable.lookup [
            WebSocketOpCode.Text,   WebSocketMessageType.Text
            WebSocketOpCode.Binary, WebSocketMessageType.Binary
            WebSocketOpCode.Close,  WebSocketMessageType.Close
        ]

    let toWebSocketOpCode =
        LookupTable.lookup [
            WebSocketMessageType.Text,   WebSocketOpCode.Text
            WebSocketMessageType.Binary, WebSocketOpCode.Binary
            WebSocketMessageType.Close,  WebSocketOpCode.Close
        ]

type internal WebSocket(socket: System.Net.WebSockets.WebSocket) =
    let sendSemaphore = new SemaphoreSlim(1, 1)
    let recvSemaphore = new SemaphoreSlim(1, 1)

    member _.Send(message: WebSocketOpCode, data: byte[], endOfMessage: bool, cancellationToken: CancellationToken) =
        if message = WebSocketOpCode.Ping || message = WebSocketOpCode.Pong then
            Task.CompletedTask
        else
            let data = ArraySegment data
            let messageType = WebSocketMessageType.ofWebSocketOpCode message

            task {
                do! sendSemaphore.WaitAsync()

                try
                    do! socket.SendAsync(data, messageType, endOfMessage, cancellationToken)
                finally
                    sendSemaphore.Release() |> ignore
            }

    member _.Receive(buffer: SocketBuffer, cancellationToken: CancellationToken) =
        task {
            do! recvSemaphore.WaitAsync()

            try
                let mutable messageType = -1

                while messageType < 0 do
                    let! result = socket.ReceiveAsync(buffer.Available, cancellationToken)
                    buffer.Position <- buffer.Position + result.Count

                    if result.EndOfMessage then
                        messageType <- int result.MessageType
                    else
                        buffer.Grow()

                return WebSocketMessageType.toWebSocketOpCode (enum<WebSocketMessageType> messageType)
            finally
                recvSemaphore.Release() |> ignore
        }

    member _.Dispose() =
        sendSemaphore.Dispose()
        recvSemaphore.Dispose()

    interface IWebSocket with
        member this.Send(message, data, endOfMessage, cancellationToken) = this.Send(message, data, endOfMessage, cancellationToken)
        member this.Receive(buffer, cancellationToken) = this.Receive(buffer, cancellationToken)
        member this.Dispose() = this.Dispose()

type HttpBackend private () =
    static let instance = HttpBackend()
    static member Instance : IHttpBackend<_, _> = instance

    interface IHttpBackend<HttpContext, HttpHandler> with
        member _.withContext handler =
            fun next context ->
                let handler = handler context
                handler next context

        member _.getRequest context =
            { new IHttpRequest with
                member _.Path = context.Request.Path.Value
                member _.Body = context.Request.Body
                member _.Method = context.Request.Method
                member _.Header(name) =
                    match context.Request.Headers.TryGetValue name with
                    | true, values when values.Count > 0 -> Some (values |> String.concat ", ")
                    | _ -> None
                member _.Headers =
                    context.Request.Headers
                    |> Seq.map (function KeyValue(n, v) -> n, String.concat ", " v)
                    |> Map.ofSeq
                member _.QueryParam(name) =
                    context.Request.Query.[name] |> Seq.tryHead
                member _.QueryParams =
                    context.Request.Query
                    |> Seq.map (function KeyValue(n, v) -> n, List.ofSeq v)
                    |> Map.ofSeq
            }

        member _.async handler =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let! handler = handler
                    return! handler next ctx
                }

        member _.choose handlers =
            choose handlers

        member _.route path =
            route path

        member _.routef path handler =
            routef path handler

        member _.subRoute path handler =
            subRoute path handler

        member _.compose h1 h2 =
            compose h1 h2

        member this.mimeType mimeType =
            fun next context ->
                context.SetContentType mimeType
                next context

        member _.redirectTo permanent location =
            redirectTo permanent location

        member _.handShake continuation =
            fun next (context: HttpContext) ->
                task {
                    if context.WebSockets.IsWebSocketRequest then
                        let! socket = context.WebSockets.AcceptWebSocketAsync()
                        let! _ = continuation (new WebSocket(socket)) context
                        return! next context
                    else
                        return! RequestErrors.BAD_REQUEST "Expected web socket request" next context
                }

        member _.method httpMethod =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                if ctx.Request.Method = httpMethod then
                    next ctx
                else
                    skipPipeline

        member _.header key value =
            setHttpHeader key value

        member _.status (status: int) =
            setStatusCode status

        member _.response (data: uint8[]) =
            fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteBytesAsync data

        member this.response (data: string) =
            fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteStringAsync data

        member _.sendFile filePath =
            fun (_: HttpFunc) (ctx: HttpContext) ->
                task {
                    let filePath =
                        match Path.IsPathRooted filePath with
                        | true -> filePath
                        | false ->
                            let env = ctx.GetWebHostEnvironment()
                            Path.Combine(env.ContentRootPath, filePath)

                    let! html = readFileAsStringAsync filePath
                    return! ctx.WriteStringAsync html
                }