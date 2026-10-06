/// HTTP/JSON API on top of Api, served by the same Suave server as the UI.
module Rest

open System
open System.Text.Json

open Suave
open Suave.Filters
open Suave.Operators

open Api

[<CLIMutable>]
type AddBoxesRequest = { count : int; color : string }

/// Boxes may be given as numbers (indices) or strings (indices, ranges, ids, "selected", "all").
[<CLIMutable>]
type BoxesRequest = { boxes : JsonElement[]; color : string }

[<CLIMutable>]
type RenderingRequest = { fillMode : string; cullMode : string }

let private options =
    JsonSerializerOptions(
        PropertyNameCaseInsensitive = true,
        Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    )

let private respond (status : HttpCode) (body : obj) : WebPart =
    Response.response status (JsonSerializer.SerializeToUtf8Bytes(body, options))
    >=> Writers.setMimeType "application/json; charset=utf-8"

let private result (r : Result<SceneDto, string>) =
    match r with
    | Ok scene -> respond HTTP_200 scene
    | Error e -> respond HTTP_400 {| error = e |}

/// Parses the request body and runs the operation, answering 400 on malformed JSON.
let private post<'T> (f : 'T -> Result<SceneDto, string>) : WebPart =
    request (fun r ->
        let parsed =
            try
                let body = if r.rawForm.Length = 0 then "{}"B else r.rawForm
                match JsonSerializer.Deserialize<'T>(ReadOnlySpan body, options) |> box with
                | null -> Error "Empty request body."
                | v -> Ok (unbox<'T> v)
            with e -> Error $"Invalid JSON: {e.Message}"

        parsed |> Result.bind f |> result
    )

let private refs (boxes : JsonElement[]) =
    boxes |> Option.ofObj |> Option.defaultValue [||] |> Array.map (fun e -> e.ToString())

let webPart (app : RemoteApp) : WebPart =
    choose [
        GET  >=> path "/api/scene"            >=> request (fun _ -> respond HTTP_200 (getScene app))
        POST >=> path "/api/boxes"            >=> post (fun (r : AddBoxesRequest) -> addBoxes app (if r.count = 0 then 1 else r.count) (Option.ofObj r.color))
        POST >=> path "/api/boxes/color"      >=> post (fun (r : BoxesRequest) -> setColor app (refs r.boxes) r.color)
        POST >=> path "/api/boxes/remove"     >=> post (fun (r : BoxesRequest) -> removeBoxes app (refs r.boxes))
        POST >=> path "/api/selection"        >=> post (fun (r : BoxesRequest) -> setSelection app (refs r.boxes))
        POST >=> path "/api/selection/invert" >=> request (fun _ -> result (invertSelection app))
        POST >=> path "/api/camera/reset"     >=> request (fun _ -> result (resetCamera app))
        POST >=> path "/api/rendering"        >=> post (fun (r : RenderingRequest) -> setRendering app (Option.ofObj r.fillMode) (Option.ofObj r.cullMode))
    ]
