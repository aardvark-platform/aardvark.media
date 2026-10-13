namespace Aardvark.UI.Tests

open System
open System.Net
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.UI

[<AutoOpen>]
module Utilities =

    type Task with
        member this.WaitIgnoreCancel() =
            try this.GetAwaiter().GetResult()
            with
            | :? OperationCanceledException -> ()
            | _ -> reraise()

[<AbstractClass>]
type TestServer(name: string, start: int -> CancellationToken -> Task) =
    let mutable port = 0
    let mutable tcs : CancellationTokenSource = null
    let mutable server = Task.CompletedTask
    let mutable refCount = 0

    member _.Name = name
    member this.Host = lock this (fun _ -> $"localhost:{port}")
    member this.CancellationToken = lock this (fun _ -> tcs.Token)
    member this.Acquire() =
        lock this (fun _ ->
            inc &refCount
            if refCount = 1 then
                port <- Server.getFreeTcpPort IPAddress.Loopback
                tcs <- new CancellationTokenSource()
                server <- start port tcs.Token
        )

        let release() =
            lock this (fun _ ->
                dec &refCount
                if refCount = 0 then
                    tcs.Cancel()
                    server.WaitIgnoreCancel()
                    tcs.Dispose()
                    port <- 0
                    tcs <- null
                    server <- Task.CompletedTask
            )

        { new IDisposable with
            member _.Dispose() = release() }

type GiraffeTestServer(content : CancellationToken -> Giraffe.WebPart list) =
    inherit TestServer("Giraffe", fun p t -> Giraffe.Server.startLocalhost p t (content t))

type SuaveTestServer(content : CancellationToken -> Suave.Http.WebPart list) =
    inherit TestServer("Suave", fun p t -> Suave.Server.startLocalhost p t (content t))