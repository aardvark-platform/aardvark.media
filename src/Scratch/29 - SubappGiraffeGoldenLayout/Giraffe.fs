namespace Test.Giraffe

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
        let errorHandler (ex : Exception) (logger : ILogger) =
            logger.LogError(EventId(), ex, "{[Server] An unhandled exception has occurred while executing the request.")
            clearResponse >=> setStatusCode 500 >=> text ex.Message

        let configureApp (app : IApplicationBuilder) =
            app.UseWebSockets()
                .UseGiraffeErrorHandler(errorHandler)
                .UseGiraffe webApp

        let configureServices (services : IServiceCollection) =
            services.AddGiraffe() |> ignore


        let configureLogging (builder : ILoggingBuilder) =
            let filter (l : LogLevel) = l.Equals LogLevel.Warning 
            builder.AddFilter(filter) 
                   .AddConsole()      
                   .AddDebug()      
            |> ignore

        //let contentRoot = Directory.GetCurrentDirectory()
        //let webRoot     = Path.Combine(contentRoot, "WebRoot")
        // .UseContentRoot(contentRoot)
        // .UseWebRoot(webRoot)
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