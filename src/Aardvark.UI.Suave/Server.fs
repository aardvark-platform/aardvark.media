namespace Aardvark.UI.Suave

open System
open System.Net
open System.Threading
open System.Threading.Tasks
open Suave
open Suave.CORS
open Suave.Operators

module Server =

    module private HttpBinding =
        let parse (url: string) =
            let url =
                match Uri.TryCreate(url.Replace("*", "[::]").Replace("+", "[::]"), UriKind.Absolute) with
                | true, uri -> uri
                | _ -> raise <| FormatException($"Invalid URL: {url}")

            let protocol =
                match url.Scheme.Trim().ToLowerInvariant() with
                | "http" -> HTTP
                | p -> raise <| NotSupportedException($"Protocol '{p}' not supported.")

            let address =
                match url.Host.Trim().ToLowerInvariant() with
                | "localhost" -> IPAddress.Loopback
                | host ->
                    let address =
                        if host.StartsWith "[" && host.EndsWith "]" then host.Substring(1, host.Length - 2)
                        else host

                    match IPAddress.TryParse address with
                    | true, address -> address
                    | _ -> raise <| FormatException($"Invalid IP address: {address}")

            HttpBinding.create protocol address (uint16 url.Port)

    /// <summary>
    /// Starts the web server and runs asynchronously until the server is fully shut down.
    /// </summary>
    /// <param name="url">The hosting URL and port configuration (e.g., http://0.0.0.0:5000).</param>
    /// <param name="cancellationToken">
    /// A lifetime token used to trigger a graceful shutdown. Cancelling this token forces 
    /// the running server to stop accepting requests and wind down all internal services.
    /// </param>
    /// <param name="content">A sequence of WebParts defining the HTTP routing and execution logic.</param>
    /// <returns>A task that completes when the server is shut down.</returns>
    let start (url: string) (cancellationToken: CancellationToken) (content: WebPart seq) : Task =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.parse url ]
                cancellationToken = cancellationToken
            }

        let index = cors corsConfig >=> choose (List.ofSeq content)
        let listening, start = startWebServerAsync config index
        let task = Async.StartAsTask(start, cancellationToken = cancellationToken)

        // wait for the server to start listening
        listening |> Async.RunSynchronously |> ignore

        task

    /// <summary>
    /// Starts the web server locally on the specified port and runs asynchronously until the server is fully shut down.
    /// </summary>
    /// <param name="port">The local port number to bind the server to (e.g., 8080).</param>
    /// <param name="cancellationToken">
    /// A lifetime token used to trigger a graceful shutdown. Cancelling this token forces
    /// the running server to stop accepting requests and wind down all internal services.
    /// </param>
    /// <param name="content">A sequence of WebParts defining the HTTP routing and execution logic.</param>
    /// <returns>A task that completes when the server is shut down.</returns>
    let startLocalhost (port: int) (cancellationToken: CancellationToken) (content: WebPart seq) : Task =
        let url = $"http://{IPAddress.Loopback}:{port}"
        start url cancellationToken content