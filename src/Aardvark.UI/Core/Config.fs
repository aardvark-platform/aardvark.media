namespace Aardvark.UI

open Aardvark.Base

module Config =
    // whether after creating dom update code (js), the code should be printed to stdout.
    let mutable shouldPrintDOMUpdates = false

    let mutable dumpJsCodeFile : Option<string> = None

    let mutable shouldTimeUpdate = false

    let mutable shouldTimeJsCodeGeneration = false

    let mutable shouldTimeUIUpdate = false

    let mutable showTimeJsAssembly = false

    /// The document title to set when a MutableApp is created.
    let mutable defaultDocumentTitle = @"Aardvark rocks \o/"

    /// Delay in milliseconds before a removed scene is destroyed.
    let mutable sceneDestroyTimeout = 1000

    /// If set to 'true', regular events named 'onRendered', 'onRender', 'onrendered', and 'onrender' will be treated as after-render events.
    /// Encountered deprecated events will trigger a warning. Use the predefined 'onAfterRender' attribute instead.
    let mutable allowDeprecatedRenderEvents = true