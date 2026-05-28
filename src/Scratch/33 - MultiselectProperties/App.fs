module App

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Primitives.Golden

open RenderingParametersModel
open Model

type Action = MultiselectPropertiesAction

let layoutConfig = LayoutConfig.Default

let defaultLayout =
    layout {
        row {
            element {
                id "render"
                title "3D View"
                minSize 100
                weight 65
            }
            column {
                weight 35
                element {
                    id "list"
                    title "Box List"
                }
                stack {
                    element {
                        id "properties"
                        title "Properties"
                    }
                    element {
                        id "controls"
                        title "Controls"
                    }
                }
            }
        }
    }

let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox = 
    {
        id = Guid.NewGuid().ToString()
        geometry = box
        color = color           
    }

let update (model : MultiselectPropertiesModel) (act : Action) =
        
    match act with
    | CameraMessage m -> 
        { model with camera = FreeFlyController.update model.camera m }          
    | RenderingAction a ->
        { model with rendering = RenderingParameters.update model.rendering a }
    | Select id -> 
        let selection = 
            if HashSet.contains id model.selectedBoxes 
            then HashSet.remove id model.selectedBoxes 
            else HashSet.add id model.selectedBoxes

        { model with selectedBoxes = selection; lastSelected = Some id }           
    | Enter id -> { model with boxHovered = Some id }            
    | Exit -> { model with boxHovered = None }                             
    | AddBox -> 
        let i = model.boxes.Count                
        let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox Primitives.colors.[i % 5]
                                     
        { model with boxes = IndexList.add box model.boxes }
    | RemoveBox ->  
        let i = model.boxes.Count - 1
        let boxes = IndexList.removeAt i model.boxes

        { model with boxes = boxes }
    | ClearSelection -> { model with selectedBoxes = HashSet.empty }
    | SetBoxColor (id, color) ->
        let boxes = model.boxes |> IndexList.map (fun b -> if b.id = id then { b with color = color } else b)
        { model with boxes = boxes }
    | GoldenLayoutMsg msg -> { model with golden = model.golden |> GoldenLayout.update msg }
                    
let mkColor (model : AdaptiveMultiselectPropertiesModel) (box : AdaptiveVisibleBox) =
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

let mkISg (model : AdaptiveMultiselectPropertiesModel) (box : AdaptiveVisibleBox) =
                
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

