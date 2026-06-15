namespace Aardvark.UI.Giraffe

open System
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open Giraffe

open Aardvark.Base
open Aardvark.UI

[<AutoOpen>]
module internal AspNetExtensions =
    open Microsoft.Extensions.Primitives

    [<return: Struct>]
    let (|SingleString|_|) (values: StringValues) =
        if values.Count = 1 then ValueSome (values.Item 0) else ValueNone

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

        member _.requestPath context =
            context.Request.Path.Value

        member _.requestMethod context =
            context.Request.Method

        member _.requestQueryParams context =
            context.Request.Query
            |> Seq.choose (function KeyValue(n, SingleString v) -> Some (n, v) | _ -> None)
            |> Map.ofSeq

        member _.requestQueryParam name context =
            context.Request.Query
            |> Seq.tryPickV (function KeyValue(n, SingleString v) when n = name -> ValueSome v | _ -> ValueNone)
            |> ValueOption.defaultValue null

        member _.requestHeader name context =
            match context.Request.Headers.TryGetValue name with
            | true, SingleString value -> value
            | _ -> null

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

        member _.ok html =
            htmlString html

        member _.ok html =
            setBody html

        member _.badRequest body =
            RequestErrors.BAD_REQUEST body