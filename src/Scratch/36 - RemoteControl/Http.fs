/// HTTP helpers shared by the REST API and the MCP server.
module Http

open System
open Suave

/// Local servers must validate the Origin header to prevent cross-site requests and DNS rebinding attacks.
/// Requests without an Origin come from non-browser clients and are allowed.
let isAllowedOrigin (r : HttpRequest) =
    match r.header "origin" with
    | Choice1Of2 origin ->
        match Uri.TryCreate(origin, UriKind.Absolute) with
        | true, uri -> uri.IsLoopback
        | _ -> false
    | Choice2Of2 _ -> true

/// Answers 403 for requests from non-local origins, otherwise continues with the given WebPart.
let requireLocalOrigin (next : WebPart) : WebPart =
    request (fun r -> if isAllowedOrigin r then next else Response.response HTTP_403 [||])