let view (model : AdaptiveMultiselectPropertiesModel) =
    let frustum = AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

    pages (function
        | Pages.Page "render" ->
            FreeFlyController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                    style "width: 100%; height: 100%; background: #1B1C1E"
                    attribute "data-samples" "8"
                ])
                (
                    model.boxes
                        |> AList.toASet
                        |> ASet.map (fun b -> mkISg model b)
                        |> Sg.set
                        |> Sg.effect [
                            toEffect DefaultSurfaces.trafo
                            toEffect DefaultSurfaces.vertexColor
                            toEffect DefaultSurfaces.simpleLighting
                        ]
                        |> Sg.noEvents
                )

        | Pages.Page "list" ->
            require (Html.semui) (
                Incremental.div
                    (AttributeMap.ofList [clazz "ui list"; style "background: #1B1C1E; height: 100%; overflow-y: auto; padding: 6px"]) (
                        alist {
                            for b in model.boxes do
                                let! boxColor = b.color |> AVal.map Html.color
                                let! isSelected =
                                    model.selectedBoxes
                                    |> ASet.toAVal
                                    |> AVal.map (HashSet.contains b.id)
                                let! isHovered =
                                    model.boxHovered
                                    |> AVal.map (fun h -> h = Some b.id)

                                let borderStyle =
                                    if isSelected then "border-left: 3px solid #4fc3f7; background: #2a3a4a;"
                                    elif isHovered then "border-left: 3px solid #555; background: #252525;"
                                    else "border-left: 3px solid transparent; background: #222;"

                                yield
                                    div [
                                        clazz "item"
                                        style (sprintf "display: flex; align-items: center; padding: 6px 8px; margin-bottom: 3px; border-radius: 4px; cursor: pointer; %s" borderStyle)
                                        onClick (fun _ -> Select b.id)
                                        onMouseEnter (fun _ -> Enter b.id)
                                        onMouseLeave (fun _ -> Exit)
                                    ] [
                                        div [style (sprintf "width: 14px; height: 14px; border-radius: 50%%; background: %s; border: 1px solid rgba(255,255,255,0.3); flex-shrink: 0; margin-right: 8px" boxColor)] []
                                        span [style "color: #ddd; font-size: 12px; font-family: monospace; overflow: hidden; text-overflow: ellipsis; white-space: nowrap"] [
                                            text (b.id.Substring(0, min 8 b.id.Length))
                                        ]
                                    ]
                        }
                    )
            )

        | Pages.Page "properties" ->
            let content =
                model.lastSelected |> AVal.bind (function
                    | None -> AVal.constant (div [style "color: #aaa; padding: 10px"] [text "No box selected"])
                    | Some id ->
                        model.boxes |> AList.toAVal |> AVal.map (fun boxes ->
                            match boxes |> Seq.tryFind (fun b -> b.id = id) with
                            | None -> div [style "color: #aaa; padding: 10px"] [text "Box not found"]
                            | Some box ->
                                require (Html.semui) (
                                    div [style "height: 100%; overflow-y: auto; background: #1B1C1E"] [
                                        Html.table [
                                            Html.row "ID:"    [text (box.id.Substring(0, min 8 box.id.Length) + "...")]
                                            Html.row "Min:"   [Incremental.text (box.geometry |> AVal.map (fun g -> sprintf "%.2f, %.2f, %.2f" g.Min.X g.Min.Y g.Min.Z))]
                                            Html.row "Max:"   [Incremental.text (box.geometry |> AVal.map (fun g -> sprintf "%.2f, %.2f, %.2f" g.Max.X g.Max.Y g.Max.Z))]
                                            Html.row "Color:" [ColorPicker.view ColorPicker.Config.Dark.Toggle (fun c -> SetBoxColor (id, c)) box.color]
                                        ]
                                    ]
                                )
                        )
                )
            Incremental.div (AttributeMap.ofList [style "height: 100%; overflow: hidden"]) (AList.ofAVal (content |> AVal.map List.singleton))

        | Pages.Page "controls" ->
            require (Html.semui) (
                div [style "background: #1B1C1E; height: 100%; overflow-y: auto"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingParameters.view model.rendering |> UI.map RenderingAction
                    ]
                    div [clazz "ui buttons"; style "padding: 4px"] [
                        button [clazz "ui button"; onMouseClick (fun _ -> AddBox)] [text "Add Box"]
                        button [clazz "ui button"; onMouseClick (fun _ -> RemoveBox)] [text "Remove Box"]
                        button [clazz "ui button"; onMouseClick (fun _ -> ClearSelection)] [text "Clear Selection"]
                    ]
                ]
            )

        | Pages.Page unknown ->
            div [style "color: red; padding: 10px"] [
                text (sprintf "Unknown panel: %s" unknown)
            ]

        | Pages.Body ->
            body [style "width: 100%; height: 100%; overflow: hidden; margin: 0"] [
                GoldenLayout.view
                    [ style "width: 100%; height: 100%; min-width: 400px; min-height: 400px; overflow: hidden" ]
                    model.golden
            ]
    )
             
let initial =
    {
        camera           = FreeFlyController.initial            
        rendering        = RenderingParameters.initial            
        boxHovered       = None
        boxes = Primitives.mkBoxes 3 |> List.mapi (fun i k -> mkVisibleBox Primitives.colors.[i % 5] k) |> IndexList.ofList
        selectedBoxes = HashSet.empty
        lastSelected = None
        boxesMap = HashMap.empty
        golden = GoldenLayout.create layoutConfig defaultLayout
    }

let app : App<MultiselectPropertiesModel, AdaptiveMultiselectPropertiesModel, Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }
