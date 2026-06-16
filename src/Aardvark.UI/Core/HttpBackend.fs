namespace Aardvark.UI

open Aardvark.Base
open System.IO
open System.Threading.Tasks
open System.Reflection

module HttpMethod =
    let [<Literal>] Connect = "CONNECT"
    let [<Literal>] Delete  = "DELETE"
    let [<Literal>] Get     = "GET"
    let [<Literal>] Head    = "HEAD"
    let [<Literal>] Options = "OPTIONS"
    let [<Literal>] Patch   = "PATCH"
    let [<Literal>] Post    = "POST"
    let [<Literal>] Put     = "PUT"
    let [<Literal>] Query   = "QUERY"
    let [<Literal>] Trace   = "TRACE"

type IHttpRequest =
    abstract member Path        : string
    abstract member Body        : Stream
    abstract member Method      : string
    abstract member Header      : name: string -> string option
    abstract member Headers     : Map<string, string>
    abstract member QueryParam  : name: string -> string option
    abstract member QueryParams : Map<string, string list>

/// Minimal interface for HTTP backends to define web parts in a library agnostic way.
type IHttpBackend<'HttpContext, 'HttpHandler> =
    abstract member withContext : handler: ('HttpContext -> 'HttpHandler) -> 'HttpHandler
    abstract member getRequest  : context: 'HttpContext -> IHttpRequest
    abstract member async       : handler: Task<'HttpHandler> -> 'HttpHandler

    abstract member choose     : handlers: 'HttpHandler list -> 'HttpHandler
    abstract member route      : path: string -> 'HttpHandler
    abstract member routef     : path: PrintfFormat<_, _, _, _, 'T> -> handler: ('T -> 'HttpHandler) -> 'HttpHandler
    abstract member subRoute   : path: string -> handler: 'HttpHandler -> 'HttpHandler
    abstract member compose    : h1: 'HttpHandler -> h2: 'HttpHandler -> 'HttpHandler
    abstract member mimeType   : mimeType: string -> 'HttpHandler
    abstract member redirectTo : permanent: bool -> location: string -> 'HttpHandler
    abstract member handShake  : continuation: (IWebSocket -> 'HttpContext -> Task) -> 'HttpHandler
    abstract member method     : httpMethod: string -> 'HttpHandler
    abstract member header     : key: string -> value: obj -> 'HttpHandler
    abstract member status     : status: int -> 'HttpHandler
    abstract member response   : data: string -> 'HttpHandler
    abstract member response   : data: byte[] -> 'HttpHandler
    abstract member sendFile   : filePath: string -> 'HttpHandler

[<AutoOpen>]
module ``IHttpBackend Extensions`` =

    let private mimeTypes =
        Dictionary.ofList [
            ".js",    "text/javascript"
            ".mjs",   "text/javascript"
            ".css",   "text/css"
            ".svg",   "image/svg+xml"
            ".woff",  "application/x-font-woff"
            ".woff2", "font/woff2"
            ".ttf",   "application/octet-stream"
            ".eot",   "application/vnd.ms-fontobject"
        ]

    let private (|LocalResourceName|_|) (ass : Assembly) (n : string) =
        let myNamespace = ass.GetName().Name + "."
        let myNamespaceResources = myNamespace + "resources."

        match n with
        | n when n.StartsWith myNamespaceResources ->
            let name = n.Substring myNamespaceResources.Length
            Some ("resources/" + name) // resources/name.min.js
        | n when n.StartsWith myNamespace ->
            let name = n.Substring myNamespace.Length
            Some name   // resources/name.min.js
        | n when n.StartsWith "resources" ->
            Some n // fallback for logicalName to prevent resource name mangling (https://github.com/aardvark-platform/aardvark.media/issues/35)
        | _ ->
            None


    let private isNetFramework (assembly : Assembly) =
        let attributeValue = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
        attributeValue.FrameworkName.ToLower().Contains("framework")

    let private ignoredResources =
        Set.ofList [
            "FSharpSignatureData"
            "FSharpOptimizationData"
        ]

    let private (|PlainFrameworkEmbedding|_|) (assembly : Assembly) (resName : string) =
        if assembly |> isNetFramework then
            let assemblyName = assembly.GetName().Name
            if ignoredResources |> Set.map (fun r -> $"{r}.{assemblyName}") |> Set.contains resName then
                None
            else
                Some resName
        else None

    type IHttpBackend<'HttpContext, 'HttpHandler> with
        member this.ok (data: byte[])         = this.compose (this.status 200) (this.response data)
        member this.ok (data: string)         = this.compose (this.status 200) (this.response data)
        member this.badRequest (data: byte[]) = this.compose (this.status 400) (this.response data)
        member this.badRequest (data: string) = this.compose (this.status 400) (this.response data)
        member this.notFound (data: byte[])   = this.compose (this.status 404) (this.response data)
        member this.notFound (data: string)   = this.compose (this.status 404) (this.response data)

        member this.text (data: byte[]) = this.compose (this.mimeType "text/plain") (this.ok data)
        member this.text (data: string) = this.compose (this.mimeType "text/plain") (this.ok data)

        member this.html (data: byte[]) = this.compose (this.mimeType "text/html") (this.ok data)
        member this.html (data: string) = this.compose (this.mimeType "text/html") (this.ok data)

        member this.json (data: byte[]) = this.compose (this.mimeType "text/json") (this.ok data)
        member this.json (data: string) = this.compose (this.mimeType "text/json") (this.ok data)

        member this.request (handler: IHttpRequest -> 'HttpHandler) =
            this.withContext (this.getRequest >> handler)

        member this.redirectRelative (path : string) : 'HttpHandler =
            if not (path.StartsWith "/") then failwith "redirect-paths need to start with /"

            this.request (fun r ->
                let newPath =
                    match r.Header "GLOBAL_PATH" with
                    | Some globalPath ->
                        if globalPath.EndsWith "/" then globalPath + path.Substring(1)
                        else globalPath + path
                    | _ ->
                        path

                this.redirectTo true newPath
            )

        member this.assembly (assembly: Assembly) =
            let (>=>) a b = this.compose a b

            assembly.GetManifestResourceNames()
            |> Array.toList
            |> List.choose (fun resName ->
                match resName with
                | PlainFrameworkEmbedding assembly n ->
                    Some (resName, n)
                | LocalResourceName assembly n ->
                    Some (resName, n)
                | _ ->
                    None
            )
            |> List.collect (fun (resName, name) ->
                use stream = assembly.GetManifestResourceStream resName
                let data = Stream.readAllBytes stream
                let name = name |> String.replace "\\" "/"

                let ext = Path.GetExtension name

                Report.Line(2, "{0} serves {1}", assembly.GetName().Name, name)

                // set the mime-type (if known)
                let part =
                    match mimeTypes.TryGetValue ext with
                    | true, mime -> this.mimeType mime >=> this.ok data
                    | _ -> this.ok data

                // index.* is also reachable via /
                let parts =
                    if Path.GetFileNameWithoutExtension name = "index" then
                        [
                            this.route ("/" + name) >=> part
                            this.route "/" >=> this.redirectRelative ("/" + name)
                        ]
                    else
                        [ this.route ("/" + name) >=> part ]

                // return the part
                parts
            )
            |> this.choose