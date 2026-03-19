namespace Aardvark.Service.Suave

open System.Net
open System.Threading
open System.Threading.Tasks
open Suave
open Suave.CORS
open Suave.Operators

module Server =

    /// Starts the server and blocks the current thread.
    let run (port: int) (content: WebPart seq) : unit =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ]
            }

        let index = cors corsConfig >=> choose (List.ofSeq content)
        startWebServer config index

    /// Starts the server asynchronously.
    let start (port: int) (cancellationToken: CancellationToken) (content: WebPart seq) : Task =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ]
                cancellationToken = cancellationToken
            }

        let index = cors corsConfig >=> choose (List.ofSeq content)
        let listening, start = startWebServerAsync config index
        let task = Async.StartAsTask(start, cancellationToken = cancellationToken)

        // wait for the server to start listening
        listening |> Async.RunSynchronously |> ignore

        task

    /// Starts the server asynchronously on localhost (does not allow access from the network).
    let startLocalhost (port: int) (cancellationToken: CancellationToken) (content: WebPart seq) : Task =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Loopback (uint16 port) ]
                cancellationToken = cancellationToken
            }

        let index = cors corsConfig >=> choose (List.ofSeq content)
        let listening, start = startWebServerAsync config index
        let task = Async.StartAsTask(start, cancellationToken = cancellationToken)

        // wait for the server to start listening
        listening |> Async.RunSynchronously |> ignore

        task