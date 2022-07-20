namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open Adaptify

type ScreenshotrMessage = 
    | SetCredentialsInputUrl of string
    | SetCredentialsInputKey of string
    | SetCredentials
    | OpenScreenshotUi
    | CloseScreenshotUi
    | TakeScreenshot
    | SetImageWidth  of int
    | SetImageHeight of int
    | SetTags        of string

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

[<ModelType>]
type ScreenshotrModel = {
    credentialsInputUrl : string
    credentialsInputKey : string
    credentials         : Credentials
    aardvarkUrl         : string
    imageSize           : V2i
    tags                : list<string>
    uiIsVisible         : bool
}

module ScreenshotrModel =

    let Default aardvarkUrl = {
        credentialsInputUrl = ""
        credentialsInputKey = ""
        credentials         = Credentials.load ()
        
        aardvarkUrl = aardvarkUrl
        imageSize   = V2i(1024, 768)
        tags        = []
        uiIsVisible = false
    }

