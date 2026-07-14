namespace Aardvark.UI.Giraffe

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System.Threading
open System.Threading.Tasks
open System.Net
open Giraffe

module Server =

    let createHost (url: string) (responseCompression: bool) (content: WebPart seq)  =
        let configureApp (app: IApplicationBuilder) =
            let app =
                if responseCompression then
                    app.UseResponseCompression()
                else
                    app

            app.UseWebSockets().UseGiraffe (choose <| List.ofSeq content)

        let configureServices (services: IServiceCollection) =
            services.AddGiraffe() |> ignore
            if responseCompression then services.AddResponseCompression() |> ignore

        let configureLogging (builder: ILoggingBuilder) =
            builder
                .AddFilter((=) LogLevel.Error)
                .AddConsole()
                .AddDebug()
                |> ignore

        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .Configure(configureApp)
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        .UseUrls(url)
                        |> ignore
                )

    /// <summary>
    /// Starts the web server and runs asynchronously until the server is fully shut down.
    /// </summary>
    /// <param name="url">The hosting URL and port configuration (e.g., http://0.0.0.0:5000).</param>
    /// <param name="cancellationToken">
    /// A lifetime token used to trigger a graceful shutdown. Cancelling this token forces
    /// the running server to stop accepting requests and wind down all internal services.
    /// </param>
    /// <param name="responseCompression">Enables or disables HTTP response compression.</param>
    /// <param name="content">A sequence of WebParts defining the HTTP routing and execution logic.</param>
    /// <returns>A task that completes when the server is shut down.</returns>
    let start (url: string) (cancellationToken: CancellationToken) (responseCompression: bool) (content: WebPart seq) : Task =
        let host = createHost url responseCompression content |> _.Build()
        host.StartAsync(cancellationToken).GetAwaiter().GetResult()
        host.WaitForShutdownAsync(cancellationToken)

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
        start url cancellationToken false content