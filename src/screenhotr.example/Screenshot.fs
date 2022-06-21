namespace screenhotr.example

open Aardvark.Base

open System
open System.Text.Json
open System.Net

open Screenshotr

module Screenshot =

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

    // download screenshot from your application
    let take (aardvarkUrl : string) (imageSize : Aardvark.Base.V2i) = 
        
        try 
        
            printfn "taking screenshot"
            printfn "image size: %A" imageSize

            let client = new WebClient()
            let baseUri = Uri(aardvarkUrl)

            let jsonUrl = Uri(baseUri, "rendering/stats.json") 
            let jsonString = client.DownloadString(jsonUrl) 
            let jsonObject = JsonSerializer.Deserialize<ClientStatistics[]>(jsonString)
        
            Uri(
                baseUri, 
                sprintf "rendering/screenshot/%s?w=%d&h=%d&samples=4" jsonObject.[0].name imageSize.X imageSize.Y
            )
            |> client.DownloadData

        with
        | _ as e -> 
            Report.Error(sprintf "Taking screenshot failed with: %s" e.Message)
            Array.empty
           
    // upload screenshot to screenshotr server
    let upload screenshotrUrl tags data = 

        try 
            
            printfn "uploading screenshot"
            printfn "tags: %A" tags

            let client = 
                ScreenshotrHttpClient.Connect(screenshotrUrl) 
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            let timestamp = System.DateTime.Now

            client.ImportScreenshot(data, tags, timestamp = timestamp)
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> ignore
            
        with
        | _ as e -> Report.Error(sprintf "Uploading screenshot failed with: %s" e.Message)

    let takeAndUpload imageSize tags =

        let aardvarkUrl = @"http://localhost:4321" // the url you specify in Program.fs
        //let screenshotrUrl = @"http://localhost:5020" // if screenshotr runs at your pc
        let screenshotrUrl = @"https://screenshotr-api.aardworx.net"

        let bytes = take aardvarkUrl imageSize 
        
        match bytes.IsEmptyOrNull() with
        | true -> ()
        | false ->  bytes |> upload screenshotrUrl tags
            
// for taking screenshots with UI
//aardvark.electron.remote.getCurrentWindow().webContents.capturePage().then((e) => console.log(e.toPNG()) );
//base64ArrayBuffer(temp3.buffer);
        

