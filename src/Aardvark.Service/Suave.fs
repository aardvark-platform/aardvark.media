namespace Suave

open System
open System.Net

module Filters =
  
    let prefix (path : string) : WebPart =
        if not (path.StartsWith "/") then failwith "prefix-paths need to start with /"
        fun (ctx : HttpContext) ->
            async {
                let request : HttpRequest = ctx.request
                let url = request.url

                if url.AbsolutePath.StartsWith path then
                    let newUrl = new UriBuilder(url)
                    let newPath = url.AbsolutePath.Substring(path.Length)
                    let newPath =
                        if newPath.StartsWith "/" then newPath
                        else "/" + newPath

                    newUrl.Path <- newPath

                    let newHeaders = 
                        let mutable found = false
                        let newHeaders =
                            request.headers |> List.map (fun (name, value) ->
                                if name = "GLOBAL_PATH" then
                                    found <- true
                                    if value.EndsWith "/" then (name, value + path.Substring(1))
                                    else (name, value + path)
                                else
                                    (name, value)
                            )

                        if not found then
                            ("GLOBAL_PATH", path) :: request.headers
                        else
                            newHeaders




                    let r = { request with url = newUrl.Uri; headers = newHeaders }
                    let innerCtx = { ctx with request = r }
                    return Some innerCtx
                else
                    return None
            }
  

module Redirection =
    let foundRelative (path : string) : WebPart =
        if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"
        fun (ctx : HttpContext) ->
            match ctx.request.header "GLOBAL_PATH" with
                | Choice1Of2 p -> 
                    let newPath =
                        if p.EndsWith "/" then p + path.Substring(1)
                        else p + path
                    Redirection.redirect newPath ctx
                | _ ->
                    Redirection.redirect path ctx

    let redirectRelative (path : string) : WebPart =
        if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"
        fun (ctx : HttpContext) ->
            match ctx.request.header "GLOBAL_PATH" with
                | Choice1Of2 p -> 
                    let newPath =
                        if p.EndsWith "/" then p + path.Substring(1)
                        else p + path
                    Redirection.redirect newPath ctx
                | _ ->
                    Redirection.redirect path ctx

module WebPart =
    let runServer (port : int) (content : list<WebPart>) =
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }
        let index = choose content
        startWebServer config index

    let startServer (port : int) (content : list<WebPart>) =
        let config =
            { defaultConfig with
                bindings = [ HttpBinding.create HTTP IPAddress.Any (uint16 port) ] 
            }
        let index = choose content
        let (_,s) = startWebServerAsync config index
        Async.Start s

