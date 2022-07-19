namespace Aardvark.UI.ScreenshotrService

open System
open System.Text.Json
open System.IO
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives

open System
open System.Text.RegularExpressions
open FSharp.Data.Adaptive

open Aardvark.Application

module ScreenshotrApp = 

    let update (msg : ScreenshotrAction) (m: ScreenshotrService) : ScreenshotrService = 
        match msg with
        | SetUrl    s -> { m with data = {m.data with url = Some s} }
        | SetApiKey s -> { m with data = {m.data with key = Some s} }
        | WriteStuff -> 

            let jsonString = JsonSerializer.Serialize m.data
               
            let dirPath = 
                Path.combine 
                    [
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolderOption.Create)
                        "Screenshotr"
                    ]
       
            if not (Directory.Exists(dirPath)) then
                Directory.CreateDirectory(dirPath) |> ignore

            let filePath = Path.combine [ dirPath; "cache.json" ] 

            File.writeAllText filePath jsonString
            
            m
        | TakeScreenshot -> 
            
            match m.data.url, m.data.key with 
            | Some url, Some key -> 
                let result = ScreenshotrService.takeAndUpload m.aardvarkUrl url key m.imageSize m.tags 
                match result with
                | Result.Ok _ -> 
                    ()
                | Result.Error e -> 
                    Log.error "Taking Screenshot failed with %A" e.Message
            | _ -> 
                ()

            m
        | SetImageWidth w -> { m with imageSize = V2i(w, m.imageSize.Y) }
            
        | SetImageHeight h -> { m with imageSize = V2i(m.imageSize.X, h) }
            
        | SetTags ts -> 
            let reg = Regex("[*'\",_&#^@?!{}%§$/=]") // filters some special characters 
            let tags = 
                ts.Split(";", StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> reg.Replace(s, ""))
                |> Array.toList
            { m with tags = tags }
            
    let viewConnection (m: AdaptiveScreenshotrService) : DomNode<ScreenshotrAction> = 
        
        div [ clazz "ui form"; attribute "id" "screenshotrForm" ] [             
            div [ clazz "field" ] [
                label [] [ text "Url" ]
                input [
                    attribute "type" "text" 
                    attribute "placeholder" "Key" 
                    onChange (fun s -> SetUrl s)
                ]
            ]

            div [ clazz "field" ] [
                label [] [ text "Key" ]
                //input [ attribute "type" "text"; attribute "name" "key"; attribute "placeholder" "Key" ]
                input [
                    attribute "type" "text" 
                    attribute "placeholder" "Key" 
                    onChange (fun s -> SetApiKey s)
                ]
            ]

            button [ clazz "ui button"; onClick (fun _ -> WriteStuff)] [text "Submit"]
        ]

    let viewUpload (m: AdaptiveScreenshotrService) : DomNode<ScreenshotrAction> = 
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
        ]