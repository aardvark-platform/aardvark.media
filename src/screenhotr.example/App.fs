namespace screenhotr.example

open System
open System.Text.RegularExpressions
open FSharp.Data.Adaptive

open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

open screenhotr.example.Model

module App =
    
    let initial = 
        { 
            cameraState = FreeFlyController.initial
            imageSize   = V2i(1024, 768)
            tags        = Array.empty
        }

    let update (m : Model) (msg : Message) =
        match msg with
            | CameraMessage msg -> { m with cameraState = FreeFlyController.update m.cameraState msg }
            
            | SetImageWidth w -> { m with imageSize = V2i(w, m.imageSize.Y) }
            
            | SetImageHeight h -> { m with imageSize = V2i(m.imageSize.X, h) }
            
            | SetTags ts -> 
                let reg = Regex("[*'\",_&#^@?!{}%§$/=]") // filters some special characters 
                let tags = 
                    ts.Split(";", StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> reg.Replace(s, ""))
                { m with tags = tags }
            
            | TakeScreenshot -> 
                Screenshot.takeAndUpload m.imageSize m.tags 
                m
                
            | Message.KeyDown k -> 
                match k with
                | Keys.F8 -> m
                | _ -> m
            

    let view (m : AdaptiveModel) =

        let frustum = 
            Frustum.perspective 60.0 0.1 100.0 1.0 
                |> AVal.constant

        let scene =
            [| 
                Sg.sphere' 8 C4b.ForestGreen 1.0
                Sg.box' C4b.CornflowerBlue Box3d.Unit |> Sg.translate 2.0 1.0 0.0
                Sg.cone' 125 C4b.DarkYellow 1.0 2.0 |> Sg.translate (-2.0) -3.0 0.0
                Sg.cylinder' 30 C4b.Tomato 1.0 2.0 |> Sg.translate 2.0 -3.0 0.0
            |]
            |> Sg.ofArray
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

        let att =
            [
                style "position: fixed; left: 0; top: 0; width: 100%; height: 100%"
                onKeyDown (KeyDown)
            ]

        let dependencies = 
            [
                { kind = Script; name = "screenshot"; url = "screenshot.js" }
            ] @ Html.semui

        body [] [
            FreeFlyController.controlledControl m.cameraState CameraMessage frustum (AttributeMap.ofList att) scene

            div [ clazz "ui euqal width grid"; style "position: fixed; left: 0; top: 0; padding: 20px"] [
            
                div [ clazz "ui grid" ] [
                
                    div [ clazz "row" ] [
                        
                        h3 [ clazz "ui inverted header"; style "margin: 3px" ] [ text "Image size: " ]
                   
                        simplenumeric {
                            attributes [clazz "ui input"; style "width: 70px"]
                            value (m.imageSize |> AVal.map (fun s -> s.X))
                            update SetImageWidth
                            step 1
                            largeStep 100
                            min 0
                            max 100000
                        }
                  
                        h3 [ clazz "ui inverted header"; style "margin: 3px" ] [ text "x" ]
                   
                        simplenumeric {
                            attributes [clazz "ui input"; style "width: 70px"]
                            value (m.imageSize |> AVal.map (fun s -> s.Y))
                            update SetImageHeight
                            step 1
                            largeStep 100
                            min 0
                            max 100000
                        } 
                    ]
                
                    div [ clazz "row" ] [
                        div [ clazz "ui right labeled left icon input"; style "width: 80%" ] [
                            i [ clazz "tags icon" ] []
                            input [
                                attribute "type" "text" 
                                attribute "placeholder" "tag1;tag2;tag3" 
                                onChange (fun tags -> SetTags tags)
                            ]
                            div [ clazz "ui tag label" ] [ text "Add Tags" ]
                        ]
                    ]
                 
                    div [ clazz "row" ] [
                        button [clazz "ui button"; onClick (fun _ -> TakeScreenshot)] [text "Take Screenshot"]
                    ]

                    //div [ clazz "row" ] [
                    //    button [clazz "ui button"; clientEvent "onclick" "takeScreenshot()"] [text "Electron Test"]
                    //]

                    div [ clazz "row" ] [
                        //button [clazz "ui button"; clientEvent "onclick" "test()"] [text "Test"]

                        form [ clazz "ui form"; attribute "id" "screenshotrForm" ] [
                            
                            div [ clazz "field" ] [
                                label [] [ text "Url" ]
                                input [ attribute "type" "text"; attribute "name" "url"; attribute "placeholder" "Url" ] 
                            ]

                            div [ clazz "field" ] [
                                label [] [ text "Key" ]
                                input [ attribute "type" "text"; attribute "name" "key"; attribute "placeholder" "Key" ] 
                            ]

                            button [ clazz "ui button"; attribute "type" "submit" ] [text "Submit"]
                            button [ clazz "ui button" ; clientEvent "onclick" "removeForm()" ] [text "Cancel"]
                        ]
                    ]
                ]
            ]
        
        ]
        |>  require dependencies

    let app =
        {
            initial = initial
            update = update
            view = view
            threads = fun m -> m.cameraState |> FreeFlyController.threads |> ThreadPool.map CameraMessage
            unpersist = Unpersist.instance
        }
