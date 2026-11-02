namespace Aardvark.UI.Suave

open Suave
open System

module Filters =

    [<Obsolete>]
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
        fun (ctx : HttpContext) ->
            async {
                let request : HttpRequest = ctx.request

                if request.rawPath.StartsWith path then
                    let newPath = request.rawPath.[path.Length ..]

                    let newHeaders =
                        let mutable found = false
                        let newHeaders =
                            request.headers |> List.map (fun (name, value as header) ->
                                if String.Equals(name, "GLOBAL_PATH", StringComparison.OrdinalIgnoreCase) then
                                    found <- true
                                    name, value + path
                                else
                                    header
                            )

                        if not found then
                            ("GLOBAL_PATH", path) :: request.headers
                        else
                            newHeaders

                    return Some { ctx with request.rawPath = newPath; request.headers = newHeaders }
                else
                    return None
            }