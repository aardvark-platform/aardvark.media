module App

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
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

let update (model : BoxSelectionDemoModel) (act : Action) =
        
    match act with
        | CameraMessage m -> 
                { model with camera = FreeFlyController.update model.camera m }          
        | RenderingAction a ->
                { model with rendering = RenderingParameters.update model.rendering a }
        | Select id-> 
            let selection = 
                if HSet.contains id model.selectedBoxes 
                then HSet.remove id model.selectedBoxes 
                else HSet.add id model.selectedBoxes

            { model with selectedBoxes = selection }           
        | Enter id-> { model with boxHovered = Some id }            
        | Exit -> { model with boxHovered = None }                             
        | AddBox -> 
                
            let i = model.boxes.Count                
            let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox Primitives.colors.[i % 5]
                                         
            { model with boxes = PList.append box model.boxes }
        | RemoveBox ->  
            let i = model.boxes.Count - 1
            let boxes = PList.removeAt i model.boxes

            {model with boxes = boxes}
        | ClearSelection -> { model with selectedBoxes = HSet.empty}
                        
let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

let mkColor (model : MBoxSelectionDemoModel) (box : MVisibleBox) =
    let id = box.id 

    let color =  
        model.selectedBoxes 
            |> ASet.contains id 
            |> Mod.bind (function 
                | true -> Mod.constant Primitives.selectionColor 
                | false -> box.color
              )

    let color = 
        model.boxHovered |> Mod.bind (function 
            | Some k -> if k = id then Mod.constant Primitives.hoverColor else color
            | None -> color
        )

    color

let mkISg (model : MBoxSelectionDemoModel) (box : MVisibleBox) =
                
    let color = mkColor model box

    Sg.box color box.geometry
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
                }                
            |> Sg.requirePicking
            |> Sg.noEvents
            |> Sg.withEvents [
                Sg.onClick (fun _ -> Select box.id)
                Sg.onEnter (fun _ -> Enter box.id)
                Sg.onLeave (fun () -> Exit)
            ]

let view (model : MBoxSelectionDemoModel) =
    let cam =
        model.camera.view 
                           
    let frustum =
        Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
    require (Html.semui) (
        div [clazz "ui"; style "background: #1B1C1E"] [
            FreeFlyController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                    attribute "style" "width:65%; height: 100%; float: left;"
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

            div [style "width:35%; height: 100%; float:right; background: #1B1C1E"] [
                Html.SemUi.accordion "Rendering" "configure" true [
                    RenderingParameters.view model.rendering |> UI.map RenderingAction 
                ]  
                div [clazz "ui buttons"] [
                    button [clazz "ui button"; onMouseClick (fun _ -> AddBox)] [text "Add Box"]
                    button [clazz "ui button"; onMouseClick (fun _ -> RemoveBox)] [text "Remove Box"]
                    button [clazz "ui button"; onMouseClick (fun _ -> ClearSelection)] [text "Clear Selection"]
                ]

                Incremental.div 
                    (AttributeMap.ofList [clazz "ui divided list"]) (
                        alist {                                
                            for b in model.boxes do
                                let! c = mkColor model b

                                let bgc = sprintf "background: %s" (Html.ofC4b c)
                                    
                                yield 
                                    div [
                                        clazz "item"; style bgc; 
                                        onClick(fun _ -> Select b.id)
                                        onMouseEnter(fun _ -> Enter b.id)
                                        onMouseLeave(fun _ -> Exit)
                                     ] [
                                        i [clazz "file outline middle aligned icon"][]
                                     ]                                                                    
                        }     
                )
            ]
        ]
    )
             
let initial =
    {
        camera           = FreeFlyController.initial            
        rendering        = RenderingParameters.initial            
        boxHovered       = None
        boxes = Primitives.mkBoxes 3 |> List.mapi (fun i k -> mkVisibleBox Primitives.colors.[i % 5] k) |> PList.ofList
        selectedBoxes = HSet.empty         
        boxesSet = HSet.empty
        boxesMap = HMap.empty
    }

let app : App<BoxSelectionDemoModel,MBoxSelectionDemoModel,Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }

