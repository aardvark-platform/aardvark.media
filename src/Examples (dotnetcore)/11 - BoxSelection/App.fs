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
        testValue = float sort
    }

let update (model : BoxSelectionDemoModel) (act : Action) =

    let helperHelper (key: string) (updateFunc: VisibleBox -> VisibleBox) model =
        //// version 1 and 2 where helper structure is used to pre-sort
        //let oldValue = model.boxesMap |> HashMap.find key
        //let helper = model.boxesHelper.Add(key, updateFunc oldValue)
        //{ model with boxesHelper = helper; boxesMap = helper.Map; boxesSortedValues = helper.SortedValues; boxesSortedKeys = helper.SortedKeys }
        
        // version 3 where adaptive sorting is performed in view
        { model with boxesMap = model.boxesMap |> HashMap.alter key (Option.map updateFunc) }

    match act with
        | CameraMessage m -> 
            { model with camera = FreeFlyController.update model.camera m }          
        | RenderingAction a ->
            { model with rendering = RenderingParameters.update model.rendering a }
        | Select id-> 
            model |> helperHelper id (fun oldValue -> { oldValue with isSelected = not oldValue.isSelected })
        | Enter id-> 
            model |> helperHelper id (fun oldValue -> { oldValue with isHovered = true })
        | Exit id ->
            model |> helperHelper id (fun oldValue -> { oldValue with isHovered = false })
        | AddBox ->  
            let i = model.boxesMap.Count                
            let box = Primitives.mkNthBox i (i+1) |> mkVisibleBox i Primitives.colors.[i % 5]
            //let helper = model.boxesHelper.Add(box.id, box)  
            //{ model with boxesHelper = helper; boxesMap = helper.Map; boxesSortedValues = helper.SortedValues; boxesSortedKeys = helper.SortedKeys }
            { model with boxesMap = model.boxesMap |> HashMap.add box.id box }
        | RemoveBox ->  
            let i = model.boxesMap.Count - 1
            //let lastKey = model.boxesHelper.SortedKeys |> IndexList.last
            //let helper = model.boxesHelper.Remove lastKey
            //{ model with boxesHelper = helper; boxesMap = helper.Map; boxesSortedValues = helper.SortedValues; boxesSortedKeys = helper.SortedKeys }
            let lastkey, lastItem = model.boxesMap |> HashMap.toArray |> Array.sortWith (fun (k1, v1) (k2, v2) -> if v1.sorting < v2.sorting then -1 else 1) |> Array.last
            { model with boxesMap = model.boxesMap |> HashMap.remove lastkey }
        | ClearSelection -> 
            //let sortedValues = model.boxesSortedValues |> IndexList.map (fun v -> { v with isHovered = false; isSelected = false})
            //let map = model.boxesMap |> HashMap.map (fun k v -> { v with isHovered = false; isSelected = false })
            //// TODO how to update helper?
            //{ model with boxesMap = map; boxesSortedValues = sortedValues } 
            { model with boxesMap = model.boxesMap |> HashMap.map (fun k v -> { v with isHovered = false; isSelected = false })}
        | SetTestValue (id, newValue) -> 
            model |> helperHelper id (fun oldValue -> { oldValue with testValue = newValue })
        | SetSorting (id, newValue) -> 
            model |> helperHelper id (fun oldValue -> { oldValue with sorting = int newValue })

let floatInputPostFix (placeholderName : string) (postFix:string) (minValue : float) (maxValue : float) (step : float) (changed : float -> 'msg) (value : aval<float>) (containerAttribs : AttributeMap<'msg>) =
    let defaultValue = max 0.0 minValue
    let parse (str : string) =
        match System.Double.TryParse str with
            | (true, v) -> v
            | _ -> defaultValue

    let changed =
        AttributeValue.Event  {
            clientSide = fun send src -> 
                String.concat "" [
                    "if(!event.inputType && event.target.value != event.target.oldValue) {"
                    "   event.target.oldValue = event.target.value;"
                    "   " + send src ["event.target.value"] + ";"
                    "}"
                ]
            serverSide = fun client src args ->
                match args with
                    | a :: _ -> 
                        let str : string = Pickler.unpickleOfJson a 
                        str |> float |> changed |> Seq.singleton
                    | _ -> Seq.empty
        }

    Incremental.div containerAttribs <|
        AList.ofList [
            Incremental.input <|
                AttributeMap.ofListCond [
                    yield always <| attribute "type" "number"
                    yield always <| attribute "step" (string step)
                    yield always <| attribute "min" (string minValue)
                    yield always <| attribute "max" (string maxValue)

                    yield always <| attribute "placeholder" placeholderName
                    yield always <| attribute "size" "4"
        
                    yield always <| ("oninput", changed)
                    yield always <| ("onchange", changed)

                    yield "value", value |> AVal.map (string >> AttributeValue.String >> Some)

                ]
            text postFix
        ]

