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

    let start (url: string) (cancellationToken: CancellationToken) (responseCompression: bool) (content: WebPart seq) : Task =
        let host = createHost url responseCompression content
        host.Build().StartAsync(cancellationToken)

    let startLocalhost (port: int) (cancellationToken: CancellationToken) (content: WebPart seq) : Task =
        let url = $"http://{IPAddress.Loopback}:{port}"
        start url cancellationToken false content