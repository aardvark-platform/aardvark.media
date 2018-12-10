namespace Aardvark.UI

module Config =
    // whether after creating dom update code (js), the code should be printed to stdout.
    let mutable shouldPrintDOMUpdates = false

    let mutable shouldTimeUnpersistCalls = false

    let mutable shouldTimeJsCodeGeneration = false

    let mutable shouldTimeUIUpdate = false

    let mutable showTimeJsAssembly = false