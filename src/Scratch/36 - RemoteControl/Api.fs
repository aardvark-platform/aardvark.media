/// Transport-agnostic remote control of the running app. Used by the HTTP API (Rest.fs) and the MCP server (Mcp.fs).
/// Every operation resolves its arguments against the current model, dispatches messages through the
/// MutableApp (the same path the browser uses, so the UI updates live) and returns the resulting scene.
module Api

open System

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

open RenderingParametersModel
open Model
open App.NamedColors

// The Aardvark opens shadow Error with another type, restore the Result cases.
open Microsoft.FSharp.Core

type RemoteApp = Aardvark.UI.MutableApp<BoxSelectionDemoModel, AdaptiveBoxSelectionDemoModel, BoxSelectionDemoAction>

type BoxDto =
    {
        /// 1-based position in the row of boxes.
        index    : int
        id       : string
        color    : string
        selected : bool
    }

type SceneDto =
    {
        boxCount      : int
        selectedCount : int
        boxes         : BoxDto[]
        fillMode      : string
        cullMode      : string
    }

let maxBoxesPerCall = 100

module Scene =

    let ofModel (model : BoxSelectionDemoModel) =
        let boxes =
            model.boxes |> IndexList.toArray |> Array.mapi (fun i b ->
                {
                    index    = i + 1
                    id       = b.id
                    color    = toHex b.color
                    selected = HashSet.contains b.id model.selectedBoxes
                }
            )

        {
            boxCount      = boxes.Length
            selectedCount = model.selectedBoxes.Count
            boxes         = boxes
            fillMode      = string model.rendering.fillMode
            cullMode      = string model.rendering.cullMode
        }

/// Resolves box references to ids. A reference is a 1-based index ("3"), a range ("1-5"),
/// a box id, "selected" or "all".
let resolveBoxes (model : BoxSelectionDemoModel) (refs : seq<string>) : Result<list<string>, string> =
    let ids = model.boxes |> IndexList.toArray |> Array.map (fun b -> b.id)

    let byIndex (i : int) =
        if i >= 1 && i <= ids.Length then Ok [ ids.[i - 1] ]
        else Error $"Box index {i} is out of range (there are {ids.Length} boxes, indices are 1-based)."

    let resolveOne (r : string) =
        let r = r.Trim()
        match r.ToLowerInvariant() with
        | "all" -> Ok (List.ofArray ids)
        | "selected" -> Ok (ids |> Array.filter (fun id -> HashSet.contains id model.selectedBoxes) |> List.ofArray)
        | _ ->
            match Int32.TryParse r with
            | true, i -> byIndex i
            | _ ->
                match r.Split('-') with
                | [| a; b |] ->
                    match Int32.TryParse(a.Trim()), Int32.TryParse(b.Trim()) with
                    | (true, a), (true, b) when a <= b ->
                        [a .. b] |> List.map byIndex |> List.fold (fun acc r ->
                            match acc, r with
                            | Ok xs, Ok ys -> Ok (xs @ ys)
                            | Error e, _ | _, Error e -> Error e
                        ) (Ok [])
                    | _ when Array.contains r ids -> Ok [ r ]
                    | _ -> Error $"Invalid box reference '{r}'."
                | _ when Array.contains r ids -> Ok [ r ]
                | _ -> Error $"Unknown box '{r}'. Use a 1-based index, a range like '1-5', a box id, 'selected' or 'all'."

    (Ok [], refs) ||> Seq.fold (fun acc r ->
        match acc, resolveOne r with
        | Ok xs, Ok ys -> Ok (xs @ ys)
        | Error e, _ | _, Error e -> Error e
    )
    |> Result.map List.distinct

let private parseColor (color : string) =
    match tryParse color with
    | Some c -> Ok c
    | None ->
        let names = all |> List.map fst |> String.concat ", "
        Error $"Unknown color '{color}'. Use one of {names} or a hex string like #33AA55."

let private parseEnum<'T when 'T : struct and 'T : (new : unit -> 'T) and 'T :> ValueType> (name : string) (value : string) =
    match Enum.TryParse<'T>(value, true) with
    | true, v when Enum.IsDefined(typeof<'T>, v) -> Ok v
    | _ ->
        let names = Enum.GetNames typeof<'T> |> String.concat ", "
        Error $"Invalid {name} '{value}'. Use one of {names}."

/// Computes the messages for the current model and dispatches them, atomically with respect to other updates.
let private run (app : RemoteApp) (getMessages : BoxSelectionDemoModel -> Result<list<BoxSelectionDemoAction>, string>) : Result<SceneDto, string> =
    lock app.UpdateLock (fun () ->
        getMessages (AVal.force app.Model)
        |> Result.map (fun msgs ->
            if not msgs.IsEmpty then app.UpdateSync(Guid.Empty, msgs)
            Scene.ofModel (AVal.force app.Model)
        )
    )

let getScene (app : RemoteApp) =
    lock app.UpdateLock (fun () -> Scene.ofModel (AVal.force app.Model))

let addBoxes (app : RemoteApp) (count : int) (color : option<string>) =
    run app (fun _ ->
        if count < 1 || count > maxBoxesPerCall then
            Error $"Count must be between 1 and {maxBoxesPerCall}."
        else
            match color with
            | Some c -> parseColor c |> Result.map (fun c -> [ AddBoxes (count, Some c) ])
            | None -> Ok [ AddBoxes (count, None) ]
    )

let setColor (app : RemoteApp) (boxes : seq<string>) (color : string) =
    run app (fun model ->
        match resolveBoxes model boxes, parseColor color with
        | Ok ids, Ok c -> Ok [ SetColor (ids, c) ]
        | Error e, _ | _, Error e -> Error e
    )

let removeBoxes (app : RemoteApp) (boxes : seq<string>) =
    run app (fun model -> resolveBoxes model boxes |> Result.map (fun ids -> [ RemoveBoxes ids ]))

let setSelection (app : RemoteApp) (boxes : seq<string>) =
    run app (fun model -> resolveBoxes model boxes |> Result.map (fun ids -> [ SetSelection ids ]))

let invertSelection (app : RemoteApp) = run app (fun _ -> Ok [ InvertSelection ])

let resetCamera (app : RemoteApp) = run app (fun _ -> Ok [ ResetCamera ])

let setRendering (app : RemoteApp) (fillMode : option<string>) (cullMode : option<string>) =
    run app (fun _ ->
        let fill = fillMode |> Option.map (parseEnum<FillMode> "fill mode" >> Result.map (SetFillMode >> RenderingAction))
        let cull = cullMode |> Option.map (parseEnum<CullMode> "cull mode" >> Result.map (SetCullMode >> RenderingAction))

        match fill, cull with
        | Some (Error e), _ | _, Some (Error e) -> Error e
        | _ -> Ok ([ fill; cull ] |> List.choose (Option.bind Result.toOption))
    )
