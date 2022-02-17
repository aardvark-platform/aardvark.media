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
