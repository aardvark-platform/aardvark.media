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

module TestApp =

    type Message =
        | AddButton of Index * string
        | Hugo of list<string>
        | ToggleD3Hate
        | Enter 
        | Exit
        | Scale
        | ResetScale

        | StartDrag
        | StopDrag
        | MoveRay of RayPart

    let initial =
        {
            lastName = None
            elements = PList.ofList ["A"; "B"; "C"]
            hasD3Hate = true
            boxHovered = false
            boxScale = 1.0
            dragging = false
        }

    let update (m : Model) (msg : Message) =
        match msg with
            | AddButton(before, str) -> 
                { m with lastName = Some str; elements = PList.remove before m.elements }
            | Hugo l ->
                let res : list<string> = l |> List.map Pickler.json.UnPickleOfString
                printfn "%A" res
                m
            | ToggleD3Hate ->
                { m with hasD3Hate = not m.hasD3Hate }
            | Enter ->
                { m with boxHovered = true }
            | Exit ->
                { m with boxHovered = false }
            | Scale ->
                { m with boxScale = 1.3 * m.boxScale } 
            | ResetScale ->
                { m with boxScale = 1.0 }

            | StartDrag ->
                printfn "start"
                { m with dragging = true }

            | StopDrag ->
                printfn "stop"
                { m with dragging = false }

            | MoveRay r ->
                printfn "move"
                m

    let clazz str = attribute "class" str

    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]

    let view (m : MModel) =
        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        let cam = Mod.constant (Camera.create view frustum)

        div' [style { backgroundColor C4b.Red }; attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [

            require semui  [
                div' [ attribute "class" "ui visible sidebar inverted vertical menu"; attribute "style" "min-width: 250px" ] [
                    div' [ clazz "item"] [ 
                        b' [] [text' "Instructions"]
                        div' [ clazz "menu" ] [
                            span' [clazz "item"] [text' "use typical WASD bindings to navigate"]
                            span' [clazz "item"] [text' "hover the box to see its highlighting"]
                            span' [clazz "item"] [text' "double click the box to persistently enlarge it"]
                        ]
                    ]

                    div' [ clazz "item"] [ 
                        b' [] [text' "Actions"]
                        div' [ clazz "menu" ] [
                            div' [ clazz "item "] [ 
                                button' [clazz "ui button tiny"; onClick (fun () -> Scale) ] [text' "increase scale"]
                            ]
                            div' [ clazz "item "] [ 
                                button' [ clazz "ui button tiny"; onClick (fun () -> ResetScale) ] [text' "reset scale"]
                            ]
                        ]
                    ]

                    div' [ clazz "item"] [ 
                        b' [] [text' "Status"]
                        div' [ clazz "menu" ] [
                            span' [clazz "item"; attribute "style" "color: red; font-weight: bold"] [ 
                                m.boxHovered |> Mod.map (function true -> "hovered" | false -> "not hovered") |> text 
                            ]
                            
                            div' [clazz "item"] [
                                span' [] [text' "current scale: "]
                                span' [attribute "style" "color: red; font-weight: bold"] [ m.boxScale |> Mod.map string |> text ]
                            ]
                        ]
                    ]
                ]
            ]

            renderControl
                cam
                (
                    AMap.ofList [
                        "style", (Mod.constant (Value "display: flex; width: 100%; height: 100%" |> Some))
                        onlyWhen m.dragging (onRayMove (fun r -> MoveRay r))
                        onlyWhen m.dragging (onMouseUp (fun b r -> StopDrag))
                    ] 
                    |> AMap.flattenM
                    |> AMap.choose (fun k v -> v)
                )
                (
                    let value = m.lastName |> Mod.map (function Some str -> str | None -> "yeah")

                    let baseBox = Box3d(-V3d.III, V3d.III)
                    let box = m.boxHovered |> Mod.map (fun h -> if h then baseBox.ScaledFromCenterBy(1.3) else baseBox)
                    let color = m.boxHovered |> Mod.map (fun h -> if h then C4b.Red else C4b.Green)

                    let box =
                        Sg.box color box
                            |> Sg.shader {
                                do! DefaultSurfaces.trafo
                                do! DefaultSurfaces.vertexColor
                                do! DefaultSurfaces.simpleLighting
                               }
                            |> Sg.noEvents
                            |> Sg.pickable (PickShape.Box baseBox)             

                    let sg = 
                        box |> Sg.trafo (m.boxScale |> Mod.map Trafo3d.Scale)
                            |> Sg.withEvents [
                                Sg.onenter (fun p -> Enter)
                                Sg.onleave (fun () -> Exit)
                                Sg.ondblclick (fun () -> Scale)
                                Sg.onmousedown MouseButtons.Left (fun _ -> StartDrag)
                            ]

                    Sg.ofList [
                        sg

                        Sg.markdown MarkdownConfig.light (m.lastName |> Mod.map (Option.defaultValue "yeah"))
                            |> Sg.noEvents
                            |> Sg.transform (Trafo3d.FromOrthoNormalBasis(-V3d.IOO, V3d.OOI, V3d.OIO))
                            |> Sg.translate 0.0 0.0 3.0
                    ]


            )

        ]

    let start (runtime : IRuntime) (port : int) =
        App.start runtime port {
            view = view
            update = update
            initial = initial
        }

