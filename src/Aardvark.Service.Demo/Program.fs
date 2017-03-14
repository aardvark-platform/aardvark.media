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
open Aardvark.UI
open Aardvark.SceneGraph
open Aardvark.Rendering.Text

module TestApp =
        
    type Model =
        {
            lastName : Option<string>
            elements : plist<string>
        }

    type MModel =
        {
            mlastName : ResetMod<Option<string>>
            melements : ResetList<string>
        }
        static member Create(m : Model) =
            {
                mlastName = ResetMod(m.lastName)
                melements = ResetList(m.elements)
            }

        member x.Update(m : Model) =
            if x.mlastName.GetValue() <> m.lastName then
                x.mlastName.Update(m.lastName)
            x.melements.Update(m.elements)

    type Message =
        | AddButton of Index * string

    let initial =
        {
            lastName = None
            elements = PList.ofList ["A"; "B"]
        }

    let update (m : Model) (msg : Message) =
        match msg with
            | AddButton(before, str) -> 
                { m with lastName = Some str; elements = PList.insertAfter before str m.elements }

    let view (m : MModel) =
        div' [attribute "style" "display: flex; flex-direction: column; width: 100%; height: 100%; border: 0; padding: 0; margin: 0"] [

            //Ui(
            //    "div", 
            //    AMap.empty, 
            //    Required = Map.ofList ["d3", "https://cdnjs.cloudflare.com/ajax/libs/d3/4.7.3/d3.min.js"],
            //    BootCode = Some (fun id -> 
            //        String.concat "\r\n" [
            //            sprintf "var sampleSVG = d3.select(\"#%s\")" id
            //            "    .append(\"svg\")"
            //            "    .attr(\"width\", 100)"
            //            "    .attr(\"height\", 100);    "
            //            ""
            //            "sampleSVG.append(\"circle\")"
            //            "    .style(\"stroke\", \"gray\")"
            //            "    .style(\"fill\", \"white\")"
            //            "    .attr(\"r\", 40)"
            //            "    .attr(\"cx\", 50)"
            //            "    .attr(\"cy\", 50)"
            //            "    .on(\"mouseover\", function(){d3.select(this).style(\"fill\", \"aliceblue\");})"
            //            "    .on(\"mouseout\", function(){d3.select(this).style(\"fill\", \"white\");});"
            //        ]
            //    )
            //)

            div AMap.empty (
                m.melements |> AList.mapi (fun i str ->
                    button' [onClick (fun () -> AddButton (i, Guid.NewGuid() |> string))] [
                        text' str
                    ]
                )
            )

            sg' [attribute "style" "display: flex; width: 100%; height: 100%"] (fun ctrl ->
                let value = m.mlastName |> Mod.map (function Some str -> str | None -> "yeah")

                let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
                let proj = ctrl.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
                let view = view |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time
                
                Sg.markdown MarkdownConfig.light value
                    |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                    |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
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

