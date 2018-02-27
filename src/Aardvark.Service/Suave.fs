namespace Suave

open System
open System.Net
open Suave.CORS
open Suave.Filters
open Suave.Operators

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module WebPart =
    let runServer (port : int) (content : list<WebPart>) =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }
        let index = cors corsConfig >=> choose content
        startWebServer config index

    let startServer (port : int) (content : list<WebPart>) =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }
        let index = cors corsConfig >=> choose content
        let (_,s) = startWebServerAsync config index
        Async.Start s

    let startServerLocalhost (port : int) (content : list<WebPart>) =
        let corsConfig = { defaultCORSConfig with allowedUris = InclusiveOption.All }

        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Loopback (uint16 port) ] 
            }
        let index = cors corsConfig >=> choose content
        let (_,s) = startWebServerAsync config index
        Async.Start s