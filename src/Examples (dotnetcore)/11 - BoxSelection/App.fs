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

let mkVisibleBox (sort: int) (color : C4b) (box : Box3d) : VisibleBox = 
    {
        id = Guid.NewGuid().ToString()
        geometry = box
        color = color 
        isSelected = false
        isHovered = false
        sorting = sort
    }

let update (model : BoxSelectionDemoModel) (act : Action) =
        
    match act with
        | CameraMessage m -> 
            { model with camera = FreeFlyController.update model.camera m }          
        | RenderingAction a ->
            { model with rendering = RenderingParameters.update model.rendering a }
        | Select id-> 
            //let selection = 
            //    if HashSet.contains id model.selectedBoxes 
            //    then HashSet.remove id model.selectedBoxes 
            //    else HashSet.add id model.selectedBoxes
            let oldValue = model.boxesMap |> HashMap.find id
            let helper = model.boxesHelper.Add(id, { oldValue with isSelected = not oldValue.isSelected })
            //let newBoxes = model.boxesMap |> HashMap.alter id (Option.map (fun old -> {old with isSelected = not old.isSelected}))
            //{ model with boxesMap = newBoxes }  // selectedBoxes = selection; 
            { model with boxesHelper = helper; boxesMap = helper.Map; boxes = helper.SortedValues }
        | Enter id-> 
            //let newBoxes = model.boxesMap |> HashMap.alter id (Option.map (fun old -> {old with isHovered = true}))
            let oldValue = model.boxesMap |> HashMap.find id
            let helper = model.boxesHelper.Add(id, { oldValue with isHovered = true })
            //{ model with boxesMap = newBoxes }  // boxHovered = Some id;
            { model with boxesHelper = helper; boxesMap = helper.Map; boxes = helper.SortedValues }
        | Exit id ->
            //let newBoxes = model.boxesMap |> HashMap.alter id (Option.map (fun old -> {old with isHovered = false}))
            //{ model with boxesMap = newBoxes }  //  boxHovered = None;
            let oldValue = model.boxesMap |> HashMap.find id
            let helper = model.boxesHelper.Add(id, { oldValue with isHovered = false })
            { model with boxesHelper = helper; boxesMap = helper.Map; boxes = helper.SortedValues }
        | AddBox ->  
            let i = model.boxesMap.Count                
            let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox i Primitives.colors.[i % 5]
            let helper = model.boxesHelper.Add(box.id, box)  
            //{ model with boxesMap = HashMap.add box.id box model.boxesMap }
            { model with boxesHelper = helper; boxesMap = helper.Map; boxes = helper.SortedValues }
        | RemoveBox ->  
            let i = model.boxesMap.Count - 1
            let lastKey = model.boxesHelper.SortedKeys |> IndexList.last
            let helper = model.boxesHelper.Remove lastKey
            //{ model with boxesMap = HashMap.add box.id box model.boxesMap }
            { model with boxesHelper = helper; boxesMap = helper.Map; boxes = helper.SortedValues }
        | ClearSelection -> 
            
            { model with boxesMap = model.boxesMap |> HashMap.map (fun k v -> { v with isHovered = false; isSelected = false })}

                        
let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

let mkColor (box : AdaptiveVisibleBox) =
    //let id = box.id 

    //let color =  
    //    model.selectedBoxes 
    //        |> ASet.toAVal 
    //        |> AVal.map (HashSet.contains id) 
    //        |> AVal.bind (function 
    //            | true -> AVal.constant Primitives.selectionColor 
    //            | false -> box.color
    //          )

    //let color = 
    //    model.boxHovered |> AVal.bind (function 
    //        | Some k -> if k = id then AVal.constant Primitives.hoverColor else color
    //        | None -> color
    //    )

    let color = (box.isSelected, box.isHovered) ||> AVal.map2 (fun selected hovered ->
        match selected, hovered with
        | false, false -> box.color
        | true, true -> C4b.Orange
        | true, false -> C4b.Red
        | false, true -> C4b.Blue
        )
        
    color

let mkISg (box : AdaptiveVisibleBox) =
                
    let color = mkColor box

    Sg.box color box.geometry
        |> Sg.withEvents [
            Sg.onClick (fun _ -> Select box.id)
            Sg.onEnter (fun _ -> Enter box.id)
            Sg.onLeave (fun _ -> Exit box.id)
        ]

let view (model : AdaptiveBoxSelectionDemoModel) =
    let cam =
        model.camera.view 
                           
    let frustum =
        AVal.constant (Frustum.perspective 60.0 0.1 1000.0 1.0)
      
    require (Html.semui) (
        div [clazz "ui"; style "background: #1B1C1E"] [
            FreeFlyController.controlledControl model.camera CameraMessage frustum
                (AttributeMap.ofList [
                    attribute "style" "width:65%; height: 100%; float: left;"
                    attribute "data-samples" "8"
                ])
                (model.boxesMap 
                    |> AMap.toASetValues
                    |> ASet.map mkISg
                    |> Sg.set
                    |> Sg.effect [
                        toEffect DefaultSurfaces.trafo
                        toEffect DefaultSurfaces.vertexColor
                        toEffect DefaultSurfaces.simpleLighting                              
                        ]
                    |> Sg.fillMode model.rendering.fillMode
                    |> Sg.cullMode model.rendering.cullMode
                    |> Sg.requirePicking
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
                        model.boxes
                        |> AList.map (fun b -> 
                            let color = mkColor b
                            let attr = amap {
                                yield clazz "item" 
                                yield onClick(fun _ -> Select b.id)
                                yield onMouseEnter(fun _ -> Enter b.id)
                                yield onMouseLeave(fun _ -> Exit b.id)
                                let! c = color
                                let bgc = sprintf "background: %s" (Html.ofC4b c)
                                yield style bgc
                            } 
                            Incremental.div (attr |> AttributeMap.ofAMap) <| alist { 
                                yield text (sprintf "sort:%i" b.sorting); 
                                yield Incremental.text (color |> AVal.map (sprintf "%A"))
                                yield i [clazz "file outline middle aligned icon"][]}
                ))
            ]
        ]
    )
             
let initial =
    let mutable compareHelper = SortedHashMap.SortedHashMap<string, VisibleBox>.Empty(fun (v1) (v2) -> if v1.sorting < v2.sorting then -1 else 1)
        
    Primitives.mkBoxes 5000 |> List.iteri (fun i k -> 
        let box = mkVisibleBox i Primitives.colors.[i % 5] k
        compareHelper <- compareHelper.Add(box.id, box))

    {
        camera           = FreeFlyController.initial            
        rendering        = RenderingParameters.initial               
        boxesHelper = compareHelper       
        boxesMap = compareHelper.Map
        boxes = compareHelper.SortedValues
    }

let app : App<BoxSelectionDemoModel,AdaptiveBoxSelectionDemoModel,Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }

