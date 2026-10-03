/// Minimal Model Context Protocol (MCP) server on top of Api, served by the same Suave server as the UI at /mcp.
///
/// Implements the stateless subset of the "Streamable HTTP" transport: every JSON-RPC message is POSTed to /mcp and
/// requests are answered with a single application/json response (no sessions, no server-initiated SSE streams).
/// That is all a tools-only server needs. Hand-rolled instead of using the official C# SDK, because the SDK requires
/// System.Text.Json >= 10 while the libraries of this repository pin System.Text.Json 8.
///
/// Connect Claude Code with:  claude mcp add --transport http aardvark-boxes http://localhost:4321/mcp
module Mcp

open System
open System.Text.Json

open Suave
open Suave.Filters
open Suave.Operators

open Api

let private supportedProtocolVersions = [ "2025-11-25"; "2025-06-18"; "2025-03-26" ]

let private serverInfo = {| name = "aardvark-boxes"; title = "Aardvark Box Demo"; version = "1.0.0" |}

let private instructions =
    "Controls a running Aardvark.Media 3D demo that shows a row of boxes. Every tool returns the resulting scene " +
    "(all boxes with 1-based index, id, color and selection state), so there is no need to call get_scene after a change. " +
    "Boxes are referenced by 1-based index (\"3\"), range (\"1-5\"), id, \"selected\" or \"all\". " +
    "Selected boxes are highlighted in red and hovered boxes in blue, which hides their own color while selected."

let private options =
    JsonSerializerOptions(Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

// ---------------------------------------------------------------------------------------------------------------------
// Tools

type private Tool =
    {
        name        : string
        description : string
        inputSchema : JsonElement
        run         : RemoteApp -> JsonElement -> Result<SceneDto, string>
    }

module private Args =

    let tryGet (args : JsonElement) (name : string) =
        if args.ValueKind = JsonValueKind.Object then
            match args.TryGetProperty name with
            | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
            | _ -> None
        else
            None

    let optString (args : JsonElement) (name : string) =
        tryGet args name |> Option.map (fun v -> v.ToString())

    let string (args : JsonElement) (name : string) =
        match optString args name with
        | Some s -> Ok s
        | None -> Error $"Missing argument '{name}'."

    let int (args : JsonElement) (name : string) =
        match tryGet args name with
        | Some v when v.ValueKind = JsonValueKind.Number ->
            match v.TryGetInt32() with
            | true, i -> Ok i
            | _ -> Error $"Argument '{name}' must be an integer."
        | Some v ->
            match Int32.TryParse(v.ToString()) with
            | true, i -> Ok i
            | _ -> Error $"Argument '{name}' must be an integer."
        | None -> Error $"Missing argument '{name}'."

    /// Box references, given as array of strings or numbers, or as a single string.
    let boxes (args : JsonElement) =
        match tryGet args "boxes" with
        | Some v when v.ValueKind = JsonValueKind.Array -> Ok [ for e in v.EnumerateArray() -> e.ToString() ]
        | Some v -> Ok [ v.ToString() ]
        | None -> Error "Missing argument 'boxes'."

let private schema (json : string) =
    use doc = JsonDocument.Parse json
    doc.RootElement.Clone()

let private boxesSchema =
    """{ "type": "array", "items": { "type": "string" },
         "description": "Boxes to apply the operation to. Each entry is a 1-based index (\"3\"), a range (\"1-5\"), a box id, \"selected\" or \"all\"." }"""

let private colorSchema =
    let names = App.NamedColors.all |> List.map fst |> String.concat ", "
    $"""{{ "type": "string", "description": "Color name ({names}) or hex string like \"#33AA55\"." }}"""

let private enumSchema<'T> (description : string) =
    let names = Enum.GetNames typeof<'T> |> Array.map (sprintf "\"%s\"") |> String.concat ", "
    $"""{{ "type": "string", "enum": [{names}], "description": "{description}" }}"""

let private noArgs = schema """{ "type": "object", "properties": {} }"""

let private tools : list<Tool> =
    [
        {
            name = "get_scene"
            description = "Returns the current scene: all boxes (1-based index, id, color, selection) and rendering settings."
            inputSchema = noArgs
            run = fun app _ -> Ok (getScene app)
        }
        {
            name = "add_boxes"
            description = $"Appends boxes to the end of the row. At most {maxBoxesPerCall} per call."
            inputSchema = schema $"""{{
                "type": "object",
                "properties": {{
                    "count": {{ "type": "integer", "minimum": 1, "maximum": {maxBoxesPerCall}, "description": "Number of boxes to add." }},
                    "color": {colorSchema}
                }},
                "required": ["count"] }}"""
            run = fun app args ->
                Args.int args "count" |> Result.bind (fun count ->
                    addBoxes app count (Args.optString args "color")
                )
        }
        {
            name = "set_box_color"
            description = "Sets the color of the given boxes."
            inputSchema = schema $"""{{
                "type": "object",
                "properties": {{ "boxes": {boxesSchema}, "color": {colorSchema} }},
                "required": ["boxes", "color"] }}"""
            run = fun app args ->
                match Args.boxes args, Args.string args "color" with
                | Ok boxes, Ok color -> setColor app boxes color
                | Error e, _ | _, Error e -> Error e
        }
        {
            name = "remove_boxes"
            description = "Removes the given boxes. Indices of the remaining boxes shift accordingly."
            inputSchema = schema $"""{{ "type": "object", "properties": {{ "boxes": {boxesSchema} }}, "required": ["boxes"] }}"""
            run = fun app args -> Args.boxes args |> Result.bind (removeBoxes app)
        }
        {
            name = "select_boxes"
            description = "Replaces the selection with the given boxes. An empty list clears the selection, [\"all\"] selects all boxes."
            inputSchema = schema $"""{{ "type": "object", "properties": {{ "boxes": {boxesSchema} }}, "required": ["boxes"] }}"""
            run = fun app args -> Args.boxes args |> Result.bind (setSelection app)
        }
        {
            name = "invert_selection"
            description = "Selects all unselected boxes and deselects all selected ones."
            inputSchema = noArgs
            run = fun app _ -> invertSelection app
        }
        {
            name = "set_rendering"
            description = "Changes how all boxes are rendered. Omitted settings stay unchanged."
            inputSchema = schema $"""{{
                "type": "object",
                "properties": {{
                    "fillMode": {enumSchema<Aardvark.Rendering.FillMode> "Polygon fill mode."},
                    "cullMode": {enumSchema<Aardvark.Rendering.CullMode> "Face culling mode."}
                }} }}"""
            run = fun app args -> setRendering app (Args.optString args "fillMode") (Args.optString args "cullMode")
        }
        {
            name = "reset_camera"
            description = "Resets the camera to its initial position."
            inputSchema = noArgs
            run = fun app _ -> resetCamera app
        }
    ]

// ---------------------------------------------------------------------------------------------------------------------
// JSON-RPC

module private ErrorCode =
    let parseError     = -32700
    let invalidRequest = -32600
    let methodNotFound = -32601
    let invalidParams  = -32602

let private success (id : JsonElement) (result : obj) : obj =
    {| jsonrpc = "2.0"; id = id; result = result |}

let private failure (id : obj) (code : int) (message : string) : obj =
    {| jsonrpc = "2.0"; id = id; error = {| code = code; message = message |} |}

let private emptyObject = schema "{}"

let private callTool (app : RemoteApp) (id : JsonElement) (parameters : JsonElement) =
    let name = Args.optString parameters "name" |> Option.defaultValue ""
    let args = Args.tryGet parameters "arguments" |> Option.defaultValue emptyObject

    match tools |> List.tryFind (fun t -> t.name = name) with
    | None -> failure id ErrorCode.invalidParams $"Unknown tool '{name}'."
    | Some tool ->
        // Domain errors are reported as tool results (isError), so the model can see them and correct itself.
        let text, isError =
            try
                match tool.run app args with
                | Ok scene -> JsonSerializer.Serialize(scene, options), false
                | Error e -> e, true
            with e ->
                Aardvark.Base.Log.warn "[MCP] tool '%s' failed: %A" name e
                $"Internal error: {e.Message}", true

        success id {| content = [| {| ``type`` = "text"; text = text |} |]; isError = isError |}

/// Handles a single JSON-RPC message. Returns None for notifications (no response).
let private handle (app : RemoteApp) (msg : JsonElement) : option<obj> =
    let id = Args.tryGet msg "id"
    let parameters = Args.tryGet msg "params" |> Option.defaultValue emptyObject

    match id, Args.optString msg "method" with
    | None, _ -> None // notification (e.g. notifications/initialized) or response, nothing to answer
    | Some id, None -> Some (failure id ErrorCode.invalidRequest "Missing method.")
    | Some id, Some meth ->
        match meth with
        | "initialize" ->
            let version =
                match Args.optString parameters "protocolVersion" with
                | Some v when List.contains v supportedProtocolVersions -> v
                | _ -> List.head supportedProtocolVersions

            Some (success id {|
                protocolVersion = version
                capabilities = {| tools = {| listChanged = false |} |}
                serverInfo = serverInfo
                instructions = instructions
            |})

        | "ping" ->
            Some (success id {| |})

        | "tools/list" ->
            let tools = tools |> List.map (fun t -> {| name = t.name; description = t.description; inputSchema = t.inputSchema |})
            Some (success id {| tools = tools |})

        | "tools/call" ->
            Some (callTool app id parameters)

        | _ ->
            Some (failure id ErrorCode.methodNotFound $"Method '{meth}' not found.")

// ---------------------------------------------------------------------------------------------------------------------
// HTTP

/// Local servers must validate the Origin header to prevent DNS rebinding attacks.
let private isAllowedOrigin (r : HttpRequest) =
    match r.header "origin" with
    | Choice1Of2 origin ->
        match Uri.TryCreate(origin, UriKind.Absolute) with
        | true, uri -> uri.IsLoopback
        | _ -> false
    | Choice2Of2 _ -> true

let private json (status : HttpCode) (body : obj) : WebPart =
    Response.response status (JsonSerializer.SerializeToUtf8Bytes(body, options))
    >=> Writers.setMimeType "application/json; charset=utf-8"

let private post (app : RemoteApp) : WebPart =
    request (fun r ->
        if not (isAllowedOrigin r) then
            Response.response HTTP_403 [||]
        else
            let parsed =
                try
                    use doc = JsonDocument.Parse(ReadOnlyMemory r.rawForm)
                    Some (doc.RootElement.Clone())
                with _ ->
                    None

            match parsed with
            | None ->
                json HTTP_400 (failure null ErrorCode.parseError "Parse error.")

            | Some msg when msg.ValueKind = JsonValueKind.Array ->
                // JSON-RPC batch (protocol version 2025-03-26)
                match [ for m in msg.EnumerateArray() do yield! Option.toList (handle app m) ] with
                | [] -> Response.response HTTP_202 [||]
                | responses -> json HTTP_200 responses

            | Some msg ->
                match handle app msg with
                | Some response -> json HTTP_200 response
                | None -> Response.response HTTP_202 [||]
    )

let webPart (app : RemoteApp) : WebPart =
    path "/mcp" >=> choose [
        POST >=> post app
        // No server-initiated streams and no sessions.
        Response.response HTTP_405 [||]
    ]
