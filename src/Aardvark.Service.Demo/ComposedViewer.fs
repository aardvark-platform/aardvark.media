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
        
    let hoveredColor (model : MComposedViewerModel) (box : VisibleBox) =
        model.boxHovered |> Mod.map (fun h -> match h with
                                                | Some i -> if i = box.id then C4b.Green else box.color
                                                | None -> box.color)
    
    let mkVisibleBox (color : C4b) (box : Box3d)= 
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
                |> Sg.pickable (PickShape.Box box.geometry)
                |> Sg.withEvents [
                        Sg.onEnter (fun p -> Enter box.id)
                        Sg.onLeave (fun () -> Exit)]                    

    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MComposedViewerModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

      //  require (Html.semui @ [myCss]) (
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        let colors = [C4b.Red; C4b.Blue]
                        let boxes = [Box3d(-V3d.III, V3d.III); Box3d(-V3d.III + V3d.IOO * 2.5, V3d.III + V3d.IOO * 2.5)]
                                              
                        let boxes = boxes 
                                    |> List.mapi (fun i k -> mkVisibleBox colors.[i] k)
                                    |> List.map (fun k -> mkISgBox model k)                                                    
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
    
    type Action =
        | CameraMessage    of CameraController.Message        
        | RenderingAction  of RenderingProperties.Action
        | Select of string     
        | Enter of string
        | Exit      

    let update (model : BoxSelectionDemoModel) (act : Action) =
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }          
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | Select id-> { model with boxHovered = Some id }           
            | Enter id-> { model with boxHovered = Some id }
            | Exit -> { model with boxHovered = None }    
        
    let selectionColor (model : MComposedViewerModel) (box : VisibleBox) =
        model.boxHovered |> Mod.map (fun h -> match h with
                                                | Some i -> if i = box.id then C4b.Green else box.color
                                                | None -> box.color)
    
    let mkVisibleBox (color : C4b) (box : Box3d)= 
        {
            id = Guid.NewGuid().ToString()
            geometry = box
            color = color
        }

    let mkISgBox (model : MComposedViewerModel) (box : VisibleBox) =
        let box' = Mod.constant (box.geometry)
        let c = selectionColor model box

        Sg.box c box'
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                    }
                |> Sg.noEvents
                |> Sg.pickable (PickShape.Box box.geometry)
                |> Sg.withEvents [
                        Sg.onEnter (fun p -> Enter box.id)
                        Sg.onLeave (fun () -> Exit)]                    

    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MBoxSelectionDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)

      //  require (Html.semui @ [myCss]) (
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                CameraController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                        let color = Mod.constant C4b.Blue
                        let boxGeometry = Box3d(-V3d.III, V3d.III)
                        let box = Mod.constant (boxGeometry)              
                                                               
                        Sg.box color box                            
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                            }
                        |> Sg.noEvents   
                )

                div [style "width:35%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]                    
                ]
            ]
        )

    let colors = [C4b.Red; C4b.Blue]                  
    
    let boxess = [Box3d(-V3d.III, V3d.III); Box3d(-V3d.III + V3d.IOO * 2.5, V3d.III + V3d.IOO * 2.5)] 
   
    let initial =
        {
            camera           = CameraController.initial            
            rendering        = InitValues.rendering
            
            boxHovered = None
            boxes = boxess |> List.mapi (fun i k -> mkVisibleBox colors.[i] k)                  
            selectedBoxes = []            
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