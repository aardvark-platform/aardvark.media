namespace Aardvark.UI

open Aardvark.Base

module Config =
    // whether after creating dom update code (js), the code should be printed to stdout.
    let mutable shouldPrintDOMUpdates = false

    let mutable dumpJsCodeFile : Option<string> = None

    let mutable shouldTimeUnpersistCalls = false

    let mutable shouldTimeJsCodeGeneration = false

    let mutable shouldTimeUIUpdate = false

    let mutable showTimeJsAssembly = false

    let mutable updateFailed =
        fun (e : exn) -> 
            Log.error "[media] update function failed with: %A" e

    let mutable updateThreadFailed =
        fun (e : exn) -> 
            Log.error "[Media] UI update thread died (exn in view function?) : \n%A" e