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
            | Missing ->
                failwith "not implemented"
            | NotAuthorized _ ->
                failwith "not implemented"
            | Valid credentials ->
                let result = Screenshot.takeAndUpload m.aardvarkUrl credentials m.imageSize m.tags 
                match result with
                | Result.Ok _ -> 
                    { m with uiIsVisible = false }
                | Result.Error e ->
                    Log.error "Taking Screenshot failed with %A" e.Message
                    { m with credentials = Credentials.NotAuthorized credentials }

        | ToggleScreenshotUi -> { m with uiIsVisible = not m.uiIsVisible }
        | CloseScreenshotUi ->  { m with uiIsVisible = false }
        
        | SetImageWidth w -> { m with imageSize = V2i(w, m.imageSize.Y) }
        | SetImageHeight h -> { m with imageSize = V2i(m.imageSize.X, h) }
        
        | SetTags ts -> 
            let reg = Regex("[*'\",_&^@?!{}%§$/=]") // filters some special characters 
            let tags = 
                ts.Split(";", StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> reg.Replace(s, ""))
                |> Array.toList
            { m with tags = tags }

