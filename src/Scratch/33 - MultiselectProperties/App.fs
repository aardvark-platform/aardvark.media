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

module Shader =
    open FShade

    type SuperVertex =
        {
            [<Position>] pos : V4f
            [<SourceVertexIndex>] i : int
        }

    let lines (t : Triangle<SuperVertex>) =
        line {
            yield t.P0
            yield t.P1
            restartStrip()
            yield t.P1
            yield t.P2
            restartStrip()
            yield t.P2
            yield t.P0
            restartStrip()
        }

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

        { model with selectedBoxes = selection; lastSelected = Some id; pendingColor = model.boxes |> Seq.tryFind (fun b -> b.id = id) |> Option.map (fun b -> b.color) }           
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
    | ClearSelection -> { model with selectedBoxes = HashSet.empty; pendingColor = None }
    | SetBoxColor (id, color) ->
        let boxes = model.boxes |> IndexList.map (fun b -> if b.id = id then { b with color = color } else b)
        { model with boxes = boxes }
    | SetPendingColor c -> { model with pendingColor = Some c }
    | ApplyPendingColor ->
        match model.pendingColor with
        | None -> model
        | Some c ->
            let boxes = model.boxes |> IndexList.map (fun b ->
                if HashSet.contains b.id model.selectedBoxes then { b with color = c } else b)
            { model with boxes = boxes; pendingColor = None }
    | GoldenLayoutMsg msg -> { model with golden = model.golden |> GoldenLayout.update msg }
                    
