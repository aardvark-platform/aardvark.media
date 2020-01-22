namespace UI.Composed

open System
open Aardvark.UI
open Aardvark.UI.Primitives
open FSharp.Data.Adaptive
open Aardvark.SceneGraph.AirState
open Demo

open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.Rendering.Text

open PRo3DModels
open Demo.TestApp
open Demo.TestApp.Mutable

module Mod =    

    let bindOption (m : aval<Option<'a>>) (defaultValue : 'b) (project : 'a -> aval<'b>)  : aval<'b> =
        m |> AVal.bind (function | None   -> AVal.constant defaultValue       
                                | Some v -> project v)

module Primitives =
    
    let hoverColor = C4b.Blue
    let selectionColor = C4b.Red
    let colors = [new C4b(166,206,227); new C4b(178,223,138); new C4b(251,154,153); new C4b(253,191,111); new C4b(202,178,214)]
    let colorsBlue = [new C4b(241,238,246); new C4b(189,201,225); new C4b(116,169,207); new C4b(43,140,190); new C4b(4,90,141)]

    let mkNthBox i n = 
        let min = -V3d.One
        let max =  V3d.One

        let offset = 0.0 * (float n) * V3d.IOO

        new Box3d(min + V3d.IOO * 2.5 * (float i) - offset, max + V3d.IOO * 2.5 * (float i) - offset)

    let mkBoxes number =        
        [0..number-1] |> List.map (function x -> mkNthBox x number)

    let hoveredColor (model : MComposedViewerModel) (box : VisibleBox) =
        model.boxHovered |> AVal.map (fun h -> match h with
                                                | Some i -> if i = box.id then hoverColor else box.color
                                                | None -> box.color)

    let mkVisibleBox (color : C4b) (box : Box3d) : VisibleBox = 
        {
            id = Guid.NewGuid().ToString()
            geometry = box
            color = color           
        }

    let mkISgBox (model : MComposedViewerModel) (box : VisibleBox) =
        let box' = AVal.constant (box.geometry)
        let c = hoveredColor model box

        Sg.box c box'
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                    }
                |> Sg.noEvents    
    
