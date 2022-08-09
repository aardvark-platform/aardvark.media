namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

module ScreenshotrUpdate = 

    let update (msg : ScreenshotrMessage) (m: ScreenshotrModel) : ScreenshotrModel = 
        
        match msg with
        
        | SetCredentialsInputKey s -> { m with credentialsInputKey = s }
        | SetCredentialsInputUrl s -> { m with credentialsInputUrl = s }
        | SetCredentials ->
            let c = { url = m.credentialsInputUrl; key = m.credentialsInputKey } 
            { m with credentials = c |> Credentials.save |> Credentials.Valid }
        
        | TakeScreenshot -> 
            match m.credentials with
            | Missing -> failwith "not implemented" // should not be reachable
            | NotAuthorized _ -> failwith "not implemented" // should not be reachable
            | Valid credentials ->
                let uploadTags = 
                    let ts = m.defaultTags @ m.tags
                    match m.internalUseOnly with
                    | true -> ts
                    | false -> ts @ [ "PR" ] 
                let result = Screenshot.takeAndUpload m.aardvarkUrl credentials m.imageSize uploadTags
                match result with
                | Result.Ok _ -> 
                    { m with uiIsVisible = false; tags = [] }
                | Result.Error e ->
                    Log.error "Error c92631ad-d3b4-4715-a8d2-96843eb46be5. Taking or uploading screenshot failed with %A" e
                    { m with credentials = Credentials.NotAuthorized credentials; tags = [] }

        | ToggleScreenshotUi -> { m with uiIsVisible = not m.uiIsVisible }
        | CloseScreenshotUi ->  { m with uiIsVisible = false }
        
        | SetImageWidth w -> { m with imageSize = Screenshotr.ImgSize(w, m.imageSize.Y) }
        | SetImageHeight h -> { m with imageSize = Screenshotr.ImgSize(m.imageSize.X, h) }
        
        | SetTags ts -> 
            let reg = Regex("[*'\",_&^@?!{}%§$/=]") // filters some special characters 
            let tags = 
                ts.Split([|";"|], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> reg.Replace(s, ""))
                |> Array.toList
            { m with tags = tags }

        | ToggleInternalUseOnly -> { m with internalUseOnly = m.internalUseOnly |> not }
            

