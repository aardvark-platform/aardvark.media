open Aardvark.Base
open System.IO
open Aardvark.Cef.Internal

// simply starts the chromium process using its args
[<EntryPoint>]
let main argv = 
    try
        Report.LogFileName <- Path.GetTempFileName()
        Cef.init()
        Cef.shutdown()
        0
    with e ->
        -1
