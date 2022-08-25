namespace Aardvark.UI.Screenshotr

open Aardvark.UI
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open Aardvark.Base

open Aardvark.Application

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
                            button [ clazz "ui button"; onClick (fun _ -> CloseScreenshotUI)] [text "Cancel"]
                        ]           
                    | None -> div [] []
            }
        )

    let private imageSizeDiv (m: AdaptiveScreenshotrModel)  = 
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

    let private tagsDiv (m: AdaptiveScreenshotrModel) =
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
        
    let private captionDiv (m: AdaptiveScreenshotrModel) = 
        div [ clazz "field" ] [
                h3 [ clazz "ui inverted dividing header" ] [ text "Caption"]
                textarea ({ placeholder = Some "You can put your caption here ..." }) AttributeMap.empty m.caption SetCaption
            ]

    let private creditsDiv (m: AdaptiveScreenshotrModel) = 
        div [ clazz "field" ] [
                h3 [ clazz "ui inverted dividing header" ] [ text "Credits"]
                textarea ({ placeholder = Some "You can put your credits here ..." }) AttributeMap.empty m.credits SetCredits 
            ]

    let private prFlagDiv (m: AdaptiveScreenshotrModel)  =
        div [ clazz "ui segment" ] [
            div [ clazz "field" ] [
                checkbox [clazz "ui toggle checkbox"] m.internalUseOnly ToggleInternalUseOnly "For internal use only!"
            ]
        ]

    /// input UI for image size and tags. separate multiple tags with a semicolon.
    let screenshotSettingsWithoutUI (m: AdaptiveScreenshotrModel) : DomNode<ScreenshotrMessage> = 

        div [ clazz "ui form"; style "width: 100%" ] [
            
            h2 [ clazz "ui inverted dividing header" ] [ text "Screenshot Settings"]

            imageSizeDiv m
            tagsDiv m
            captionDiv m
            creditsDiv m
            prFlagDiv m

            button [clazz "ui button"; onClick (fun _ -> TakeScreenshotWithoutUI)] [text "Take Screenshot"]
            button [clazz "ui button"; onClick (fun _ -> CancelScreenshotUI)] [text "Cancel"]
        ]

    /// input UI for image size and tags. separate multiple tags with a semicolon.
    let screenshotSettingsWithUI (m: AdaptiveScreenshotrModel) : DomNode<ScreenshotrMessage> = 

        let captureScreenshotWithUI (msg : byte[] -> 'msg) (node : DomNode<'msg>) =
            
            let boot =
                String.concat "\n" [
                    "if(aardvark.electron) {"

                    "function makeVisible() {"
                    "   document.getElementById(\"mainScreenshotrDiv\").style.display = \"inline\";"
                    "}"

                    "function takeScreenshot() {"
                    """var uint8ToBase64=function(t){"use strict";const{fromCharCode:o}=String,r=t=>t.charCodeAt(0);return t.decode=t=>Uint8Array.from(atob(t),r),t.encode=t=>{const r=[];for(let e=0,{length:n}=t;e<n;e++)r.push(o(t[e]));return btoa(r.join(""))},t}({});"""
                    "    aardvark.electron.remote.getCurrentWindow().webContents.capturePage().then((a) => { "
                    "    let data = uint8ToBase64.encode(a.toPNG());"
                    "    aardvark.processEvent('__ID__', 'onimage', data);"
                    "  });"
                    "}"

                    "document.getElementById('__ID__').addEventListener('click', (e) => {"
                    "   document.getElementById(\"mainScreenshotrDiv\").style.display = \"none\";"
                    "   setTimeout(takeScreenshot, 500);" 
                    "   setTimeout(makeVisible, 1000);" 
                    "})"

                    "}"
                ]

            let att =
                AttributeMap.ofList [
                    onEvent "onimage" [] (fun a ->
                        let str = Pickler.json.UnPickleOfString (List.head a)
                        msg (System.Convert.FromBase64String str)
                    )
                ]

            onBoot boot (
                node.WithAttributes att
            )
        
        div [ clazz "ui form"; style "width: 100%" ] [
            
            h2 [ clazz "ui inverted dividing header" ] [ text "Screenshot Settings"]

            tagsDiv m
            captionDiv m
            creditsDiv m
            prFlagDiv m

            captureScreenshotWithUI (fun data -> 
                data |> TakeScreenshotWithUI) (
                button [ clazz "ui button" ] [text "Take Screenshot"]
            )

            button [clazz "ui button"; onClick (fun _ -> CancelScreenshotUI)] [text "Cancel"]
        ]

    /// only show UI when it should be visible and determine if 
    /// the credentials or the screenshot settings UI is shown
    let screenshotrUI (m: AdaptiveScreenshotrModel) = 
        Incremental.div (AttributeMap.ofList [ attribute "id" "mainScreenshotrDiv"; style "position: absolute; top: 5%; left: 10%; width: 50%" ])  (
            alist {
                let! withoutIsVisible = m.withoutUiIsVisible
                let! withIsVisible = m.withUiIsVisible
                let isVisible = withoutIsVisible || withIsVisible
                match isVisible with
                | false -> div [] []
                | true -> 
                    let! c = m.credentials
                    match c with
                    | Valid _ -> 
                        if withoutIsVisible then screenshotSettingsWithoutUI m
                        if withIsVisible then screenshotSettingsWithUI m
                    | _ -> credentials m
            }
        )
        