let mkISg (model : AdaptiveMultiselectPropertiesModel) (box : AdaptiveVisibleBox) =

    let passMain    = RenderPass.main
    let passOutline = RenderPass.after "outline" RenderPassOrder.Arbitrary passMain

    let stencilWrite =
        { StencilMode.None with
            Pass       = StencilOperation.Replace
            DepthFail  = StencilOperation.Replace
            Reference  = 1
            Comparison = ComparisonFunction.Greater }

    let stencilRead =
        { StencilMode.None with
            Comparison = ComparisonFunction.Greater
            Reference  = 1 }

    let isHighlighted =
        let sel = model.selectedBoxes |> ASet.toAVal |> AVal.map (HashSet.contains box.id)
        let hov = model.boxHovered |> AVal.map (fun h -> h = Some box.id)
        AVal.map2 (||) sel hov

    let outlineColor =
        let sel = model.selectedBoxes |> ASet.toAVal |> AVal.map (HashSet.contains box.id)
        let hov = model.boxHovered |> AVal.map (fun h -> h = Some box.id)
        AVal.map2 (fun s h -> if s then C4f.Red else if h then C4f.Blue else C4f.White) sel hov

    let regular =
        Sg.box box.color box.geometry
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.pass passMain
            |> Sg.requirePicking
            |> Sg.noEvents
            |> Sg.fillMode model.rendering.fillMode
            |> Sg.cullMode model.rendering.cullMode
            |> Sg.withEvents [
                Sg.onClick  (fun _ -> Select box.id)
                Sg.onEnter  (fun _ -> Enter box.id)
                Sg.onLeave  (fun () -> Exit)
            ]

    let mask =
        Sg.box box.color box.geometry
            |> Sg.pass passMain
            |> Sg.stencilMode' stencilWrite
            |> Sg.writeBuffers' (Set.singleton (WriteBuffer.Stencil))
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }
            |> Sg.onOff isHighlighted

    let outline =
        Sg.box box.color box.geometry
            |> Sg.pass passOutline
            |> Sg.stencilMode' stencilRead
            |> Sg.depthTest' DepthTest.None
            |> Sg.writeBuffers' (Set.singleton (WriteBuffer.Color DefaultSemantic.Colors))
            |> Sg.uniform "LineWidth" (AVal.constant 4.0)
            |> Sg.uniform "Color" outlineColor
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.lines
                do! DefaultSurfaces.thickLine
                do! DefaultSurfaces.thickLineRoundCaps
                do! DefaultSurfaces.sgColor
            }
            |> Sg.onOff isHighlighted

    Sg.ofSeq [ regular; mask; outline ]

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
                                    if isSelected then "border-left: 3px solid #e53935; background: #2e1a1a;"
                                    elif isHovered then "border-left: 3px solid #1e88e5; background: #1a1e2e;"
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
            let selCount = model.selectedBoxes |> ASet.toAVal |> AVal.map HashSet.count

            // Picker seeds from pendingColor if staged, else from lastSelected box's color
            let pickerColor : aval<C4b> =
                model.pendingColor |> AVal.bind (function
                    | Some c -> AVal.constant c
                    | None ->
                        model.lastSelected |> AVal.bind (function
                            | None -> AVal.constant (C4b(180, 180, 180, 255))
                            | Some id ->
                                model.boxes |> AList.toAVal |> AVal.bind (fun boxes ->
                                    match boxes |> Seq.tryFind (fun b -> b.id = id) with
                                    | Some b -> b.color
                                    | None    -> AVal.constant (C4b(180, 180, 180, 255))
                                )
                        )
                )

            // Color picker + Apply button — only used in multi-select view
            let multiColorRow () =
                require (Html.semui) (
                    div [] [
                        div [style "display: flex; justify-content: space-between; align-items: center; padding: 5px 0; border-bottom: 1px solid #2a2a2a"] [
                            span [style "color: #777; font-size: 12px"] [text "Color"]
                            div [style "display: flex; align-items: center; gap: 6px"] [
                                Incremental.div (AttributeMap.ofList [style "display: flex; align-items: center"]) (
                                    alist {
                                        let! c = pickerColor
                                        yield div [style (sprintf "width: 14px; height: 14px; border-radius: 50%%; background: %s; border: 1px solid rgba(255,255,255,0.2); flex-shrink: 0" (Html.color c))] []
                                    }
                                )
                                ColorPicker.view ColorPicker.Config.Dark.Toggle SetPendingColor pickerColor
                            ]
                        ]
                        button [
                            style "margin-top: 10px; background: #1e88e5; color: #fff; border: none; border-radius: 4px; padding: 6px 10px; font-size: 12px; cursor: pointer; width: 100%"
                            onMouseClick (fun _ -> ApplyPendingColor)
                        ] [text "Apply to Selection"]
                    ]
                )

            let content =
                AVal.map2 (fun count lastSel -> count, lastSel) selCount model.lastSelected
                |> AVal.bind (function
                    | 0, _ ->
                        AVal.constant (div [style "color: #555; padding: 10px; font-size: 12px"] [text "No selection"])

                    | 1, Some id ->
                        model.boxes |> AList.toAVal |> AVal.map (fun boxes ->
                            match boxes |> Seq.tryFind (fun b -> b.id = id) with
                            | None -> div [style "color: #aaa; padding: 10px"] [text "Box not found"]
                            | Some box ->
                                require (Html.semui) (
                                    div [style "height: 100%; overflow-y: auto; background: #1B1C1E; padding: 10px; color: #ccc; font-size: 13px"] [

                                        div [style "font-size: 10px; font-weight: bold; letter-spacing: 1px; text-transform: uppercase; color: #666; margin-bottom: 8px"] [
                                            text "VisibleBox"
                                        ]

                                        div [style "display: flex; justify-content: space-between; align-items: center; padding: 5px 0; border-bottom: 1px solid #2a2a2a"] [
                                            span [style "color: #777; font-size: 12px"] [text "ID"]
                                            span [style "font-family: monospace; font-size: 12px; color: #aaa"] [text (box.id.Substring(0, min 8 box.id.Length) + "…")]
                                        ]

                                        div [style "display: flex; justify-content: space-between; align-items: center; padding: 5px 0; border-bottom: 1px solid #2a2a2a"] [
                                            span [style "color: #777; font-size: 12px"] [text "Min"]
                                            Incremental.span (AttributeMap.ofList [style "font-family: monospace; font-size: 11px; color: #aaa"]) (
                                                AList.ofList [Incremental.text (box.geometry |> AVal.map (fun g -> sprintf "%.2f, %.2f, %.2f" g.Min.X g.Min.Y g.Min.Z))]
                                            )
                                        ]

                                        div [style "display: flex; justify-content: space-between; align-items: center; padding: 5px 0; border-bottom: 1px solid #2a2a2a"] [
                                            span [style "color: #777; font-size: 12px"] [text "Max"]
                                            Incremental.span (AttributeMap.ofList [style "font-family: monospace; font-size: 11px; color: #aaa"]) (
                                                AList.ofList [Incremental.text (box.geometry |> AVal.map (fun g -> sprintf "%.2f, %.2f, %.2f" g.Max.X g.Max.Y g.Max.Z))]
                                            )
                                        ]

                                        // Single select: instant color change, no Apply
                                        div [style "display: flex; justify-content: space-between; align-items: center; padding: 5px 0"] [
                                            span [style "color: #777; font-size: 12px"] [text "Color"]
                                            div [style "display: flex; align-items: center; gap: 6px"] [
                                                Incremental.div (AttributeMap.ofList [style "display: flex; align-items: center"]) (
                                                    alist {
                                                        let! c = box.color
                                                        yield div [style (sprintf "width: 14px; height: 14px; border-radius: 50%%; background: %s; border: 1px solid rgba(255,255,255,0.2); flex-shrink: 0" (Html.color c))] []
                                                    }
                                                )
                                                ColorPicker.view ColorPicker.Config.Dark.Toggle (fun c -> SetBoxColor (id, c)) box.color
                                            ]
                                        ]
                                    ]
                                )
                        )

                    | n, _ ->
                        AVal.constant (
                            require (Html.semui) (
                                div [style "height: 100%; overflow-y: auto; background: #1B1C1E; padding: 10px; color: #ccc; font-size: 13px"] [

                                    div [style "font-size: 10px; font-weight: bold; letter-spacing: 1px; text-transform: uppercase; color: #666; margin-bottom: 8px"] [
                                        text (sprintf "%d items selected" n)
                                    ]

                                    multiColorRow ()
                                ]
                            )
                        )
                )
            Incremental.div (AttributeMap.ofList [style "height: 100%; overflow: hidden"]) (AList.ofAVal (content |> AVal.map List.singleton))

        | Pages.Page "controls" ->
            require (Html.semui) (
                div [style "background: #1B1C1E; height: 100%; overflow-y: auto; padding: 10px; color: #ccc; font-size: 13px"] [

                    div [style "font-size: 10px; font-weight: bold; letter-spacing: 1px; text-transform: uppercase; color: #555; margin-bottom: 6px; padding-bottom: 4px; border-bottom: 1px solid #2a2a2a"] [
                        text "Rendering"
                    ]
                    div [style "margin-bottom: 16px"] [
                        RenderingParameters.view model.rendering |> UI.map RenderingAction
                    ]

                    div [style "font-size: 10px; font-weight: bold; letter-spacing: 1px; text-transform: uppercase; color: #555; margin-bottom: 6px; padding-bottom: 4px; border-bottom: 1px solid #2a2a2a"] [
                        text "Scene"
                    ]
                    div [style "display: flex; flex-direction: column; gap: 4px"] [
                        button [
                            style "background: #252525; color: #ccc; border: 1px solid #333; border-radius: 4px; padding: 6px 10px; font-size: 12px; cursor: pointer; text-align: left; width: 100%"
                            onMouseClick (fun _ -> AddBox)
                        ] [text "+ Add Box"]
                        button [
                            style "background: #252525; color: #ccc; border: 1px solid #333; border-radius: 4px; padding: 6px 10px; font-size: 12px; cursor: pointer; text-align: left; width: 100%"
                            onMouseClick (fun _ -> RemoveBox)
                        ] [text "− Remove Box"]
                        button [
                            style "background: #252525; color: #888; border: 1px solid #333; border-radius: 4px; padding: 6px 10px; font-size: 12px; cursor: pointer; text-align: left; width: 100%"
                            onMouseClick (fun _ -> ClearSelection)
                        ] [text "✕ Clear Selection"]
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
        pendingColor = None
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
