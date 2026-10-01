module App

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

open RenderingParametersModel
open CommandPalette
open Model

type Action = BoxSelectionDemoAction

let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox =
    {
        id = Guid.NewGuid().ToString()
        geometry = box
        color = color
    }

// The state the commands depend on. Extracted from both the model (for executing commands)
// and the adaptive model (for sending the current commands to the client).
type CommandContext =
    {
        boxes     : list<string * C4b>
        selected  : HashSet<string>
        rendering : RenderingParameters
    }

module CommandContext =

    let ofModel (model : BoxSelectionDemoModel) =
        {
            boxes     = model.boxes |> IndexList.toList |> List.map (fun b -> b.id, b.color)
            selected  = model.selectedBoxes
            rendering = model.rendering
        }

    let ofAdaptiveModel (model : AdaptiveBoxSelectionDemoModel) =
        adaptive {
            let! boxes = model.boxes |> AList.mapA (fun b -> b.color |> AVal.map (fun c -> b.id, c)) |> AList.toAVal
            let! selected = model.selectedBoxes.Content
            let! fillMode = model.rendering.fillMode
            let! cullMode = model.rendering.cullMode

            return {
                boxes     = IndexList.toList boxes
                selected  = selected
                rendering = { fillMode = fillMode; cullMode = cullMode }
            }
        }

let private enumCommands (current : 'T) (toMsg : 'T -> Action) =
    Enum.GetValues typeof<'T>
    |> Seq.cast<'T>
    |> Seq.map (fun v ->
        PaletteCommand.run "" (string v) (if v = current then "current" else "") (toMsg v)
    )
    |> Seq.toList

let commands (ctx : CommandContext) : list<PaletteCommand<Action>> =
    let boxes =
        ctx.boxes |> List.mapi (fun i (id, c) ->
            let detail =
                let color = $"#%02X{c.R}%02X{c.G}%02X{c.B}"
                if HashSet.contains id ctx.selected then $"selected · {color}" else color

            PaletteCommand.create id "Box" $"Box {i + 1}" detail (Run (Select id))
        )

    [
        PaletteCommand.run  "Boxes"     "Add Box"                  "" AddBox
        PaletteCommand.run  "Boxes"     "Remove Last Box"          "" RemoveBox
        PaletteCommand.run  "Boxes"     "Remove Selected Boxes"    $"{ctx.selected.Count} selected" RemoveSelected
        PaletteCommand.pick "Selection" "Toggle Box Selection"     "Select a box to toggle" boxes
        PaletteCommand.run  "Selection" "Select All"               "" SelectAll
        PaletteCommand.run  "Selection" "Invert Selection"         "" InvertSelection
        PaletteCommand.run  "Selection" "Clear Selection"          "" ClearSelection
        PaletteCommand.pick "Rendering" "Fill Mode"                "Select fill mode" (enumCommands ctx.rendering.fillMode (SetFillMode >> RenderingAction))
        PaletteCommand.pick "Rendering" "Cull Mode"                "Select cull mode" (enumCommands ctx.rendering.cullMode (SetCullMode >> RenderingAction))
        PaletteCommand.run  "Camera"    "Reset Camera"             "" ResetCamera
    ]

let rec update (model : BoxSelectionDemoModel) (act : Action) =

    match act with
        | CameraMessage m ->
            { model with camera = FreeFlyController.update model.camera m }
        | RenderingAction a ->
            { model with rendering = RenderingParameters.update model.rendering a }
        | PaletteAction m ->
            let palette, cmd = CommandPalette.update (commands (CommandContext.ofModel model)) model.palette m
            let model = { model with palette = palette }

            match cmd with
            | Some cmd -> update model cmd
            | None -> model
        | Select id->
            let selection =
                if HashSet.contains id model.selectedBoxes
                then HashSet.remove id model.selectedBoxes
                else HashSet.add id model.selectedBoxes

            { model with selectedBoxes = selection }
        | Enter id-> { model with boxHovered = Some id }
        | Exit -> { model with boxHovered = None }
        | AddBox ->

            let i = model.boxes.Count
            let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox Primitives.colors.[i % 5]

            { model with boxes = IndexList.add box model.boxes }
        | RemoveBox ->
            match IndexList.tryLast model.boxes with
            | Some last ->
                { model with
                    boxes = IndexList.removeAt (model.boxes.Count - 1) model.boxes
                    selectedBoxes = HashSet.remove last.id model.selectedBoxes }
            | None -> model
        | RemoveSelected ->
            { model with
                boxes = model.boxes |> IndexList.filter (fun b -> not (HashSet.contains b.id model.selectedBoxes))
                selectedBoxes = HashSet.empty }
        | ClearSelection -> { model with selectedBoxes = HashSet.empty}
        | SelectAll -> { model with selectedBoxes = model.boxes |> Seq.map (fun b -> b.id) |> HashSet.ofSeq }
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

    let commands =
        CommandContext.ofAdaptiveModel model |> AVal.map commands

    let status =
        (model.boxes.Content, model.selectedBoxes.Content)
        ||> AVal.map2 (fun boxes selected ->
            $"{boxes.Count} boxes · {selected.Count} selected · Ctrl+P for commands"
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

        div [clazz "cp-status"] [ Incremental.text status ]

        CommandPalette.view PaletteAction commands model.palette
    ]

let initial =
    {
        camera        = FreeFlyController.initial
        rendering     = RenderingParameters.initial
        palette       = CommandPalette.initial
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
