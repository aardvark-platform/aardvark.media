namespace Aardvark.UI.ScreenshotrService

open System
open System.Net
open System.Text.Json
open System.IO

open Screenshotr
open System.Net.Http
open Aardvark.Base
open Adaptify

type ClientStatistics =
    {
        session         : System.Guid
        name            : string
        frameCount      : int
        invalidateTime  : float
        renderTime      : float
        compressTime    : float
        frameTime       : float
    }

type ScreenshotrData = 
    {
        url : string option
        key : string option
    }

type ScreenshotrAction = 
    | WriteStuff     
    | SetUrl         of string
    | SetApiKey      of string
    | SetImageWidth  of int
    | SetImageHeight of int
    | SetTags        of string
    | TakeScreenshot

[<ModelType>]
type ScreenshotrService =
    {
        data        : ScreenshotrData
        aardvarkUrl : string
        imageSize   : V2i
        tags        : list<string>
    }

module ScreenshotrService = 

    let initScreenshotr aardvarkUrl = {
        data        = {
            url = None
            key = None
        }
        aardvarkUrl = aardvarkUrl
        imageSize   = V2i(1024, 768)
        tags        = []
    }

    // download screenshot from your application
    let take (aardvarkUrl : string) (imageSize : Aardvark.Base.V2i) : Result<byte[],exn> = 
        
        try 
        
            printfn "taking screenshot"
            printfn "image size: %A" imageSize

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
            Report.Error(sprintf "Taking screenshot failed with: %s" e.Message)
            Result.Error e
           
    // upload screenshot to screenshotr server
    let upload screenshotrUrl apiKey tags data : Result<ApiImportScreenshotResponse, exn> = 

        try 
            
            printfn "uploading screenshot"
            printfn "tags: %A" tags

            let client = 
                ScreenshotrHttpClient.Connect(screenshotrUrl, apiKey) 
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            let timestamp = System.DateTime.Now

            client.ImportScreenshot(data, tags, timestamp = timestamp)
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> Ok
            
        with
        | _ as e -> 
            Report.Error(sprintf "Uploading screenshot failed with: %s" e.Message)
            Result.Error e

    let takeAndUpload aardvarkUrl screenshotrUrl apiKey imageSize tags =

        let bytes = take aardvarkUrl imageSize 
        
        match bytes with
        | Result.Error e -> Result.Error e  
        | Result.Ok b -> b |> upload screenshotrUrl apiKey tags

    let setConnection = 

        let dirPath = 
            Path.combine 
                [
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    "Screenshotr"
                ]
       
        if not (Directory.Exists(dirPath)) then
            Directory.CreateDirectory(dirPath) |> ignore
        let filePath = Path.combine [ dirPath; "cache.json" ] 

        match File.Exists(filePath) with
        | true -> 
            filePath
            |> File.ReadAllText
            |> JsonSerializer.Deserialize<ScreenshotrData>
        | false -> {
                url = None
                key = None
            }
