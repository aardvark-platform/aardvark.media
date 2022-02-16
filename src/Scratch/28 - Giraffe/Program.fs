open System
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardium
open Inc

open Aardvark.UI.Giraffe





[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()
    
    use app = new OpenGlApplication()
    let instance = App.app |> App.start

    let webApp = MutableApp.toWebPart app.Runtime instance

    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp
    
    let configureServices (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe() |> ignore
    

    let configureLogging (builder : ILoggingBuilder) =
        // Set a logging filter (optional)
        let filter (l : LogLevel) = l.Equals LogLevel.Error

        // Configure the logging factory
        builder.AddFilter(filter) // Optional filter
               .AddConsole()      // Set up the Console logger
               .AddDebug()        // Set up the Debug logger

               // Add additional loggers if wanted...
        |> ignore

    Host.CreateDefaultBuilder()
         .ConfigureWebHostDefaults(
             fun webHostBuilder ->
                    webHostBuilder
                      .Configure(configureApp)
                      .ConfigureServices(configureServices)
                      .ConfigureLogging(configureLogging)
                      .UseUrls("http://localhost:4321")
                      |> ignore)
         .Build()
         .Run()

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }

    0 