module SimpleCompositionViewer =     
    
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
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
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
    
    type Action =
        | CameraMessage     of ArcBallController.Message
        | RenderingAction   of RenderingProperties.Action                
        | V3dMessage        of Vector3d.Action
        | ColorMessage      of ColorPicker.Action
        | ChangeSensitivity of Numeric.Action

    let update (model : OrbitCameraDemoModel) (act : Action) =
        match act with
            | CameraMessage a -> 
                let model = 
                    { model with camera = ArcBallController.update model.camera a}

                match model.camera.orbitCenter with
                    | Some c -> { model with orbitCenter = Vector3d.updateV3d model.orbitCenter c}
                    | None -> model
                
            | RenderingAction a ->
                { model with rendering = RenderingProperties.update model.rendering a }
            | V3dMessage a ->
                let orbitCenter = Vector3d.update model.orbitCenter a
                
                let cam = { 
                    model.camera with 
                            orbitCenter = Some orbitCenter.value
                            view = CameraView.lookAt model.camera.view.Location orbitCenter.value model.camera.view.Up
                }

                { model with orbitCenter = orbitCenter; camera = cam}
            | ColorMessage a ->
                { model with color = ColorPicker.update model.color a}
            | ChangeSensitivity a ->               
                let sense = Numeric.update model.navsensitivity a
                { model with navsensitivity = sense; camera = { model.camera with sensitivity = sense.value } }

    let view (model : MOrbitCameraDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
            
        require (Html.semui) (
            div [clazz "ui"; style "background-color: #1B1C1E"] [
                ArcBallController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                        attribute "style" "width:65%; height: 100%; float: left;"
                    ])
                    (
                      
                        let boxGeometry = Box3d(-V3d.III, V3d.III)
                        let box = AVal.constant (boxGeometry)   
                        
                        //let trafo = 
                        //    model.camera.orbitCenter 
                        //        |> AVal.bind (fun center -> match center with 
                        //                                    | Some x -> AVal.constant (Trafo3d.Translation x)
                        //                                    | None -> AVal.constant (Trafo3d.Identity))

                        let trafo = model.orbitCenter.value |> AVal.map(fun d -> Trafo3d.Translation d)

                        let b = Sg.box model.color.c box
                                    |> Sg.shader {
                                        do! DefaultSurfaces.trafo
                                        do! DefaultSurfaces.vertexColor
                                        do! DefaultSurfaces.simpleLighting
                                        }
                                    |> Sg.noEvents
                                    |> Sg.pickable (PickShape.Box boxGeometry)
                                    |> Sg.withEvents [
                                            Sg.onDoubleClick (fun p -> ArcBallController.Message.Pick p) ] |> Sg.map CameraMessage

                        let s = Sg.sphere 5 (AVal.constant C4b.Red) (AVal.constant 0.15)
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

                div [style "width:35%; height: 100%; float:right; background-color: #1B1C1E"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]

                    Html.SemUi.accordion "ExploreCenter" "Compass" true [
                        //let attributes =
                        //    amap {
                        //        let! center = model.camera.orbitCenter
                        //        let center =
                        //            match center with
                        //                | Some c -> c
                        //                | None   -> V3d.NaN

                        //        let input = Vector3d.initV3d center
                        //    }
                        //AVal.bindOption model.dnsResults Double.NaN (fun a -> a.dipAngle)                       

                        yield Incremental.div AttributeMap.empty (
                            AList.ofList [Vector3d.view model.orbitCenter |> UI.map V3dMessage]
                        );
                        yield ColorPicker.view model.color |> UI.map ColorMessage

                        yield Numeric.view model.navsensitivity |> UI.map ChangeSensitivity
                    ]
                ]
            ]
        )

    let initNavSens = {
        value   = 0.0
        min     = -5.0
        max     = +5.0
        step    = 0.25
        format  = "{0:0.00}"
    }

    let initial =
        {
            camera = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
            rendering = { InitValues.rendering with cullMode = CullMode.None }    
            orbitCenter = Vector3d.init
            color = { c = C4b.VRVisGreen}
            navsensitivity = initNavSens
          //  navigation = { navigationMode = NavigationMode.FreeFly }
        }

    let app : App<OrbitCameraDemoModel, MOrbitCameraDemoModel, Action> =
        {
            unpersist   = Unpersist.instance
            threads     = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial     = initial
            update      = update
            view        = view
        }

    let start () = App.start app

