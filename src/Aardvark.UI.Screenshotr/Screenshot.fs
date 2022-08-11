namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open Screenshotr
open System
open System.Net.Http
open System.Text.Json

module Screenshot = 

    /// creates a screenshot from your aardvark.media application and returns it as byte[]
    let take (aardvarkUrl : string) (imageSize : Screenshotr.ImgSize) : Result<byte[],exn> = 
        
        try 
            let client = new HttpClient()
            let baseUri = Uri(aardvarkUrl)

            let jsonUrl = Uri(baseUri, "rendering/stats.json") 

            let getSync x = x |> Async.AwaitTask |> Async.RunSynchronously

            let jsonString = client.GetStringAsync(jsonUrl) |> getSync
            let jsonObject = JsonSerializer.Deserialize<ClientStatistics[]>(jsonString)
        
            Uri(
                baseUri, 
                sprintf "rendering/screenshot/%s?w=%d&h=%d&samples=4" jsonObject.[0].name imageSize.X imageSize.Y
            )
            |> client.GetByteArrayAsync 
            |> getSync
            |> Ok

        with
        | _ as e -> 
            Log.error "Error c83ef237-e4bd-4716-8624-574b481a45b1. Taking screenshot failed with: %A" e
            Result.Error e

    /// uploads a taken screenshot (byte[]) to the screenshotr server
    let upload (credentials : CredentialsDto) tags (caption : string) (credits : string) data : Result<ApiImportScreenshotResponse, exn> = 

        try 
            let client = 
                ScreenshotrHttpClient.Connect(credentials.url, credentials.key) 
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let captionEntry =
                match caption.IsEmptyOrNull() with
                | true -> []
                | false -> [ Custom.Entry.Create("caption", caption) ]

            let creditsEntry = 
                match credits.IsEmptyOrNull() with
                | true -> []
                | false -> [ Custom.Entry.Create("credits", credits) ]

            let custom = captionEntry @ creditsEntry |> Custom.Create

            client.ImportScreenshot(data, tags, custom)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> Ok
            
        with
        | _ as e -> 
            Log.error "Error 1f9596a9-c261-4910-929a-a95014e606bb. Uploading screenshot failed with: %A" e
            Result.Error e

    /// takes a screenshot from your aardvark.media application and uploads it to the screenshotr server
    let takeAndUpload aardvarkUrl credentials imageSize tags caption credits =

        let bytes = take aardvarkUrl imageSize 
        
        match bytes with
        | Result.Error e -> Result.Error e  
        | Result.Ok b -> b |> upload credentials tags caption credits

    /// returns all tags from the homepage
    let getTags credentials =

        try 
            let client = 
                ScreenshotrHttpClient.Connect(credentials.url, credentials.key) 
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let response =
                client.GetTags()
                |> Async.AwaitTask
                |> Async.RunSynchronously

            response.Items |> Ok
            
        with
        | _ as e -> 
            Log.error "Error 50e64cb3-86cd-49bb-afcb-ab09400f84cb. Requesting tags failed with: %A" e
            Result.Error e