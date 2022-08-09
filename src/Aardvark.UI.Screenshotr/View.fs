namespace Aardvark.UI.Screenshotr

open Aardvark.UI
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives

module ScreenshotrView = 
  
    /// UI for credentials input
    let credentials (m: AdaptiveScreenshotrModel) : DomNode<ScreenshotrMessage> = 
        
        Incremental.div (AttributeMap.ofList []) (
            alist {
                
                let! c = m.credentials

                yield 
                    match c with
                    | Missing -> h3 [ style "color: red" ] [ text "missing" ]
                    | NotAuthorized _ -> h3 [ style "color: red" ] [ text "cannot connect to screenshotr server" ] 
                    | _ -> div [] []

                let c =
                    match c with
                    | Missing -> Some { url = "<url>"; key = "<key>" }
                    | NotAuthorized c -> Some c
                    | Valid _ -> None

                yield
                    match c with
                    | Some c ->
                        div [ clazz "ui form" ] [             
                            div [ clazz "field" ] [
                                label [] [ text "Url" ]
                                input [
                                    attribute "type" "text" 
                                    attribute "value" c.url
                                    onChange (fun s -> SetCredentialsInputUrl s)
                                ]
                            ]

                            div [ clazz "field"; style "width: 600px" ] [
                                label [] [ text "Key" ]
                                input [
                                    attribute "type" "text" 
                                    attribute "value" c.key
                                    onChange (fun s -> SetCredentialsInputKey s)
                                ]
                            ]

                            button [ clazz "ui button"; onClick (fun _ -> SetCredentials)] [text "Submit"]
                        ]           
                    | None -> div [] []
            }
        )
        
    /// input UI for image size and tags. separate multiple tags with a semicolon.
    let screenshotSettings (m: AdaptiveScreenshotrModel) : DomNode<ScreenshotrMessage> = 

        div [ clazz "ui grid" ] [
            div [ clazz "row" ] [         
            
                h3 [ clazz "ui inverted header"
                     style "margin: 3px; color: white; align-items: center" 
                ] [ text "Image Size:" ]
                
                simplenumeric {
                    attributes [clazz "ui input"; style "width: 70px"]
                    value (m.imageSize |> AVal.map (fun s -> s.X))
                    update SetImageWidth
                    step 1
                    largeStep 100
                    min 0
                    max 100000
                }
                  
                h3 [ clazz "ui inverted header"; style "margin: 3px; display: inline" ] [ text "x" ]
                        
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
                checkbox [clazz "ui inverted toggle checkbox"] m.internalUseOnly ToggleInternalUseOnly "For internal use only!"
            ]
                 
            div [ clazz "row" ] [
                button [clazz "ui button"; onClick (fun _ -> TakeScreenshot)] [text "Take Screenshot"]
            ]
        ]

    /// only show UI when it should be visible and determine if 
    /// the credentials or the screenshot settings UI is shown
    let screenshotrUI (m: AdaptiveScreenshotrModel) = 
        Incremental.div (AttributeMap.ofList [ style "position: absolute; top: 10%; left: 10%" ])  (
            alist {
                let! isVisible = m.uiIsVisible
                match isVisible with
                | false -> div [] []
                | true -> 
                    let! c = m.credentials
                    match c with
                    | Valid _ -> screenshotSettings m
                    | _ -> credentials m
            }
        )