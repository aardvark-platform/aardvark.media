namespace Suave

open System
open System.Net
open System.Threading
open System.Reactive.Disposables
open Suave.CORS
open Suave.Operators

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module WebPart =

    let runServer (port : int) (content : list<WebPart>) : IDisposable =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let cts = new CancellationTokenSource()
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
                cancellationToken = cts.Token
            }
        let index = cors corsConfig >=> choose content
        startWebServer config index

        Disposable.Create(fun () -> cts.Cancel())

    let startServer (port : int) (content : list<WebPart>) : IDisposable =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let cts = new CancellationTokenSource()
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
                cancellationToken = cts.Token
            }
        let index = cors corsConfig >=> choose content
        let (_,s) = startWebServerAsync config index
        Async.Start s


        Disposable.Create(fun () -> cts.Cancel())

    let startServerLocalhost (port : int) (content : list<WebPart>):  IDisposable =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let cts = new CancellationTokenSource()
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Loopback (uint16 port) ] 
                cancellationToken = cts.Token
            }
        let index = cors corsConfig >=> choose content
        let (_,s) = startWebServerAsync config index
        Async.Start s

        Disposable.Create(fun () -> cts.Cancel())