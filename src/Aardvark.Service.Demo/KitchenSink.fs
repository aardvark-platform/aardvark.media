namespace Viewer

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


module KitchenSinkApp =
    open Demo.TestApp.Mutable.MModel

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
        | MoveRay of Option<float> * RayPart
        | NewTime 

    let initial =
        {
            lastName = None
            elements = PList.ofList ["A"; "B"; "C"]
            hasD3Hate = true
            boxHovered = false
            boxScale = 1.0
            dragging = false
            lastTime = MicroTime.Now
            objects = HMap.empty
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

            | NewTime ->
                let now = MicroTime.Now
                { m with lastName = Some (string (now - m.lastTime)); lastTime = now }

            | MoveRay(t,r) ->
                match t with
                    | Some t ->
                        { m with lastName = Some (string t) }
                    | None ->
                        { m with lastName = None }// Some "no hit" }

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

        div [attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [

            require semui (
                div [ attribute "class" "ui visible sidebar inverted vertical menu"; attribute "style" "min-width: 250px" ] [
                    div [ clazz "item"] [ 
                        b [] [text "Instructions"]
                        div [ clazz "menu" ] [
                            span [clazz "item"] [text "use typical WASD bindings to navigate"]
                            span [clazz "item"] [text "hover the box to see its highlighting"]
                            span [clazz "item"] [text "double click the box to persistently enlarge it"]
                        ]
                    ]

                    div [ clazz "item"] [ 
                        b [] [text "Actions"]
                        div [ clazz "menu" ] [
                            div [ clazz "item "] [ 
                                button [clazz "ui button tiny"; onClick (fun () -> Scale) ] [text "increase scale"]
                            ]
                            div [ clazz "item "] [ 
                                button [ clazz "ui button tiny"; onClick (fun () -> ResetScale) ] [text "reset scale"]
                            ]
                        ]
                    ]
                    
                    div [ clazz "item"] [ 
                        b [] [text "Status"]
                        div [ clazz "menu" ] [
                            span [clazz "item"; attribute "style" "color: red; font-weight: bold"] [ 
                                m.boxHovered |> Mod.map (function true -> "hovered" | false -> "not hovered") |> Incremental.text 
                            ]
                            
                            div [clazz "item"] [
                                span [] [text "current scale: "]
                                span [attribute "style" "color: red; font-weight: bold"] [ m.boxScale |> Mod.map string |> Incremental.text ]
                            ]
                        ]
                    ]
                ]
            )

            Incremental.renderControl
                cam
                (
                    AttributeMap.ofListCond [
                        always (style "display: flex; width: 100%; height: 100%")
                        onlyWhen m.dragging (onMouseUp (fun b r -> StopDrag))
                    ] 
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
                            |> Sg.pickable (Box baseBox)        
                                 

                    let sg = 
                        box |> Sg.trafo (m.boxScale |> Mod.map Trafo3d.Scale)
                            |> Sg.withEvents [
                                Sg.onEnter (fun p -> Enter)
                                Sg.onLeave (fun () -> Exit)
                                Sg.onDoubleClick (fun _ -> Scale)
                                Sg.onMouseDown (fun _ -> StartDrag)
                            ]
                            |> Sg.Incremental.withGlobalEvents (
                                    amap {
                                        let! dragging= m.dragging
                                        if dragging then
                                            yield Sg.Global.onMouseMove (fun evt -> MoveRay(None,evt.ray))
                                   }
                               )

                    Sg.ofList [
                        sg

                        Sg.markdown MarkdownConfig.light (m.lastName |> Mod.map (Option.defaultValue "yeah"))
                            |> Sg.noEvents
                            |> Sg.transform (Trafo3d.FromOrthoNormalBasis(-V3d.IOO, V3d.OOI, V3d.OIO))
                            |> Sg.translate 0.0 0.0 3.0
                    ]


            )

        ]

    let rec timerThread() =
        proclist {
            do! Proc.Sleep 10
            yield NewTime
            yield! timerThread()
        }

    let pool = ThreadPool.empty |> ThreadPool.add "timer" (timerThread())

    let start () =
        App.start {
            unpersist = Unpersist.instance
            threads = fun _ -> pool
            view = view
            update = update
            initial = initial
        }