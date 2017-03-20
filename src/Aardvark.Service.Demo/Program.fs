open Aardvark.Service

open System

open Aardvark.Base
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
        

    let initial =
        {
            lastName = None
            elements = PList.ofList ["A"; "B"; "C"]
            hasD3Hate = true
            boxHovered = false
            boxScale = 1.0
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
    let clazz str = attribute "class" str

    let semui =
        [ 
            { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
            { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
        ]

    let view (m : MModel) =
        
        div' [style { backgroundColor C4b.Red }; attribute "style" "display: flex; flex-direction: row; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [
            

//            div AMap.empty (
//                alist {
//                    let! hasHate = m.mhasD3Hate
//                    if hasHate then
//                        yield Ui(
//                            "div", 
//                            AMap.empty, 
//                            Required = 
//                                [ 
//                                    { kind = Script; name = "d3"; url = "https://cdnjs.cloudflare.com/ajax/libs/d3/4.7.3/d3.min.js" } 
//                                ],
//
//                            Callbacks = 
//                                Map.ofList [
//                                    "bla", Hugo
//                                ],
//
//                            Channels = 
//                                [
//                                    new ModChannel<_>("urdar", m.mlastName)
//                                    new AListChannel<_>("heinzi", m.melements)
//                                ],
//
//                            Boot = Some (fun id -> 
//                                String.concat "\r\n" [
//                                    "urdar.onmessage = function(data) { console.warn(data); };"
//                                    "heinzi.onmessage = function(data) { console.warn(data); };"
//                                    ""
//                                    sprintf "console.warn(\"%s said hi\");" id
//                                    sprintf "var sampleSVG = d3.select(\"#%s\")" id
//                                    sprintf "    .append(\"svg\")"
//                                    sprintf "    .attr(\"width\", 100)"
//                                    sprintf "    .attr(\"height\", 100);    "
//                                    sprintf ""
//                                    sprintf "sampleSVG.append(\"circle\")"
//                                    sprintf "    .style(\"stroke\", \"gray\")"
//                                    sprintf "    .style(\"fill\", \"white\")"
//                                    sprintf "    .attr(\"r\", 40)"
//                                    sprintf "    .attr(\"cx\", 50)"
//                                    sprintf "    .attr(\"cy\", 50)"
//                                    sprintf "    .on(\"mouseover\", function(){d3.select(this).style(\"fill\", \"aliceblue\"); aardvark.processEvent(\"%s\", \"bla\", \"adorner\"); })" id
//                                    sprintf "    .on(\"mouseout\", function(){d3.select(this).style(\"fill\", \"white\"); aardvark.processEvent(\"%s\", \"bla\", \"dedorner\"); });" id
//                                ]
//                            ),
//
//                            Shutdown = Some (fun id ->
//                                sprintf "console.warn(\"%s said bye\");" id
//                            )   
//                        )
//                }
//            )
//
//            Ui(
//                "button",
//                AMap.ofList [attribute "class" "ui button"; onClick (fun () -> ToggleD3Hate)],
//                AList.ofList [text' "asdsad"],
//                Required = [ 
//                    { kind = Stylesheet; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.css" }
//                    { kind = Script; name = "semui"; url = "https://cdn.jsdelivr.net/semantic-ui/2.2.6/semantic.min.js" }
//                ]
//
//            )
//
//            div AMap.empty (
//                m.melements |> AList.mapi (fun i str ->
//                    button' [onClick (fun () -> AddButton (i, Guid.NewGuid() |> string))] [
//                        Ui("span", AMap.empty, Mod.constant str, Shutdown = Some (sprintf "console.log(\"shutdown %s\")"))
//                    ]
//                )
//            )
            
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

            sg' [attribute "style" "display: flex; width: 100%; height: 100%"] (fun ctrl ->
                let value = m.lastName |> Mod.map (function Some str -> str | None -> "yeah")

                let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
                let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time
                let cam = Mod.map2 Camera.create view proj

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
                        ]

                let sg =
                    Sg.ofList [
                        sg

                        Sg.markdown MarkdownConfig.light (m.lastName |> Mod.map (Option.defaultValue "yeah"))
                            |> Sg.noEvents
                            |> Sg.transform (Trafo3d.FromOrthoNormalBasis(-V3d.IOO, V3d.OOI, V3d.OIO))
                            |> Sg.translate 0.0 0.0 3.0
                    ]

                cam, sg
            )

        ]
        //Ui(
        //    "div",
        //    AMap.empty,
        //    AList.ofList [
        //        Ui(
        //            "div",
        //            AMap.empty,
        //            m.melements |> AList.mapi (fun i str ->
        //                Ui(
        //                    "button",
        //                    AMap.ofList ["onclick", Event([], fun _ -> AddButton (i, Guid.NewGuid() |> string))],
        //                    Mod.constant ("<&" + str + "&>")
        //                )
        //            )
        //        )

        //        Ui(
        //            "div",
        //            AMap.ofList ["class", Value "aardvark"; "style", Value "height: 600px; width: 800px"],
        //            fun (ctrl : IRenderControl) ->
                            
        //                let value =
        //                    m.mlastName |> Mod.map (function Some str -> str | None -> "yeah")

        //                let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
        //                let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        //                let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time

        //                let sg = 
        //                    Sg.markdown MarkdownConfig.light value
        //                        |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
        //                        |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

        //                ctrl.Runtime.CompileRender(ctrl.FramebufferSignature, sg)

        //        )

        //    ]
        //)


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
    
    TestApp.start runtime 8888
    
    Console.ReadLine() |> ignore
    0

