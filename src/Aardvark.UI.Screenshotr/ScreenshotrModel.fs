namespace Aardvark.UI.Screenshotr

open Aardvark.Base
open Adaptify

type ScreenshotrMessage = 
    | SetCredentialsInputUrl of string
    | SetCredentialsInputKey of string
    | SetCredentials
    | ToggleScreenshotUi
    | CloseScreenshotUi
    | TakeScreenshot
    | SetImageWidth         of int
    | SetImageHeight        of int
    | SetTags               of string
    | ToggleInternalUseOnly

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
    imageSize           : Screenshotr.ImgSize
    defaultTags         : list<string>
    tags                : list<string>
    uiIsVisible         : bool
    internalUseOnly     : bool
}

module ScreenshotrModel =

    let c = Credentials.load ()
   
    let Default aardvarkUrl = {
        credentialsInputUrl = match c with | Valid c -> c.url | _ -> ""
        credentialsInputKey = match c with | Valid c -> c.key | _ -> ""
        credentials         = c 
        aardvarkUrl         = aardvarkUrl
        imageSize           = Screenshotr.ImgSize(1024, 768)
        defaultTags         = []
        tags                = []
        uiIsVisible         = false
        internalUseOnly     = true
    }

    let Custom aardvarkUrl imageSize tags = {
        credentialsInputUrl = match c with | Valid c -> c.url | _ -> ""
        credentialsInputKey = match c with | Valid c -> c.key | _ -> ""
        credentials         = c 
        aardvarkUrl         = aardvarkUrl
        imageSize           = imageSize
        defaultTags         = tags
        tags                = []
        uiIsVisible         = false
        internalUseOnly     = true
    }

