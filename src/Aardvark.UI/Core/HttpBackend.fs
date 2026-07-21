namespace Aardvark.UI

open Aardvark.Base
open System
open System.IO
open System.Reflection
open System.Text
open System.Threading.Tasks

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
    type IHttpRequest with
        member this.BodyData =
            task {
                let ms = new MemoryStream()
                do! this.Body.CopyToAsync ms // ASP.NET Core disallows synchronous IO by default
                return ms.ToArray()
            }

        member this.BodyUtf8 =
            task {
                let! data = this.BodyData
                return Encoding.UTF8.GetString data
            }

    type IHttpBackend<'HttpContext, 'HttpHandler> with
        member this.ok (data: byte[])            = this.compose (this.status 200) (this.response data)
        member this.ok (data: string)            = this.compose (this.status 200) (this.response data)
        member this.badRequest (data: byte[])    = this.compose (this.status 400) (this.response data)
        member this.badRequest (data: string)    = this.compose (this.status 400) (this.response data)
        member this.notFound (data: byte[])      = this.compose (this.status 404) (this.response data)
        member this.notFound (data: string)      = this.compose (this.status 404) (this.response data)
        member this.internalError (data: byte[]) = this.compose (this.status 500) (this.response data)
        member this.internalError (data: string) = this.compose (this.status 500) (this.response data)

        member this.text (data: byte[]) = this.compose (this.mimeType "text/plain") (this.ok data)
        member this.text (data: string) = this.compose (this.mimeType "text/plain") (this.ok data)

        member this.html (data: byte[]) = this.compose (this.mimeType "text/html") (this.ok data)
        member this.html (data: string) = this.compose (this.mimeType "text/html") (this.ok data)

        member this.jsonRaw (data: byte[]) = this.compose (this.mimeType "application/json") (this.ok data)
        member this.jsonRaw (data: string) = this.compose (this.mimeType "application/json") (this.ok data)
        member this.json (data: 'T)        = this.jsonRaw (Pickler.jsonToString data)

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

        /// <summary>
        /// Serves the embedded resources of an assembly according to the given chooser function.
        /// </summary>
        /// <param name="chooser">A function that maps a manifest resource name to an <c>HttpResource option</c>; returns <c>None</c> if the resource is not to be served.</param>
        /// <param name="assembly">The assembly containing the embedded resources to serve.</param>
        member this.assemblyWith (chooser: string -> HttpResource option) (assembly: Assembly) =
            let (>=>) a b = this.compose a b

            assembly.GetManifestResourceNames()
            |> Array.toList
            |> List.choose (fun resourceName ->
                match chooser resourceName with
                | Some resource -> Some (resourceName, resource)
                | _ -> None
            )
            |> List.collect (fun (resourceName, resource) ->
                use stream = assembly.GetManifestResourceStream resourceName
                let data = Stream.readAllBytes stream
                let name = resource.Path |> String.replace "\\" "/"
                Report.Line(2, "{0} serves {1}", assembly.GetName().Name, name)

                // set the mime-type (if known)
                let part =
                    if String.IsNullOrEmpty resource.MimeType then
                        this.ok data
                    else
                        this.mimeType resource.MimeType >=> this.ok data

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

        /// <summary>
        /// Serves the embedded resources of an assembly.
        /// The MIME type is determined from the file extension of the resource name.
        /// </summary>
        /// <remarks>
        /// For .NET Framework assemblies all manifest resources are served using the resource name as path as-is.
        /// Otherwise, the path is determined as follows:
        /// <list type="bullet">
        /// <item>AssemblyName.resources.name -&gt; resources/name</item>
        /// <item>AssemblyName.name -&gt; name</item>
        /// <item>resources/name -&gt; resources/name</item>
        /// <item>resources\name -&gt; resources/name</item>
        /// </list>
        /// The rules are case-insensitive; if no rule applies, the resource is ignored.
        /// </remarks>
        /// <param name="assembly">The assembly containing the embedded resources to serve.</param>
        member this.assembly (assembly: Assembly) =
            assembly |> this.assemblyWith (HttpResource.ofAssemblyResource assembly)

        member this.bindBody (mapping: byte[] -> 'HttpHandler) =
            this.request (fun r ->
                this.async <| task {
                    let! body = r.BodyData
                    return mapping body
                }
            )

        member this.bindBody (mapping: string -> 'HttpHandler) =
            this.bindBody (Encoding.UTF8.GetString >> mapping)

        member this.bindJson (mapping: 'T -> 'HttpHandler) =
            this.bindBody (Pickler.json.UnPickle >> mapping)

        member this.mapJson (mapping: 'T1 -> 'T2) =
            this.bindJson (mapping >> this.json)