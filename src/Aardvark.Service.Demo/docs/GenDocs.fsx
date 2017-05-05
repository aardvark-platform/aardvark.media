#load "../../../packages/FSharp.Formatting/FSharp.Formatting.fsx"
open FSharp.Literate
open System.IO

let source = __SOURCE_DIRECTORY__
let template = Path.Combine(source, "template.html")

let script = Path.Combine(source, "Test.fsx")
Literate.ProcessScriptFile(script, template)//, assemblyReferences=["bin/Debug/Aardvark.Base.dll"])
