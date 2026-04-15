namespace Aardvark.UI

open System
open System.Net.Sockets
open System.Net.WebSockets
open Aardvark.Base

type ConnectionError =
    | Closed   = 0
    | Canceled = 1
    | Lost     = 2
    | Unknown  = 3

type ConnectionException =
    inherit Exception
    val Error : ConnectionError

    static let getErrorMessage =
        LookupTable.tryLookupV [
            ConnectionError.Closed,   "Connection has been closed"
            ConnectionError.Canceled, "Operation has been canceled"
            ConnectionError.Lost,     "Connection has been lost"
        ]
        >> ValueOption.defaultValue "An unknown connection error occurred"

    static let getBaseException (exn: exn) =
        if notNull exn then exn.GetBaseException() else null

    new () =
        ConnectionException(ConnectionError.Unknown)

    new (innerException: exn) =
        ConnectionException(null, innerException)

    new (message: string) =
        ConnectionException(ConnectionError.Unknown, message)

    new (message: string, innerException: exn) =
        ConnectionException(ConnectionError.Unknown, message, innerException)

    new (error: ConnectionError) =
        ConnectionException(error, getErrorMessage error, null)

    new (error: ConnectionError, message: string) =
        ConnectionException(error, message, null)

    new (error: ConnectionError, message: string, innerException: exn) =
        let message = if String.IsNullOrEmpty message then getErrorMessage error else message
        { inherit Exception($"{message} (Error: {error})", innerException); Error = error }

module ConnectionError =

    let ofSocketError (error: SocketError) =
        match error with
        | SocketError.Shutdown            -> ConnectionError.Closed
        | SocketError.Disconnecting       -> ConnectionError.Closed
        | SocketError.Interrupted         -> ConnectionError.Closed
        | SocketError.OperationAborted    -> ConnectionError.Closed
        | SocketError.ConnectionReset     -> ConnectionError.Lost
        | SocketError.ConnectionAborted   -> ConnectionError.Lost
        | SocketError.HostDown            -> ConnectionError.Lost
        | SocketError.HostNotFound        -> ConnectionError.Lost
        | SocketError.HostUnreachable     -> ConnectionError.Lost
        | SocketError.NetworkDown         -> ConnectionError.Lost
        | SocketError.NetworkReset        -> ConnectionError.Lost
        | SocketError.NetworkUnreachable  -> ConnectionError.Lost
        | _                               -> ConnectionError.Unknown

    let ofWebSocketError (error: WebSocketError) =
        match error with
        | WebSocketError.InvalidState                -> ConnectionError.Closed
        | WebSocketError.ConnectionClosedPrematurely -> ConnectionError.Lost
        | _                                          -> ConnectionError.Unknown

    let ofException (exn: exn) =
        if notNull exn then
            match exn.GetBaseException() with
            | :? OperationCanceledException -> ConnectionError.Canceled
            | :? SocketException as exn     -> ofSocketError exn.SocketErrorCode
            | :? WebSocketException as exn  -> ofWebSocketError exn.WebSocketErrorCode
            | :? ConnectionException as exn -> exn.Error
            | _                             -> ConnectionError.Unknown
        else
            ConnectionError.Unknown