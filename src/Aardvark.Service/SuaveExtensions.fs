﻿namespace Suave

module Filters =
    let folder (name : string) (parts : list<WebPart>) : WebPart =
        let prefix =
            if name.StartsWith "/" then name
            else "/" + name

        fun (ctx : HttpContext) ->
            let path = ctx.request.rawPath
            if path.StartsWith prefix then
                let rest = path.Substring prefix.Length
                if rest = "" || rest.StartsWith "/" then
                    let rest = if rest = "" then "/" else rest
                    choose parts { ctx with request = { ctx.request with rawPath = rest } }
                else
                    never ctx
            else
                never ctx
  
    let prefix (path : string) : WebPart =
        if not (path.StartsWith "/") then failwith "prefix-paths need to start with /"
        fun (ctx : HttpContext) ->
            async {
                let request : HttpRequest = ctx.request
                
                if request.rawPath.StartsWith path then
                    let newPath = request.rawPath.Substring(path.Length)
                    let newPath =
                        if newPath.StartsWith "/" then newPath
                        else "/" + newPath
                        
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

                    let r = { request with rawPath = newPath; headers = newHeaders }
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


module Reflection =
    open Aardvark.Base
    open System.Reflection
    open System.IO
    open Suave.Successful
    open Suave.Filters
    open Suave.Redirection
    open Suave
    open Suave.Operators

    open Aardvark.Service.Resources

    let private mimeTypes =
        Dictionary.ofList [
            ".js", "text/javascript"
            ".mjs", "text/javascript"
            ".css", "text/css"
            ".svg", "image/svg+xml"
            ".woff", "application/x-font-woff"
            ".woff2", "font/woff2"
            ".ttf","application/octet-stream" 
            ".eot","application/vnd.ms-fontobject"
        ]
       

    let assemblyWebPart (assembly : Assembly) = 
        assembly.GetManifestResourceNames()
            |> Array.toList
            |> List.choose (fun resName -> 
                match resName with 
                    | PlainFrameworkEmbedding assembly n -> 
                        Some(resName, n)
                    | LocalResourceName assembly n -> 
                        Some(resName, n) 
                    | _ -> 
                        None
            )
            |> List.collect (fun (resName, name) ->
                use stream = assembly.GetManifestResourceStream resName
                let name = name |> String.replace "\\" "/"

                let buffer = Array.zeroCreate (int stream.Length)
                
                let mutable remaining = buffer.Length
                let mutable read = 0
                while remaining > 0 do
                    let s = stream.Read(buffer, read, remaining)
                    read <- read + s
                    remaining <- remaining - s

                let ext = Path.GetExtension name
        
                Report.Line(2, "{0} serves {1}", assembly.GetName().Name, name)
               
                let part = ok  buffer

                // set the mime-type (if known)
                let part = 
                    match mimeTypes.TryGetValue ext with
                        | (true, mime) -> part >=> Writers.setMimeType mime
                        | _ -> part 

                // index.* is also reachable via /
                let parts =
                    if Path.GetFileNameWithoutExtension name = "index" then
                        [
                            path ("/" + name) >=> part
                            path "/" >=> Redirection.redirectRelative ("/" + name)
                        ]
                    else
                        [ path ("/" + name) >=> part ]
                
                // return the part
                parts
            )
            |> choose



module Extensions =

    open Suave.WebSocket
    open Suave.Sockets.Control

    type WebSocket with
        member x.readMessage() =
            socket {
                let! (t,d,fin) = x.read()
                if fin then 
                    return (t,d)
                else
                    let! (_, rest) = x.readMessage()
                    return (t, Array.append d rest)
            }