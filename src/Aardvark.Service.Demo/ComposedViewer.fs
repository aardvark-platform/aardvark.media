namespace UI.Composed

open System
open Aardvark.UI
open Aardvark.Base.Incremental
open Aardvark.SceneGraph.AirState
open Demo

open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text
open Demo.TestApp
open Demo.TestApp.Mutable

module Primitives =
    open PRo3DModels

    let hoverColor = C4b.Blue
    let selectionColor = C4b.Red
    let colors = [new C4b(166,206,227); new C4b(178,223,138); new C4b(251,154,153); new C4b(253,191,111); new C4b(202,178,214)]

    let mkNthBox i n = 
        let min = -V3d.One
        let max =  V3d.One

        let offset = 0.0 * (float n) * V3d.IOO

        new Box3d(min + V3d.IOO * 2.5 * (float i) - offset, max + V3d.IOO * 2.5 * (float i) - offset)

    let mkBoxes number =        
        [0..number-1] |> List.map (function x -> mkNthBox x number)

    let hoveredColor (model : MComposedViewerModel) (box : VisibleBox) =
        model.boxHovered |> Mod.map (fun h -> match h with
                                                | Some i -> if i = box.id then hoverColor else box.color
                                                | None -> box.color)

    let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox = 
        {
            id = Guid.NewGuid().ToString()
            geometry = box
            color = color           
        }

    let mkISgBox (model : MComposedViewerModel) (box : VisibleBox) =
        let box' = Mod.constant (box.geometry)
        let c = hoveredColor model box

        Sg.box c box'
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                    }
                |> Sg.noEvents    
    
module SimpleCompositionViewer = 
    open PRo3DModels
    open Aardvark.Base
    
    type Action =
        | CameraMessage    of CameraController.Message
        | AnnotationAction of AnnotationProperties.Action
        | RenderingAction  of RenderingProperties.Action
        | Enter of string
        | Exit      

    let update (model : ComposedViewerModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }
            | AnnotationAction a ->
                 { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | Enter id-> { model with boxHovered = Some id }
            | Exit -> { model with boxHovered = None }    
            
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        
                        let boxes = Primitives.mkBoxes 5
                                              
                        let boxes = boxes 
                                    |> List.mapi (fun i k -> Primitives.mkVisibleBox Primitives.colors.[i] k)
                                    |> List.map (fun k -> Primitives.mkISgBox model k 
                                                                |> Sg.pickable (PickShape.Box k.geometry)
                                                                |> Sg.withEvents [
                                                                        Sg.onEnter (fun p -> Enter k.id)
                                                                        Sg.onLeave (fun () -> Exit)]
                                                )
                        boxes
                            |> Sg.ofList
                            |> Sg.noEvents     
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                            |> Sg.noEvents                        
                )

                div [style "width:35%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]
                    Html.SemUi.accordion "Properties" "options" true [
                        AnnotationProperties.view model.singleAnnotation |> UI.map AnnotationAction
                    ]
                ]
            ]
        )

    let initial =
        {
            camera           = CameraController.initial
            singleAnnotation = InitValues.annotation
            rendering        = InitValues.rendering
            
            boxHovered = None
        }

    let app : App<ComposedViewerModel,MComposedViewerModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

module OrbitCameraDemo = 
    open PRo3DModels
    open Aardvark.Base    
    open Demo.TestApp.Mutable.MCameraControllerState
    
    type Action =
        | CameraMessage    of ArcBallController.Message
        | RenderingAction  of RenderingProperties.Action        
        //| NavigationAction  of NavigationProperties.Action

    let update (model : OrbitCameraDemoModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                { model with camera = ArcBallController.update model.camera m }
            | RenderingAction a ->
                { model with rendering = RenderingProperties.update model.rendering a }       
            //| NavigationAction a ->
            //    { model with navigation = NavigationProperties.update model.navigation a }

    let view (model : MOrbitCameraDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
            
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                ArcBallController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        let color = Mod.constant C4b.Blue
                        let boxGeometry = Box3d(-V3d.III, V3d.III)
                        let box = Mod.constant (boxGeometry)                       
                        
                        let trafo = 
                            model.camera.orbitCenter 
                                |> Mod.bind (fun center -> match center with 
                                                            | Some x -> Mod.constant (Trafo3d.Translation x)
                                                            | None -> Mod.constant (Trafo3d.Identity))

                        let b = Sg.box color box                            
                                    |> Sg.shader {
                                        do! DefaultSurfaces.trafo
                                        do! DefaultSurfaces.vertexColor
                                        do! DefaultSurfaces.simpleLighting
                                        }
                                    |> Sg.noEvents
                                    |> Sg.pickable (PickShape.Box boxGeometry)
                                    |> Sg.withEvents [
                                            Sg.onDoubleClick (fun p -> ArcBallController.Message.Pick p) ] |> Sg.map CameraMessage

                        let s = Sg.sphere 20 (Mod.constant C4b.Red) (Mod.constant 0.15)
                                    |> Sg.shader {
                                        do! DefaultSurfaces.trafo
                                        do! DefaultSurfaces.vertexColor
                                        do! DefaultSurfaces.simpleLighting
                                        }
                                    |> Sg.noEvents
                                    |> Sg.trafo trafo

                        [b; s] |> Sg.ofList 
                               |> Sg.fillMode model.rendering.fillMode
                               |> Sg.cullMode model.rendering.cullMode    
                    )

                div [style "width:35%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]

                    //Html.SemUi.accordion "Navigation" "Compass" true [
                    //    NavigationProperties.view model.navigation |> UI.map NavigationAction 
                    //]
                ]
            ]
        )

    let initial =
        {
            camera = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
            rendering = { InitValues.rendering with cullMode = CullMode.None }            
        }

    let app : App<OrbitCameraDemoModel, MOrbitCameraDemoModel, Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

