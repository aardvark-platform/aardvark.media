namespace Aardvark.Service.Giraffe

open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open System.Net.WebSockets
open System.Threading.Tasks

open Microsoft.AspNetCore.Http
open Giraffe


[<AutoOpen>]
module AspNetExtensions = 

    open Microsoft.Extensions.Primitives
    
    let (|SingleString|_|) (v : StringValues) =
        if v.Count = 1 then Some (v.Item 0) else None

module Redirection =

    let redirectRelative (path : string) (next : HttpFunc) =
        if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"
        fun (ctx : HttpContext) ->
            match ctx.Request.Headers.TryGetValue "GLOBAL_PATH" with
                | (true, SingleString p) -> 
                    let newPath =
                        if p.EndsWith "/" then p + path.Substring(1)
                        else p + path
                    Giraffe.Core.redirectTo true newPath next ctx
                | _ ->
                    Giraffe.Core.redirectTo true path next ctx


module Reflection =
    open Aardvark.Base
    open System.Reflection
    open System.IO

    open Giraffe

    open Aardvark.Service.Resources

    let private mimeTypes =
        Dictionary.ofList [
            ".js", "text/javascript"
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

                let buffer = Array.zeroCreate (int stream.Length)
                
                let mutable remaining = buffer.Length
                let mutable read = 0
                while remaining > 0 do
                    let s = stream.Read(buffer, read, remaining)
                    read <- read + s
                    remaining <- remaining - s

                let ext = Path.GetExtension name
        
                Report.Line(2, "{0} serves {1}", assembly.GetName().Name, name)
               

                let part (next : HttpFunc) (ctx : HttpContext) =
                   task {
                       match mimeTypes.TryGetValue ext with
                           | (true, mime) -> ctx.SetContentType(mime)
                           | _ -> () 
                       return! Giraffe.Core.setBody buffer next ctx
                   }

                // index.* is also reachable via /
                let parts =
                    if Path.GetFileNameWithoutExtension name = "index" then
                        [
                            route ("/" + name) >=> part
                            //route "/" >=> Redirection.redirectRelative ("/" + name)
                        ]
                    else
                        [ route ("/" + name) >=> part ]
                
                // return the part
                parts
            )
            |> choose


module Websockets =

    
    let handShake (f : WebSocket -> HttpContext -> Task<unit>)  (next : HttpFunc) (context : HttpContext) =
        task {
            match context.WebSockets.IsWebSocketRequest with
            | true -> 
                let! webSocket = context.WebSockets.AcceptWebSocketAsync()
                let! _ = f webSocket context
                return! next context
            | _ -> 
                return failwith "no ws request"
        }

