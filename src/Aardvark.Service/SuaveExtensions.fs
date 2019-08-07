namespace Suave

open System

[<AutoOpen>]
module UriExtensions =
    type Uri with
        member x.WithPath(path : string) =
            let path = 
                if path.StartsWith "/" then path
                else "/" + path

            let portSuffix = 
                if x.IsDefaultPort then ""
                else sprintf ":%d" x.Port

            Uri(sprintf "%s://%s%s%s%s" x.Scheme x.Host portSuffix path x.Query)
        

module Filters =
    let folder (name : string) (parts : list<WebPart>) : WebPart =
        let prefix =
            if name.StartsWith "/" then name
            else "/" + name

        fun (ctx : HttpContext) ->
            let url = ctx.request.url
            let path = url.AbsolutePath
            if path.StartsWith prefix then
                let rest = path.Substring prefix.Length
                if rest = "" || rest.StartsWith "/" then
                    let rest = if rest = "" then "/" else rest
                    let newUri = url.WithPath rest
                    choose parts { ctx with request = { ctx.request with rawPath = newUri.AbsolutePath } }
                else
                    never ctx
            else
                never ctx
  
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




                    let r = { request with rawPath = newUrl.Uri.AbsolutePath; headers = newHeaders }
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
            None

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
