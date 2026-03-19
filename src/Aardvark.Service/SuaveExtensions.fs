namespace Aardvark.Service.Suave

open Suave

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