let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

let mkColor (box : AdaptiveVisibleBox) : aval<C4b> =
    // adaptive changes only rely on visible box itself (which are finegrained managed within UPDATE)
    adaptive {
        let! selected = box.isSelected
        let! hovered = box.isHovered
        let! value = box.testValue

        match value, selected, hovered with
        | v, _, _ when v > 10.0 -> return C4b.Green
        | _, false, false -> return box.color
        | _, true, true -> return C4b.Orange
        | _, true, false -> return C4b.Red
        | _, false, true -> return C4b.Blue
    }
    
let mkISg (box : AdaptiveVisibleBox) =
                
    let color = mkColor box

    Sg.box color box.geometry
        |> Sg.withEvents [
            Sg.onClick (fun _ -> Select box.id)
            Sg.onEnter (fun _ -> Enter box.id)
            Sg.onLeave (fun _ -> Exit box.id)
        ]

let viewItem (b : AdaptiveVisibleBox) : DomNode<_> = 
    // 2d list entry for each cube
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
        yield Incremental.text (b.sorting |> AVal.map (sprintf "sort:%i"))
        yield Incremental.text (color |> AVal.map (sprintf "%A"))
        yield floatInputPostFix "" "°C" 0.0 100.0 1.0 (fun v -> SetTestValue(b.id, v)) b.testValue AttributeMap.empty
        yield floatInputPostFix "" "sorting" -10.0 10000.0 1.0 (fun v -> SetSorting(b.id, v)) (b.sorting |> AVal.map float) AttributeMap.empty
        yield i [clazz "file outline middle aligned icon"][]}

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
                        ] // apply shader for whole sg once!
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
                //// version 1 where helper structure is used to organize order
                //Incremental.div (AttributeMap.ofList [clazz "ui divided list"]) 
                //    (model.boxesSortedKeys |> AList.mapA (fun (key: string) ->
                //        let box = model.boxesMap |> AMap.find key
                //        let item = box |> AVal.map viewItem
                //        item)
                //    )
                //// version 2 where helper structure is directly used (test if focus gets lost) - update same like version 1
                //Incremental.div (AttributeMap.ofList [clazz "ui divided list"]) 
                //    (model.boxesSortedValues |> AList.map viewItem)
                //// version 3 adaptive sorting
                Incremental.div (AttributeMap.ofList [clazz "ui divided list"])
                    (model.boxesMap 
                    |> AMap.toASet 
                    |> ASet.mapA (fun (k,v) -> v.sorting |> AVal.map (fun sort -> k, sort, v))
                    |> ASet.sortBy (fun (k, sort, v) -> sort)
                    |> AList.map (fun (key, _, b) -> viewItem b))
            ]
        ]
    )
             
let initial =
    //let mutable compareHelper = SortedHashMap.SortedHashMap<string, VisibleBox>.Empty(fun (v1) (v2) -> if v1.sorting < v2.sorting then -1 else 1)
        
    //Primitives.mkBoxes 1000 |> List.iteri (fun i k -> 
    //    let box = mkVisibleBox i Primitives.colors.[i % 5] k
    //    compareHelper <- compareHelper.Add(box.id, box))

    let map = Primitives.mkBoxes 1000 |> List.mapi (fun i k -> 
        let box = mkVisibleBox i Primitives.colors.[i % 5] k
        box.id, box) |> HashMap.ofList

    {
        camera            = FreeFlyController.initial            
        rendering         = RenderingParameters.initial               
        boxesMap          = map // compareHelper.Map
        //boxesHelper       = compareHelper       
        //boxesSortedValues = compareHelper.SortedValues
        //boxesSortedKeys   = compareHelper.SortedKeys
    }

let app : App<BoxSelectionDemoModel,AdaptiveBoxSelectionDemoModel,Action> =
    {
        unpersist = Unpersist.instance
        threads = fun model -> FreeFlyController.threads model.camera |> ThreadPool.map CameraMessage
        initial = initial
        update = update
        view = view
    }

