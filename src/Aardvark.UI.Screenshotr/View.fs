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

                // ERROR TEXT

                yield 
                    match c with
                    | Missing -> h2 [ style "color: red" ] [ text "Credentials are missing." ]
                    | NotAuthorized _ -> h2 [ style "color: red" ] [ text "Cannot connect to screenshotr server." ] 
                    | _ -> div [] []

                let c =
                    match c with
                    | Missing -> Some { url = "<url>"; key = "<key>" }
                    | NotAuthorized c -> Some c
                    | Valid _ -> None

                yield
                    match c with
                    | Some c ->
                        div [ clazz "ui form"; style "width: 100%" ] [          
                            
                            // URL
                            h2 [ clazz "ui inverted dividing header" ] [ text "Url"]
                            
                            div [ clazz "field" ] [
                                input [
                                    attribute "type" "text" 
                                    attribute "value" c.url
                                    onChange (fun s -> SetCredentialsInputUrl s)
                                ]
                            ]

                            // KEY
                            h2 [ clazz "ui inverted dividing header" ] [ text "Key"]

                            div [ clazz "field" ] [
                                input [
                                    attribute "type" "text" 
                                    attribute "value" c.key
                                    onChange (fun s -> SetCredentialsInputKey s)
                                ]
                            ]

                            // BUTTONS
                            button [ clazz "ui button"; onClick (fun _ -> SetCredentials)] [text "Submit"]
                            button [ clazz "ui button"; onClick (fun _ -> CloseScreenshotUi)] [text "Cancel"]
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

                Incremental.div (AttributeMap.ofList[]) (
                    alist {
                        let! credentials = m.credentials
                        match credentials with
                        | Credentials.Valid cs -> 
                            let result = Screenshot.getTags cs
                            match result with
                            | Result.Error _ -> ()
                            | Result.Ok tagInfos -> 
                                
                                let sortedTags = 
                                    tagInfos 
                                    |> Seq.filter (fun t -> t.Tag <> "PR" && t.Tag <> "!hide")
                                    |> Seq.sortByDescending (fun t -> t.Count)
                                
                                onBoot "$('.ui.dropdown').dropdown({ allowAdditions: true });" (
                                    div [ clazz "ui fluid multiple search selection dropdown"; onChange (fun tags -> SetTags tags) ] [
                                        input [ attribute "type" "hidden"; attribute "name" "tags" ]
                                        i [ clazz "dropdown icon" ] []
                                        div [ clazz "default text" ] [ text "Enter Tag" ]
                                        div [ clazz "menu" ] [
                                            for st in sortedTags do
                                                yield div [ clazz "item"; attribute "data-value" st.Tag ] [ text st.Tag ]
                                        ]
                                    ]
                                )

                        | _ -> ()
                       
                    }
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