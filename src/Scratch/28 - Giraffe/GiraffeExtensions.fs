namespace Aardvark.Service.Giraffe

open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

//module Redirection =
//    let foundRelative (path : string) : WebPart =
//        if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"
//        fun (ctx : HttpContext) ->
//            match ctx.request.header "GLOBAL_PATH" with
//                | Choice1Of2 p -> 
//                    let newPath =
//                        if p.EndsWith "/" then p + path.Substring(1)
//                        else p + path
//                    Redirection.redirect newPath ctx
//                | _ ->
//                    Redirection.redirect path ctx

//    let redirectRelative (path : string) : WebPart =
//        if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"
//        fun (ctx : HttpContext) ->
//            match ctx.request.header "GLOBAL_PATH" with
//                | Choice1Of2 p -> 
//                    let newPath =
//                        if p.EndsWith "/" then p + path.Substring(1)
//                        else p + path
//                    Redirection.redirect newPath ctx
//                | _ ->
//                    Redirection.redirect path ctx


module Reflection =
    open Aardvark.Base
    open System.Reflection
    open System.IO

    open Giraffe

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
        
    let private (|LocalResourceName|_|) (ass : Assembly) (n : string) =
        let myNamespace = ass.GetName().Name + "."
        if n.StartsWith myNamespace then 
            let name = n.Substring(myNamespace.Length)
            let arr = name.Split('.')
            if arr.Length > 1 then
                Some (String.concat "." arr.[arr.Length - 2 .. ])
            else
                Some name

        else
            // fallback for logicalName to prevent resource name mangling (https://github.com/aardvark-platform/aardvark.media/issues/35)
            if n.StartsWith "resources" then Some n 
            else None

    let private isNetFramework (assembly : Assembly) =
        let attributeValue = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
        attributeValue.FrameworkName.ToLower().Contains("framework")

    let (|PlainFrameworkEmbedding|_|) (assembly : Assembly) (resName : string) =
        if assembly |> isNetFramework then Some resName
        else None

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
                            //route "/" >=> redirectTo true Redirection.redirectRelative ("/" + name)
                        ]
                    else
                        [ route ("/" + name) >=> part ]
                
                // return the part
                parts
            )
            |> choose
