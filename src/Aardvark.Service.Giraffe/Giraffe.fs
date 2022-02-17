namespace Aardvark.UI.Giraffe

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe


open Microsoft.AspNetCore.Http
open Microsoft.FSharp.Control
open System.Threading

module Server =

    type WebApp = HttpFunc -> HttpContext -> HttpFuncResult

    let startServer (url : string) (cancellationToken : CancellationToken) (webApp : WebApp)  =

        let configureApp (app : IApplicationBuilder) =
            app.UseWebSockets().UseGiraffe webApp

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