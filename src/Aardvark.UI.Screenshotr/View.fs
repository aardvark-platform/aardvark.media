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

        div [ clazz "ui form"; style "width: 100%" ] [
            
            // SCREENSHOT SETTINGS
            h2 [ clazz "ui inverted dividing header" ] [ text "Screenshot Settings"]

            // IMAGE SIZE
            div [ clazz "field" ] [ 
                
                h3 [ clazz "ui inverted dividing header" ] [ text "Image Size"]
                
                div [ clazz "two fields"] [
                    
                    div [ clazz "field"] [
                        label [ style "color:white" ] [ text "Width" ]
                        simplenumeric {
                            attributes [clazz "ui input"]
                            value (m.imageSize |> AVal.map (fun s -> s.X))
                            update SetImageWidth
                            step 1
                            largeStep 100
                            min 0
                            max 100000
                        }
                    ]

                    div [ clazz "field"] [
                        label [ style "color:white" ] [ text "Height" ]
                        simplenumeric {
                            attributes [clazz "ui input"]
                            value (m.imageSize |> AVal.map (fun s -> s.Y))
                            update SetImageHeight
                            step 1
                            largeStep 100
                            min 0
                            max 100000
                        } 
                    ]
                ]
            ]
        
            // TAGS
            div [ clazz "field" ] [
            
                h3 [ clazz "ui inverted dividing header" ] [ text "Tags"]

                div [ clazz "ui input" ] [
                    input [
                        attribute "type" "text" 
                        attribute "id" "myTagInputId" 
                        onChange (fun tag -> AddTag tag)
                        clientEvent "onchange" "$('input[id=\"myTagInputId\"]').val('')"
                    ]
                ]

                Incremental.div (AttributeMap.ofList []) (
                    m.tags
                    |> ASet.map (fun tag -> 
                        div [ clazz "ui label"; style "margin: 3px; margin-top: 5px" ] [ 
                            text tag
                            i [ 
                                clazz "delete icon"
                                onClick (fun _ -> RemoveTag tag) 
                            ] [] 
                        ]
                    )
                    |> ASet.toAList
                )
            ]

            // CAPTION
            div [ clazz "field" ] [
                h3 [ clazz "ui inverted dividing header" ] [ text "Caption"]
                textarea ({ placeholder = Some "You can put your caption here ..." }) AttributeMap.empty m.caption SetCaption
            ]

            // CREDITS
            div [ clazz "field" ] [
                h3 [ clazz "ui inverted dividing header" ] [ text "Credits"]
                textarea ({ placeholder = Some "You can put your credits here ..." }) AttributeMap.empty m.credits SetCredits 
            ]

            // INTERNAL USAGE
            div [ clazz "ui segment" ] [
                div [ clazz "field" ] [
                    checkbox [clazz "ui toggle checkbox"] m.internalUseOnly ToggleInternalUseOnly "For internal use only!"
                ]
            ]

            // BUTTONS
            button [clazz "ui button"; onClick (fun _ -> TakeScreenshot)] [text "Take Screenshot"]
            button [clazz "ui button"; onClick (fun _ -> CancelScreenshotUi)] [text "Cancel"]
        ]

    /// only show UI when it should be visible and determine if 
    /// the credentials or the screenshot settings UI is shown
    let screenshotrUI (m: AdaptiveScreenshotrModel) = 
        Incremental.div (AttributeMap.ofList [ style "position: absolute; top: 5%; left: 10%; width: 50%" ])  (
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