module CameraController =
    open Aardvark.Base.Incremental.Operators

    type Message = 
        | Down of button : MouseButtons * pos : V2i
        | Up of button : MouseButtons
        | Move of V2i
        | StepTime
        | KeyDown of key : Keys
        | KeyUp of key : Keys

    let initial =
        {
            view = CameraView.lookAt (6.0 * V3d.III) V3d.Zero V3d.OOI
            dragStart = V2i.Zero
            look = false
            zoom = false
            pan = false
            moveVec = V3i.Zero
            lastTime = None
        }

    let sw = System.Diagnostics.Stopwatch()
    do sw.Start()

    let update (model : CameraControllerState) (message : Message) =
        match message with
            | StepTime ->
                let now = sw.Elapsed.TotalSeconds
                let cam = model.view

                let cam = 
                    match model.lastTime with
                        | Some last ->
                            let dt = now - last

                            let dir = 
                                cam.Forward * float model.moveVec.Z +
                                cam.Right * float model.moveVec.X +
                                cam.Sky * float model.moveVec.Y

                            if model.moveVec = V3i.Zero then
                                printfn "useless time %A" now

                            cam.WithLocation(model.view.Location + dir * 1.5 * dt)

                        | None -> 
                            cam


                { model with lastTime = Some now; view = cam }

            | KeyDown Keys.W | KeyUp Keys.S -> 
                { model with moveVec = model.moveVec + V3i(0, 0, 1); lastTime = Some sw.Elapsed.TotalSeconds }

            | KeyDown Keys.S | KeyUp Keys.W ->
                { model with moveVec = model.moveVec + V3i(0, 0, -1); lastTime = Some sw.Elapsed.TotalSeconds }
                
            | KeyDown Keys.A | KeyUp Keys.D ->
                { model with moveVec = model.moveVec + V3i(-1, 0, 0); lastTime = Some sw.Elapsed.TotalSeconds }

            | KeyDown Keys.D | KeyUp Keys.A ->
                { model with moveVec = model.moveVec + V3i(1, 0, 0); lastTime = Some sw.Elapsed.TotalSeconds }
                

            | KeyDown _ | KeyUp _ ->
                model


            | Down(button,pos) ->
                let model = { model with dragStart = pos }
                match button with
                    | MouseButtons.Left -> { model with look = true }
                    | MouseButtons.Middle -> { model with pan = true }
                    | MouseButtons.Right -> { model with zoom = true }
                    | _ -> model

            | Up button ->
                match button with
                    | MouseButtons.Left -> { model with look = false }
                    | MouseButtons.Middle -> { model with pan = false }
                    | MouseButtons.Right -> { model with zoom = false }
                    | _ -> model

            | Move pos  ->
                let cam = model.view
                let delta = pos - model.dragStart

                let cam =
                    if model.look then
                        let trafo =
                            M44d.Rotation(cam.Right, float delta.Y * -0.01) *
                            M44d.Rotation(cam.Sky, float delta.X * -0.01)

                        let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                        cam.WithForward newForward
                    else
                        cam

                let cam =
                    if model.zoom then
                        let step = -0.05 * (cam.Forward * float delta.Y)
                        cam.WithLocation(cam.Location + step)
                    else
                        cam

                let cam =
                    if model.pan then
                        let step = 0.05 * (cam.Down * float delta.Y + cam.Right * float delta.X)
                        cam.WithLocation(cam.Location + step)
                    else
                        cam

                { model with view = cam; dragStart = pos }

    let withCameraController (state : MCameraControllerState) (f : Message -> 'msg) (m : list<Ui<'msg>>) =
        let attributes =
            AMap.ofList [
                always (attribute "style" "display: flex; width: 100%; height: 100%")
                always (onMouseDown (fun b p -> f (Down(b,p))))
                always (onMouseUp (fun b p -> f (Up b)))
                onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
            ]
            |> AMap.flattenM
            |> AMap.choose (fun _ v -> v)

        div attributes (AList.ofList m)

    let controlledControl (state : MCameraControllerState) (f : Message -> 'msg) (frustum : IMod<Frustum>) (att : amap<string, AttributeValue<'msg>>) (sg : ISg<'msg>) =
        let attributes =
            AMap.ofList [
                always (attribute "style" "display: flex; width: 100%; height: 100%")
                always (onMouseDown (fun b p -> f (Down(b,p))))
                always (onMouseUp (fun b p -> f (Up b)))
                always (onKeyDown (KeyDown >> f))
                always (onKeyUp (KeyUp >> f))
                onlyWhen (state.look %|| state.pan %|| state.zoom) (onMouseMove (Move >> f))
                onlyWhen (state.moveVec |> Mod.map (fun v -> v <> V3i.Zero)) (onRendered (fun s c t -> f StepTime))
            ]
            |> AMap.flattenM
            |> AMap.choose (fun _ v -> v)


//        let camSg : ISg<Message> =
//            failwith ""

        let cam = Mod.map2 Camera.create state.view frustum 
        renderControl cam attributes sg

//
//    type Tool<'model, 'msg> = 
//        {
//            initial : 'model
//            update : 'model -> 'msg -> 'model
//            view : 'model -> hmap<string, AttributeValue<'msg>>
//        }
//
//    let cameraControllerApp =
//        {
//            view = 
//                fun model ->
//                    HMap.ofList [
//                        if model.zoom || model.pan || model.look then
//                            yield onMouseMove Move
//
//                        yield onMouseDown (fun b p -> Down(b,p))
//                        yield onMouseUp (fun b _ -> Up b)
//                    ]
//            update = update
//            initial = initial
//        }

//    let run (f : 'model -> 'msg) (app : Tool<'model, 'm>) : amap<string, AttributeValue<'msg>> =
////        let rec run (m : 'model) =
////            
////        failwith ""
//
////    let att = cameraControllerApp |> run (fun (cam : CameraControllerState) -> Down (MouseButtons.None, V2i.Zero))
//
//    let controlWASD (msg : CameraControllerState -> 'msg) : Attribute<'msg> =
//        failwith ""

    let view (state : MCameraControllerState) =
        let frustum = Frustum.perspective 60.0 0.1 100.0 1.0
        div' [attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [
  
            controlledControl state id 
                (Mod.constant frustum)
                (AMap.empty)
                (
                    Sg.box' C4b.Green (Box3d(-V3d.III, V3d.III))
                        |> Sg.shader {
                            do! DefaultSurfaces.trafo
                            do! DefaultSurfaces.vertexColor
                            do! DefaultSurfaces.simpleLighting
                        }
                        |> Sg.noEvents
                )
            
//            controlledControl state id 
//                (Mod.constant frustum)
//                (AMap.empty)
//                (
//                    Sg.box' C4b.Green (Box3d(-V3d.III, V3d.III))
//                        |> Sg.shader {
//                            do! DefaultSurfaces.trafo
//                            do! DefaultSurfaces.vertexColor
//                            do! DefaultSurfaces.simpleLighting
//                        }
//                        |> Sg.noEvents
//                )
        ]



    let start (runtime : IRuntime) (port : int) =
        App.start runtime port {
            view = view
            update = update
            initial = initial
        }


[<EntryPoint>]
let main args =
    Ag.initialize()
    Aardvark.Init()
    
    use app = new OpenGlApplication()
    let runtime = app.Runtime
    
    CameraController.start runtime 8888
    
    Console.ReadLine() |> ignore
    0

