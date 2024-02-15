namespace Aardvark.UI.Giraffe

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open System.Threading
open System.Net
open Giraffe

module Server =

    type WebApp = HttpFunc -> HttpContext -> HttpFuncResult

    let startServer' (url : string) (cancellationToken : CancellationToken) (content : WebApp list)  =
        let configureApp (app : IApplicationBuilder) =
            app.UseWebSockets().UseGiraffe (choose content)

        let configureServices (services : IServiceCollection) =
            services.AddGiraffe() |> ignore

        let configureLogging (builder : ILoggingBuilder) =
            let filter (l : LogLevel) = l.Equals LogLevel.Error
            builder.AddFilter(filter)
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
                          |> ignore)
             .Build()
             .StartAsync(cancellationToken)

    let startServer (url: string) (cancellationToken: CancellationToken) (webApp: WebApp) =
        startServer' url cancellationToken [webApp]

    let startServerLocalhost (port: int) (cancellationToken: CancellationToken) (content: WebApp list) =
        let url = sprintf "http://%A:%d" IPAddress.Loopback port
        startServer' url cancellationToken content