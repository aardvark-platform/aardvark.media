namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FSharp.Data.Adaptive

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
                    let ts = HashSet.union m.defaultTags m.tags
                    match m.internalUseOnly with
                    | true -> ts
                    | false -> ts |> HashSet.add "PR" 
                let result = Screenshot.takeAndUpload m.aardvarkUrl credentials m.imageSize uploadTags m.caption m.credits
                match result with
                | Result.Ok _ -> 
                    { m with uiIsVisible = false; tags = HashSet.empty; caption = ""; credits = "" }
                | Result.Error e ->
                    Log.error "Error c92631ad-d3b4-4715-a8d2-96843eb46be5. Taking or uploading screenshot failed with %A" e
                    { m with credentials = Credentials.NotAuthorized credentials; tags = HashSet.empty; caption = ""; credits = "" }

        | ToggleScreenshotUi -> { m with uiIsVisible = not m.uiIsVisible }
        | CloseScreenshotUi ->  { m with uiIsVisible = false }
        | CancelScreenshotUi -> { m with uiIsVisible = false; tags = HashSet.empty; caption = ""; credits = "" }
        
        | SetImageWidth w -> { m with imageSize = Screenshotr.ImgSize(w, m.imageSize.Y) }
        | SetImageHeight h -> { m with imageSize = Screenshotr.ImgSize(m.imageSize.X, h) }
        
        | SetTags tags -> { m with tags = tags.Split(',') |> HashSet.ofArray }
        
        | SetCaption c -> { m with caption = c }
        | SetCredits c -> { m with credits = c }

        | ToggleInternalUseOnly -> { m with internalUseOnly = m.internalUseOnly |> not }
            

