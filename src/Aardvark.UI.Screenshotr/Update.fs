namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FSharp.Data.Adaptive

module ScreenshotrUpdate = 

    let private uploadScreenshot m data = 
         
        match m.credentials with
        | Missing -> failwith "not implemented" // should not be reachable
        | NotAuthorized _ -> failwith "not implemented" // should not be reachable
        | Valid credentials ->
                
            let uploadTags = 
                let ts = HashSet.union m.defaultTags m.tags
                match m.internalUseOnly with
                | true -> ts
                | false -> ts |> HashSet.add "PR"
                    
            let result = 
                match data with
                | None -> Screenshot.takeAndUpload m.aardvarkUrl credentials m.imageSize uploadTags m.caption m.credits
                | Some bytes -> bytes |> Screenshot.upload credentials uploadTags m.caption m.credits 
                   
            match result with
            | Result.Ok _ -> 
                { m with withoutUiIsVisible = false; withUiIsVisible = false; tags = HashSet.empty; caption = ""; credits = "" }
            | Result.Error e ->
                Log.error "Error c92631ad-d3b4-4715-a8d2-96843eb46be5. Taking or uploading screenshot failed with %A" e
                { m with credentials = Credentials.NotAuthorized credentials; tags = HashSet.empty; caption = ""; credits = "" }

    let update (msg : ScreenshotrMessage) (m: ScreenshotrModel) : ScreenshotrModel = 
        
        match msg with

        | SetCredentialsInputKey s -> { m with credentialsInputKey = s }
        | SetCredentialsInputUrl s -> { m with credentialsInputUrl = s }
        | SetCredentials ->
            let c = { url = m.credentialsInputUrl; key = m.credentialsInputKey } 
            { m with credentials = c |> Credentials.save |> Credentials.Valid }
        
        | TakeScreenshotWithoutUI -> uploadScreenshot m None
        | TakeScreenshotWithUI bytes -> Some bytes |> uploadScreenshot m 

        | ToggleScreenshotUI t -> 
            match t with
            | ScreenshotType.WithoutUI -> { m with withoutUiIsVisible = not m.withoutUiIsVisible }
            | ScreenshotType.WithUI ->  { m with withUiIsVisible = not m.withUiIsVisible }

        | CloseScreenshotUI ->  { m with withoutUiIsVisible = false; withUiIsVisible = false }
        | CancelScreenshotUI -> { m with withoutUiIsVisible = false;  withUiIsVisible = false; tags = HashSet.empty; caption = ""; credits = "" }
        
        | SetImageWidth w -> { m with imageSize = Screenshotr.ImgSize(w, m.imageSize.Y) }
        | SetImageHeight h -> { m with imageSize = Screenshotr.ImgSize(m.imageSize.X, h) }
        
        | SetTags tags -> { m with tags = tags.Split(',') |> HashSet.ofArray }
        
        | SetCaption c -> { m with caption = c }
        | SetCredits c -> { m with credits = c }

        | ToggleInternalUseOnly -> { m with internalUseOnly = m.internalUseOnly |> not }
            