module NavigationModeDemo = 
    open PRo3DModels
    open Aardvark.Base    
    open Demo.TestApp.Mutable.MCameraControllerState
    
    type Action =
        | ArcBallAction     of ArcBallController.Message
        | FreeFlyAction     of CameraController.Message
        | RenderingAction   of RenderingProperties.Action        
        | NavigationAction  of NavigationProperties.Action

    let update (model : NavigationModeDemoModel) (act : Action) =
        match act with
            | ArcBallAction a -> 
                { model with camera = ArcBallController.update model.camera a }
            | FreeFlyAction a ->
                { model with camera = CameraController.update model.camera a }
            | RenderingAction a ->
                { model with rendering = RenderingProperties.update model.rendering a }       
            | NavigationAction a ->
                { model with navigation = NavigationProperties.update model.navigation a }

    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MNavigationModeDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
        
        let controller = 
            model.navigation.navigationMode 
                |> Mod.map (function 
                    | NavigationMode.FreeFly -> CameraController.controlledControl model.camera FreeFlyAction frustum
                    | NavigationMode.ArcBall -> ArcBallController.controlledControl model.camera ArcBallAction frustum
                    | _ -> CameraController.controlledControl model.camera FreeFlyAction frustum
                )

        let scene =
            let color = Mod.constant C4b.Blue
            let boxGeometry = Box3d(-V3d.III, V3d.III)
            let box = Mod.constant (boxGeometry)                       
                        
            let trafo = 
                model.camera.orbitCenter 
                    |> Mod.map (function
                        | Some x -> Trafo3d.Translation x
                        | None   -> Trafo3d.Identity
                    )

            let b = Sg.box color box                            
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                            }
                        |> Sg.noEvents
                        |> Sg.pickable (PickShape.Box boxGeometry)
                        |> Sg.withEvents [
                                Sg.onDoubleClick (fun p -> ArcBallController.Message.Pick p) ] |> Sg.map ArcBallAction                                    

            let s = Sg.sphere 20 (Mod.constant C4b.Red) (Mod.constant 0.15)
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                            }
                        |> Sg.noEvents
                        |> Sg.trafo trafo
                        |> Sg.fillMode model.rendering.fillMode
                        |> Sg.cullMode model.rendering.cullMode    

            [b; s]  |> Sg.ofList 
                    |> Sg.fillMode model.rendering.fillMode
                    |> Sg.cullMode model.rendering.cullMode      

        
        let renderControlAttributes =
            amap {
                let! state = model.navigation.navigationMode 
                match state with
                    | NavigationMode.FreeFly -> yield! CameraController.extractAttributes model.camera FreeFlyAction frustum
                    | NavigationMode.ArcBall -> yield! ArcBallController.extractAttributes model.camera ArcBallAction frustum
                    | _ -> failwith "Invalid NavigationMode"
            } |> AttributeMap.ofAMap
        
        require (Html.semui @ [myCss]) ( 
           div [clazz "ui"; style "background: #1B1C1E"] [
                    Incremental.renderControl 
                            (Mod.map2 Camera.create model.camera.view frustum) 
                            (AttributeMap.unionMany [
                                renderControlAttributes 
                                [attribute "style" "width:65%; height: 100%; float: left;"] |> AttributeMap.ofList
                            ])
                            scene

                    div [style "width:35%; height: 100%; float:right"] [
                        Html.SemUi.accordion "Rendering" "configure" true [
                            RenderingProperties.view model.rendering |> UI.map RenderingAction 
                        ]

                        Html.SemUi.accordion "Navigation" "Compass" true [
                            NavigationProperties.view model.navigation |> UI.map NavigationAction 
                        ]
                    ]
                ]
        )


    let initial =
        {
            camera = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
            rendering = { InitValues.rendering with cullMode = CullMode.None }
            navigation = InitValues.navigation
        }

    let app : App<NavigationModeDemoModel, MNavigationModeDemoModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map ArcBallAction
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

