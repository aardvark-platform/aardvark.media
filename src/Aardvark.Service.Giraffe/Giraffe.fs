namespace Aardvark.UI.Giraffe

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open System.Threading
open System.Reflection
open System.Net
open Giraffe

type WebPart = HttpFunc -> HttpContext -> HttpFuncResult

module WebPart =
    open Aardvark.Service.Giraffe

    let ofAssembly (assembly: Assembly) : WebPart =
        Reflection.assemblyWebPart assembly

    let ofRouteHtml (route: string) (handler: (string -> string option) -> string) : WebPart =
        let handle (next: HttpFunc) (ctx: HttpContext) =
            let response = handler ctx.TryGetQueryStringValue
            htmlString response next ctx

        Giraffe.Routing.route route >=> handle

module Server =

    type WebApp = WebPart

    let createHost' (url : string) (content : WebApp list)  = 
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

    let createHost (url : string) (webApp : WebApp)  = 
        createHost' url [webApp]

    let startServer' (url : string) (cancellationToken : CancellationToken) (content : WebApp list)  =
        (createHost' url content)
             .Build()
             .StartAsync(cancellationToken)

    let startServer (url: string) (cancellationToken: CancellationToken) (webApp: WebApp) =
        startServer' url cancellationToken [webApp]

    let startServerLocalhost (port: int) (cancellationToken: CancellationToken) (content: WebApp list) =
        let url = sprintf "http://%A:%d" IPAddress.Loopback port
        startServer' url cancellationToken content