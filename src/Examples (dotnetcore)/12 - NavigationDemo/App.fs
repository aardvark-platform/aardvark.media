module App


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph

open Aardvark.UI
open Aardvark.UI.Primitives

open RenderingParametersModel
open Model

type Action =
    | ArcBallAction     of ArcBallController.Message
    | FreeFlyAction     of FreeFlyController.Message
    | RenderingAction   of RenderingParametersModel.Action        
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
            { model with camera = FreeFlyController.update model.camera a }
        | RenderingAction a ->
            { model with rendering = RenderingParameters.update model.rendering a }       
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


let view (model : MNavigationModeDemoModel) =
    let cam =
        model.camera.view 
            
    let frustum =
        Mod.constant (Frustum.perspective 60.0 0.1 100.0 1.0)
        
    //let controller = 
    //    model.navigation.navigationMode 
    //        |> Mod.map (function 
    //            | NavigationMode.FreeFly ->FreeFlyController.controlledControl model.camera FreeFlyAction frustum
    //            | NavigationMode.ArcBall -> ArcBallController.controlledControl model.camera ArcBallAction frustum
    //            | _ ->FreeFlyController.controlledControl model.camera FreeFlyAction frustum
    //        )

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
                    |> Sg.requirePicking
                    |> Sg.noEvents
                    //|> Sg.pickable (PickShape.Box boxGeometry)
                    |> Sg.withEvents [
                            Sg.onDoubleClick (fun p -> ArcBallController.Message.Pick p) ] |> Sg.map ArcBallAction                                    

        let s = Sg.sphere 4 (Mod.constant C4b.Red) (Mod.constant 0.15)
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
                | NavigationMode.FreeFly -> yield! FreeFlyController.extractAttributes model.camera FreeFlyAction
                | NavigationMode.ArcBall -> yield! ArcBallController.extractAttributes model.camera ArcBallAction 
                | _ -> failwith "Invalid NavigationMode"
        } |> AttributeMap.ofAMap
        
    require Html.semui ( 
        div [clazz "ui"; style "background: #1B1C1E"] [
                yield 
                    Incremental.renderControl 
                        (Mod.map2 Camera.create model.camera.view frustum) 
                        (AttributeMap.unionMany [
                            renderControlAttributes 
                                
                            [
                                attribute "style" "width:65%; height: 100%; float: left"
                                attribute "data-renderalways" "true"
                                attribute "showFPS" "true"
                                attribute "data-samples" "8"
                                onKeyDown (KeyDown)
                                onKeyUp (KeyUp) ] |> AttributeMap.ofList
                        ])
                        scene

                let renderingAcc = 
                    Html.SemUi.accordion "Rendering" "configure" true [
                        RenderingParameters.view model.rendering |> UI.map RenderingAction 
                    ]

        //         Html.table [                            
        //   Html.row "Mode:" [Html.SemUi.dropDown model.navigationMode SetNavigationMode]
        //]
                let cameracontroller (ccs : MCameraControllerState) = 
                    Html.table [  
                        Html.row "Sensitivity:" [Incremental.text (ccs.sensitivity |> Mod.map (fun x -> sprintf "%f" x))]
                        Html.row "ZoomFactor:"  [Incremental.text (ccs.zoomFactor  |> Mod.map (fun x -> sprintf "%f" x))]
                        Html.row "PanFactor:"   [Incremental.text (ccs.panFactor   |> Mod.map (fun x -> sprintf "%f" x))]                            
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
                    Html.SemUi.tabbed [clazz "ui inverted segment"; style "width:35%; height: 100%; float:right; margin: 0; border-radius: 0;" ] [
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
        rendering       = { RenderingParameters.initial with cullMode = CullMode.None }
        navigation      = NavigationParameters.initial        
        zoomFactor      = initFactors
        panFactor       = initFactors
        navsensitivity  = initNavSens
    }

let threads model =  
  match model.navigation.navigationMode with
    | NavigationMode.FreeFly -> FreeFlyController.threads model.camera |> ThreadPool.map FreeFlyAction
    | NavigationMode.ArcBall -> ArcBallController.threads model.camera |> ThreadPool.map ArcBallAction
    | _ -> failwith "invalid navmode"

let app =
    {
        unpersist = Unpersist.instance
        threads   = threads            
        initial   = initial
        update    = update
        view      = view
    }