module BoxSelectionDemo = 
    open PRo3DModels
    open Aardvark.Base
    
    type Action = BoxSelectionDemoAction

    let update (model : BoxSelectionDemoModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }          
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
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
                let box = Primitives.mkNthBox i (i+1) |> Primitives.mkVisibleBox Primitives.colors.[i % 5]
                                         
                { model with boxes = PList.append box model.boxes }
            | RemoveBox ->  
                let i = model.boxes.Count - 1
                let boxes = PList.removeAt i model.boxes

                {model with boxes = boxes}
            | ClearSelection -> { model with selectedBoxes = HSet.empty}
                        
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let mkColor (model : MBoxSelectionDemoModel) (box : MVisibleBox) =
        let id = box.id |> Mod.force

        let color =  
            model.selectedBoxes 
                |> ASet.contains id 
                |> Mod.bind (function x -> if x then Mod.constant Primitives.selectionColor else box.color)

        let color = model.boxHovered |> Mod.bind (function x -> match x with
                                                                | Some k -> if k = id then Mod.constant Primitives.hoverColor else color
                                                                | None -> color)

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
                    Sg.onClick (fun _ -> Select (box.id |> Mod.force))
                    Sg.onEnter (fun _ -> Enter (box.id |> Mod.force))
                    Sg.onLeave (fun () -> Exit)
                ]

    let view (model : MBoxSelectionDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
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

                div [style "width:35%; height: 100%; float:right"] [
                    //Html.SemUi.accordion "Rendering" "configure" true [
                    //    RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    //]  
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
                                    
                                    yield div [clazz "item"; style bgc; 
                                               onClick(fun _ -> Select (b.id |> Mod.force))
                                               onMouseEnter(fun _ -> Enter (b.id |> Mod.force))
                                               onMouseLeave(fun _ -> Exit)] [
                                         i [clazz "medium File Outline middle aligned icon"][]
                                    ]                                                                    
                            }     
                    )
                ]
            ]
        )
             
    let initial =
        {
            camera           = CameraController.initial            
            rendering        = InitValues.rendering            
            boxHovered = None
            boxes = Primitives.mkBoxes 3 |> List.mapi (fun i k -> Primitives.mkVisibleBox Primitives.colors.[i % 5] k) |> PList.ofList
            selectedBoxes = HSet.empty            
        }

    let app : App<BoxSelectionDemoModel,MBoxSelectionDemoModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app

module DrawingApp = 
    open PRo3DModels
    open Aardvark.Base
    
    type Action =
        | CameraMessage    of CameraController.Message
        | AnnotationAction of AnnotationProperties.Action
        | RenderingAction  of RenderingProperties.Action
        | Enter of string
        | Exit      

    let update (model : ComposedViewerModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }
            | AnnotationAction a ->
                 { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | Enter id-> { model with boxHovered = Some id }
            | Exit -> { model with boxHovered = None }    
            
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        
                        let boxes = Primitives.mkBoxes 5
                                              
                        let boxes = boxes 
                                    |> List.mapi (fun i k -> Primitives.mkVisibleBox Primitives.colors.[i] k)
                                    |> List.map (fun k -> Primitives.mkISgBox model k 
                                                                |> Sg.pickable (PickShape.Box k.geometry)
                                                                |> Sg.withEvents [
                                                                        Sg.onEnter (fun p -> Enter k.id)
                                                                        Sg.onLeave (fun () -> Exit)]
                                                )
                        boxes
                            |> Sg.ofList
                            |> Sg.noEvents     
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                            |> Sg.noEvents                        
                )

                div [style "width:35%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]
                    Html.SemUi.accordion "Properties" "options" true [
                        AnnotationProperties.view model.singleAnnotation |> UI.map AnnotationAction
                    ]
                ]
            ]
        )

    let initial =
        {
            camera           = CameraController.initial
            singleAnnotation = InitValues.annotation
            rendering        = InitValues.rendering
            
            boxHovered = None
        }

    let app : App<ComposedViewerModel,MComposedViewerModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> CameraController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app