module NavigationModeDemo = 
    
    type Action =
        | ArcBallAction     of ArcBallController.Message
        | FreeFlyAction     of CameraController.Message
        | RenderingAction   of RenderingProperties.Action        
        | NavigationAction  of NavigationProperties.Action
        | ChangeSensitivity of Numeric.Action
        | ChangePanFactor   of Numeric.Action
        | ChangeZoomFactor  of Numeric.Action       
        | KeyDown                   of key : Aardvark.Application.Keys
        | KeyUp                     of key : Aardvark.Application.Keys      

    let update (model : NavigationModeDemoModel) (act : Action) =
        match act with            
            | ArcBallAction a -> 
                let model = 
                    match a with 
                        | ArcBallController.Message.Pick a ->
                            let navParams = { navigationMode = NavigationMode.ArcBall }
                            { model with navigation = navParams }
                        | _ -> model
                        
                { model with camera = ArcBallController.update model.camera a }
            | FreeFlyAction a ->
                { model with camera = CameraController.update model.camera a }
            | RenderingAction a ->
                { model with rendering = RenderingProperties.update model.rendering a }       
            | NavigationAction a ->
                { model with navigation = NavigationProperties.update model.navigation a }
            | ChangeSensitivity a ->               
                let sense = Numeric.update model.navsensitivity a
                { model with navsensitivity = sense; camera = { model.camera with sensitivity = sense.value } }
            | ChangeZoomFactor a ->               
                let zoom = Numeric.update model.zoomFactor a
                { model with zoomFactor = zoom; camera = { model.camera with zoomFactor = zoom.value } }
            | ChangePanFactor a ->               
                let pan = Numeric.update model.panFactor a
                { model with panFactor = pan; camera = { model.camera with panFactor = pan.value } }
            | KeyDown k -> 
                let (a : Numeric.Action) =
                    match k with 
                        | Aardvark.Application.Keys.PageUp -> Numeric.Action.SetValue (model.camera.sensitivity + 0.5)
                        | Aardvark.Application.Keys.PageDown -> Numeric.Action.SetValue (model.camera.sensitivity - 0.5)
                        | _ -> Numeric.Action.SetValue (model.camera.sensitivity)

                let sense = Numeric.update model.navsensitivity a
                { model with navsensitivity = sense; camera = { model.camera with sensitivity = sense.value } }
            | KeyUp k -> model

    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let view (model : MNavigationModeDemoModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
        
        //let controller = 
        //    model.navigation.navigationMode 
        //        |> AVal.map (function 
        //            | NavigationMode.FreeFly -> CameraController.controlledControl model.camera FreeFlyAction frustum
        //            | NavigationMode.ArcBall -> ArcBallController.controlledControl model.camera ArcBallAction frustum
        //            | _ -> CameraController.controlledControl model.camera FreeFlyAction frustum
        //        )

        let scene =
            let color = AVal.constant C4b.Blue
            let boxGeometry = Box3d(-V3d.III, V3d.III)
            let box = AVal.constant (boxGeometry)                       
                        
            let trafo = 
                model.camera.orbitCenter 
                    |> AVal.map (function
                        | Some x -> Trafo3d.Translation x
                        | None   -> Trafo3d.Identity
                    )

            let b = Sg.box color box                            
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                            }
                        |> Sg.requirePicking
                        |> Sg.noEvents
                        //|> Sg.pickable (PickShape.Box boxGeometry)
                        |> Sg.withEvents [
                                Sg.onDoubleClick (fun p -> ArcBallController.Message.Pick p) ] |> Sg.map ArcBallAction                                    

            let s = Sg.sphere 4 (AVal.constant C4b.Red) (AVal.constant 0.15)
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
                    yield 
                        Incremental.renderControl 
                            (AVal.map2 Camera.create model.camera.view frustum) 
                            (AttributeMap.unionMany [
                                renderControlAttributes 
                                
                                [
                                    attribute "style" "width:65%; height: 100%; float: left;"
                                    onKeyDown (KeyDown)
                                    onKeyUp (KeyUp) ] |> AttributeMap.ofList
                            ])
                            scene

                    let renderingAcc = 
                        Html.SemUi.accordion "Rendering" "configure" true [
                            RenderingProperties.view model.rendering |> UI.map RenderingAction 
                        ]

            //         Html.table [                            
            //   Html.row "Mode:" [Html.SemUi.dropDown model.navigationMode SetNavigationMode]
            //]
                    let cameracontroller (ccs : MCameraControllerState) = 
                        Html.table [  
                            Html.row "Sensitivity:" [Incremental.text (ccs.sensitivity |> AVal.map (fun x -> sprintf "%f" x))]
                            Html.row "ZoomFactor:"  [Incremental.text (ccs.zoomFactor  |> AVal.map (fun x -> sprintf "%f" x))]
                            Html.row "PanFactor:"   [Incremental.text (ccs.panFactor   |> AVal.map (fun x -> sprintf "%f" x))]                            
                        ]

                    let navigationAcc = 
                        Html.SemUi.accordion "Navigation" "Compass" true [
                            NavigationProperties.view model.navigation |> UI.map NavigationAction 
                            Html.table [  
                                Html.row "Sensitivity:" [Numeric.view' [InputBox;] model.navsensitivity |> UI.map ChangeSensitivity]
                                Html.row "ZoomFactor:"  [Numeric.view' [InputBox;] model.zoomFactor     |> UI.map ChangeZoomFactor]
                                Html.row "PanFactor:"   [Numeric.view' [InputBox;] model.panFactor      |> UI.map ChangePanFactor]                                
                            ]
                            cameracontroller model.camera
                        ]

                   
                                        
                    yield 
                        Html.SemUi.tabbed [clazz "ui inverted segment"; style "width:35%; height: 100%; float:right" ] [
                            ("Rendering", renderingAcc)
                            ("Navigation", navigationAcc)
                           // ("Debug", cameracontroller model.camera)
                        ] "Navigation"
                ]
        )
    
    let initNavSens = {
        value   = 0.0
        min     = -5.0
        max     = +5.0
        step    = 0.25
        format  = "{0:0.00}"
    }

    let initFactors = {
        value   = 0.001
        min     = 0.000000001
        max     = 1000000.0
        step    = 0.1
        format  = "{0:0.00000000}"
    }

    let initial : NavigationModeDemoModel =
        {
            camera          = { ArcBallController.initial with orbitCenter = Some V3d.Zero }
            rendering       = { InitValues.rendering with cullMode = CullMode.None }
            navigation      = InitValues.navigation        
            zoomFactor      = initFactors
            panFactor       = initFactors
            navsensitivity  = initNavSens
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
    
    type Action = BoxSelectionDemoAction

    let update (model : BoxSelectionDemoModel) (act : Action) =
        
        match act with
            | CameraMessage m -> 
                 { model with camera = CameraController.update model.camera m }          
            | RenderingAction a ->
                 { model with rendering = RenderingProperties.update model.rendering a }
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
                let box = Primitives.mkNthBox i (i+1) |> Primitives.mkVisibleBox Primitives.colors.[i % 5]
                                         
                { model with boxes = IndexList.append box model.boxes }
            | RemoveBox ->  
                let i = model.boxes.Count - 1
                let boxes = IndexList.removeAt i model.boxes

                {model with boxes = boxes}
            | ClearSelection -> { model with selectedBoxes = HashSet.empty}
                        
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let mkColor (model : MBoxSelectionDemoModel) (box : MVisibleBox) =
        let id = box.id |> AVal.force

        let color =  
            model.selectedBoxes 
                |> ASet.contains id 
                |> AVal.bind (function x -> if x then AVal.constant Primitives.selectionColor else box.color)

        let color = model.boxHovered |> AVal.bind (function x -> match x with
                                                                | Some k -> if k = id then AVal.constant Primitives.hoverColor else color
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
                    Sg.onClick (fun _ -> Select (box.id |> AVal.force))
                    Sg.onEnter (fun _ -> Enter (box.id |> AVal.force))
                    Sg.onLeave (fun () -> Exit)
                ]

    let view (model : MBoxSelectionDemoModel) =
        let cam =
            model.camera.view 
                           
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
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
                                               onClick(fun _ -> Select (b.id |> AVal.force))
                                               onMouseEnter(fun _ -> Enter (b.id |> AVal.force))
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
            boxes = Primitives.mkBoxes 3 |> List.mapi (fun i k -> Primitives.mkVisibleBox Primitives.colors.[i % 5] k) |> IndexList.ofList
            selectedBoxes = HashSet.empty         
            boxesSet = HashSet.empty
            boxesMap = HashMap.empty
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

module SimpleDrawingApp = 
        
    type Action =
        | CameraMessage    of ArcBallController.Message
        //| AnnotationAction of AnnotationProperties.Action
        | RenderingAction  of RenderingProperties.Action
        | Move of V3d
        | AddPoint of V3d
        | KeyDown of key : Keys
        | KeyUp of key : Keys
        | Exit      

    let update (model : SimpleDrawingAppModel) (act : Action) =
        match act, model.draw with
            | CameraMessage m, false -> { model with camera = ArcBallController.update model.camera m }
            //| AnnotationAction a ->
            //     { model with singleAnnotation = AnnotationProperties.update model.singleAnnotation a }
            | RenderingAction a, _ ->
                 { model with rendering = RenderingProperties.update model.rendering a }
            | KeyDown Keys.LeftCtrl, _ -> { model with draw = true }
            | KeyUp Keys.LeftCtrl, _ -> { model with draw = false; hoverPosition = None }
            | Move p, true -> { model with hoverPosition = Some (Trafo3d.Translation p) }
            | AddPoint p, true -> { model with points = model.points |> List.append [p] }
            | Exit, _ -> { model with hoverPosition = None }
            | _ -> model
            
            
    let myCss = { kind = Stylesheet; name = "semui-overrides"; url = "semui-overrides.css" }

    let computeScale (view : aval<CameraView>)(p:V3d)(size:float) =        
        view 
            |> AVal.map (function v -> 
                                    let distV = p - v.Location
                                    let distF = V3d.Dot(v.Forward, distV)
                                    distF * size / 800.0)

    let mkISg color size trafo =         
        Sg.sphere 5 color size 
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.noEvents
                |> Sg.trafo(trafo) 
        
    let canvas =  
        let b = new Box3d( V3d(-2.0,-0.5,-2.0), V3d(2.0,0.5,2.0) )                                               
        Sg.box (AVal.constant Primitives.colorsBlue.[0]) (AVal.constant b)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.requirePicking
            |> Sg.noEvents 
                |> Sg.withEvents [
                    Sg.onMouseMove (fun p -> Move p)
                    Sg.onClick(fun p -> AddPoint p)
                    Sg.onLeave (fun _ -> Exit)
                ]    

    let edgeLines (close : bool)  (points : aval<list<V3d>>) =        
        points 
            |> AVal.map (
                function k -> 
                            let head = k |> List.tryHead
                            match head with
                                    | Some h -> if close then k @ [h] else k
                                                    |> List.pairwise
                                                    |> List.map (fun (a,b) -> new Line3d(a,b)) 
                                                    |> Array.ofList                                                        
                                    | None -> [||])

    let view (model : MSimpleDrawingAppModel) =
        let cam =
            model.camera.view 
            
        let frustum =
            AVal.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
      
        require (Html.semui) (
            div [clazz "ui"; style "background: #1B1C1E"] [
                ArcBallController.controlledControl model.camera CameraMessage frustum
                    (AttributeMap.ofList [
                                onKeyDown (KeyDown)
                                onKeyUp (KeyUp)
                                attribute "style" "width:65%; height: 100%; float: left;"]
                    )
                    (        
                        
                        let x = 
                            edgeLines false model.points 
                                |> Sg.lines (AVal.constant Primitives.colorsBlue.[2])
                                |> Sg.noEvents
                                |> Sg.uniform "LineWidth" (AVal.constant 5) 
                                |> Sg.effect [
                                    toEffect DefaultSurfaces.trafo
                                    toEffect DefaultSurfaces.vertexColor
                                    toEffect DefaultSurfaces.thickLine
                                    ]
                                |> Sg.pass (RenderPass.after "lines" RenderPassOrder.Arbitrary RenderPass.main)
                                |> Sg.depthTest (AVal.constant DepthTestMode.None)

                                                

                        let spheres =
                            model.points 
                                |> AVal.map(function ps -> ps |> List.map (function p -> mkISg (AVal.constant Primitives.colorsBlue.[3])
                                                                                              (computeScale model.camera.view p 5.0)
                                                                                              (AVal.constant (Trafo3d.Translation(p)))) 
                                                             |> Sg.ofList)                                
                                |> Sg.dynamic                            
                                              
                        let trafo = 
                            model.hoverPosition 
                                |> AVal.map (function o -> match o with 
                                                            | Some t-> t
                                                            | None -> Trafo3d.Scale(V3d.Zero))

                        let brush = mkISg (AVal.constant C4b.Red) (AVal.constant 0.05) trafo
                                                            
                        [canvas; brush; spheres; x]
                            |> Sg.ofList
                            |> Sg.fillMode model.rendering.fillMode
                            |> Sg.cullMode model.rendering.cullMode                                                                                           
                )

                div [style "width:35%; height: 100%; float:right"] [
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingProperties.view model.rendering |> UI.map RenderingAction 
                    ]
                    ]
                ]
        )

    let initial =
        {
            camera           = { ArcBallController.initial with view = CameraView.lookAt (6.0 * V3d.OIO) V3d.Zero V3d.OOI}
            rendering        = InitValues.rendering
            hoverPosition = None
            draw = false
            points = []
        }

    let app : App<SimpleDrawingAppModel,MSimpleDrawingAppModel,Action> =
        {
            unpersist = Unpersist.instance
            threads = fun model -> ArcBallController.threads model.camera |> ThreadPool.map CameraMessage
            initial = initial
            update = update
            view = view
        }

    let start () = App.start app
