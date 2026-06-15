namespace Aardvark.UI.Suave

open Suave
open Suave.Filters
open Suave.RequestErrors
open Suave.Sockets
open Suave.Sockets.Control
open System
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.UI

module internal Opcode =

    let ofWebSocketOpCode =
        LookupTable.lookup [
            WebSocketOpCode.Ping,   WebSocket.Opcode.Ping
            WebSocketOpCode.Pong,   WebSocket.Opcode.Pong
            WebSocketOpCode.Text,   WebSocket.Opcode.Text
            WebSocketOpCode.Binary, WebSocket.Opcode.Binary
            WebSocketOpCode.Close,  WebSocket.Opcode.Close
        ]

    let toWebSocketOpCode =
        LookupTable.lookup [
            WebSocket.Opcode.Ping,   WebSocketOpCode.Ping
            WebSocket.Opcode.Pong,   WebSocketOpCode.Pong
            WebSocket.Opcode.Text,   WebSocketOpCode.Text
            WebSocket.Opcode.Binary, WebSocketOpCode.Binary
            WebSocket.Opcode.Close,  WebSocketOpCode.Close
        ]

module internal SocketOp =

    let toTask (socketOp: Sockets.SocketOp<'T>) : Task<'T> =
        task {
            match! socketOp with
            | Choice1Of2 result ->
                return result

            | Choice2Of2 (Error.SocketError err) ->
                return raise <| SocketException(int err)

            | Choice2Of2 (Error.InputDataError (status, message)) ->
                let code = match status with Some status -> $" ({status})" | _ -> ""
                return raise <| ConnectionException($"Input data error: {message}{code}")

            | Choice2Of2 (Error.ConnectionError message) ->
                // See: https://github.com/SuaveIO/suave/blob/v2.5.6/src/Suave/Sockets/TcpTransport.fs
                if message.Contains "acceptArgs.AcceptSocket = null" then
                    return raise <| ConnectionException(ConnectionError.Lost)
                else
                    return raise <| ConnectionException(message)
        }

type internal WebSocket(socket: WebSocket.WebSocket) =

    member _.Send(message: WebSocketOpCode, data: byte[], endOfMessage: bool, cancellationToken: CancellationToken) : Task =
        if cancellationToken.IsCancellationRequested then
            Task.CompletedTask
        else
            let opcode = Opcode.ofWebSocketOpCode message
            let data = ByteSegment data
            SocketOp.toTask (socket.send opcode data endOfMessage)

    member _.Receive(buffer: SocketBuffer, cancellationToken: CancellationToken) =
        let rec read() =
            SocketMonad.socket {
                cancellationToken.ThrowIfCancellationRequested()

                let! opcode, data, endOfMessage = socket.read()
                buffer.Write(data)

                if endOfMessage then
                    return opcode
                else
                    let! _ = read()
                    return opcode
            }

        read()
        |> Async.map (Choice.map Opcode.toWebSocketOpCode)
        |> SocketOp.toTask

    interface IWebSocket with
        member _.Dispose() = ()
        member this.Send(message, data, endOfMessage, cancellationToken) = this.Send(message, data, endOfMessage, cancellationToken)
        member this.Receive(buffer, cancellationToken) = this.Receive(buffer, cancellationToken)

type HttpBackend private () =
    static let instance = HttpBackend()
    static member Instance : IHttpBackend<_, _> = instance

    interface IHttpBackend<HttpContext, WebPart> with
        member _.withContext handler =
            fun context ->
                let handler = handler context
                handler context

        member _.requestPath context =
            context.request.path

        member _.requestMethod context =
            context.request.rawMethod

        member _.requestQueryParams context =
            context.request.query
            |> List.choose (function n, Some v -> Some (n, v) | _ -> None)
            |> Map.ofList

        member _.requestQueryParam name context =
            context.request.query
            |> List.tryPickV (function n, Some v when n = name -> ValueSome v | _ -> ValueNone)
            |> ValueOption.defaultValue null

        member _.requestHeader name context =
            match context.request.header name with
            | Choice1Of2 value -> value
            | _ -> null

        member _.choose handlers =
            choose handlers

        member _.route path =
            Filters.path path

        member _.routef path handler =
            pathScan path handler

        member _.subRoute path handler =
            let prefix = Filters.prefix path
            compose prefix handler

        member _.compose h1 h2 =
            compose h1 h2

        member _.mimeType mimeType =
            Writers.setMimeType mimeType

        member _.redirectTo _ location =
            Redirection.redirect location

        member _.handShake continuation =
            WebSocket.handShake (fun socket context ->
                let socket = new WebSocket(socket)

                async {
                    try
                        do! continuation socket context
                        return Choice1Of2 ()

                    with
                    | exn when (exn.GetBaseException() :? OperationCanceledException) ->
                        return Choice1Of2 ()

                    | :? SocketException as exn ->
                        let error = Error.SocketError (enum<SocketError> exn.ErrorCode)
                        return Choice2Of2 error

                    | exn ->
                        let error = Error.ConnectionError exn.Message
                        return Choice2Of2 error
                }
            )

        member _.method httpMethod =
            method <| HttpMethod.OTHER httpMethod

        member _.header key value =
            Writers.setHeader key (string value)

        member _.ok html =
            Successful.OK html

        member _.ok html =
            Successful.ok html

        member _.badRequest body =
            BAD_REQUEST body