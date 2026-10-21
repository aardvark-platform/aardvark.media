module App

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

open RenderingParametersModel
open Model

type Action = BoxSelectionDemoAction

let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox =
    {
        id = Guid.NewGuid().ToString()
        geometry = box
        color = color
    }

/// Colors addressable by name in the remote API.
module NamedColors =

    let all =
        [
            "red",    C4b(228, 26, 28)
            "green",  C4b(77, 175, 74)
            "blue",   C4b(55, 126, 184)
            "yellow", C4b(255, 221, 51)
            "orange", C4b(255, 127, 0)
            "purple", C4b(152, 78, 163)
            "cyan",   C4b(0, 190, 210)
            "pink",   C4b(247, 129, 191)
            "white",  C4b(240, 240, 240)
            "gray",   C4b(128, 128, 128)
            "black",  C4b(25, 25, 25)
        ]

    let toHex (c : C4b) = $"#%02X{c.R}%02X{c.G}%02X{c.B}"

    /// Parses a color name (see all) or a hex string (#RRGGBB).
    let tryParse (str : string) =
        let str = if isNull str then "" else str.Trim().ToLowerInvariant()
        match all |> List.tryFind (fun (n, _) -> n = str) with
        | Some (_, c) -> Some c
        | None ->
            let hex = str.TrimStart '#'
            match Int32.TryParse(hex, Globalization.NumberStyles.HexNumber, null) with
            | true, v when hex.Length = 6 -> Some (C4b(byte (v >>> 16), byte (v >>> 8), byte v))
            | _ -> None

let update (model : BoxSelectionDemoModel) (act : Action) =

    match act with
        | CameraMessage m ->
            { model with camera = FreeFlyController.update model.camera m }
        | RenderingAction a ->
            { model with rendering = RenderingParameters.update model.rendering a }
        | Select id->
            let selection =
                if HashSet.contains id model.selectedBoxes
                then HashSet.remove id model.selectedBoxes
                else HashSet.add id model.selectedBoxes

            { model with selectedBoxes = selection }
        | Enter id-> { model with boxHovered = Some id }
        | Exit -> { model with boxHovered = None }
        | AddBoxes (count, color) ->
            // Boxes are laid out in slots along the x-axis (see Primitives.mkNthBox). Continue after the
            // right-most box, so new boxes do not overlap existing ones after removals.
            let next =
                if model.boxes.IsEmpty then 0
                else model.boxes |> Seq.map (fun b -> int (round ((b.geometry.Min.X + 1.0) / 2.5))) |> Seq.max |> (+) 1

            let boxes =
                List.init (max 0 count) (fun j ->
                    let i = next + j
                    let color = color |> Option.defaultValue Primitives.colors.[i % 5]
                    Primitives.mkNthBox i (next + count) |> mkVisibleBox color
                )

            { model with boxes = IndexList.append model.boxes (IndexList.ofList boxes) }
        | SetColor (ids, color) ->
            let ids = HashSet.ofList ids
            { model with boxes = model.boxes |> IndexList.map (fun b -> if HashSet.contains b.id ids then { b with color = color } else b) }
        | SetSelection ids ->
            let existing = model.boxes |> Seq.map (fun b -> b.id) |> HashSet.ofSeq
            { model with selectedBoxes = HashSet.intersect existing (HashSet.ofList ids) }
        | RemoveBoxes ids ->
            let ids = HashSet.ofList ids
            { model with
                boxes = model.boxes |> IndexList.filter (fun b -> not (HashSet.contains b.id ids))
                selectedBoxes = HashSet.difference model.selectedBoxes ids }
        | InvertSelection ->
            let all = model.boxes |> Seq.map (fun b -> b.id) |> HashSet.ofSeq
            { model with selectedBoxes = HashSet.difference all model.selectedBoxes }
        | ResetCamera -> { model with camera = FreeFlyController.initial }

let mkColor (model : AdaptiveBoxSelectionDemoModel) (box : AdaptiveVisibleBox) =
    let id = box.id

    let color =
        model.selectedBoxes
            |> ASet.toAVal
            |> AVal.map (HashSet.contains id)
            |> AVal.bind (function
                | true -> AVal.constant Primitives.selectionColor
                | false -> box.color
              )

    let color =
        model.boxHovered |> AVal.bind (function
            | Some k -> if k = id then AVal.constant Primitives.hoverColor else color
            | None -> color
        )

    color

let mkISg (model : AdaptiveBoxSelectionDemoModel) (box : AdaptiveVisibleBox) =

    let color = mkColor model box

    Sg.box color box.geometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
            }
        |> Sg.requirePicking
        |> Sg.noEvents
        |> Sg.fillMode model.rendering.fillMode
        |> Sg.cullMode model.rendering.cullMode
        |> Sg.withEvents [
                Sg.onClick (fun _ -> Select box.id)
                Sg.onEnter (fun _ -> Enter box.id)
                Sg.onLeave (fun () -> Exit)
        ]

let view (model : AdaptiveBoxSelectionDemoModel) =
    let frustum =
        AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

    let status =
        (model.boxes.Content, model.selectedBoxes.Content)
        ||> AVal.map2 (fun boxes selected ->
            $"{boxes.Count} boxes · {selected.Count} selected · MCP: http://localhost:4321/mcp"
        )

    div [style "position: fixed; inset: 0; background: #1B1C1E"] [
        FreeFlyController.controlledControl model.camera CameraMessage frustum
            (AttributeMap.ofList [
                style "width: 100%; height: 100%"
                attribute "data-samples" "8"
            ])
            (
                model.boxes
                    |> AList.toASet
                    |> ASet.map (function b -> mkISg model b)
                    |> Sg.set
                    |> Sg.effect [
                        toEffect DefaultSurfaces.trafo
                        toEffect DefaultSurfaces.vertexColor
                        toEffect DefaultSurfaces.simpleLighting
                        ]
                    |> Sg.noEvents
            )

        div [style "position: fixed; right: 12px; bottom: 8px; color: #8b8b8b; font: 12px system-ui, sans-serif; pointer-events: none"] [
            Incremental.text status
        ]
    ]

let initial =
    {
        camera        = FreeFlyController.initial
        rendering     = RenderingParameters.initial
        boxHovered    = None
        boxes         = Primitives.mkBoxes 3 |> List.mapi (fun i k -> mkVisibleBox Primitives.colors.[i % 5] k) |> IndexList.ofList
        selectedBoxes = HashSet.empty
    }

let app : App<BoxSelectionDemoModel,AdaptiveBoxSelectionDemoModel,Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }
