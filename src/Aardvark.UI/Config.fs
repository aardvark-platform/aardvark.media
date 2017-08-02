namespace Aardvark.UI

module Config =
    // whether after creating dom update code (js), the code should be printed to stdout.
    let mutable shouldPrintDOMUpdates = false