namespace Suave

open System
open System.Net
open System.Threading
open Suave.CORS
open Suave.Operators

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module WebPart =

    /// runs the server blocking / cannot be canceled
    let runServer (port : int) (content : list<WebPart>) =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }
        let index = cors corsConfig >=> choose content
        startWebServer config index

    /// starts the server asynchronously and returns Disposable to stop the server again
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

        { new IDisposable with member x.Dispose() = cts.Cancel() }

    /// starts the server on localhost (does not allow access from the network) asynchronously 
    /// and returns Disposable to stop the server again
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

        { new IDisposable with member x.Dispose() = cts.Cancel() }