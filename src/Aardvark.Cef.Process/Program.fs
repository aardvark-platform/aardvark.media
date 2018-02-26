open Aardvark.Base
open System.IO
open System
open Aardvark.Cef.Internal

// simply starts the chromium process using its args
[<EntryPoint>]
let main argv = 
    try
        Report.LogFileName <- Path.GetTempFileName()
        Xilium.CefGlue.ChromiumUtilities.unpackCef()
        Cef.init()
        Cef.shutdown()
        0
    with e ->
        System.Diagnostics.Debugger.Launch() |> ignore
        let message = sprintf "Secondary process crashed: %A" e.Message
        let appData = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
        let tempPath = Path.Combine(appData, sprintf "cefCrash.%Alog" DateTime.Now.Ticks)
        File.WriteAllText(tempPath,message)
        -1
