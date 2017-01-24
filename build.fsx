#load @"paket-files/build/vrvis/Aardvark.Fake/DefaultSetup.fsx"

open Fake
open System
open System.IO
open System.Diagnostics
open Aardvark.Fake

do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

DefaultSetup.install ["src/Aardvark.Media.sln"]

Target "Run" (fun() ->
    tracefn "exec: %d" (Shell.Exec "bin/Release/Aardvark.Cef.exe")
)

Target "Test" (fun () ->
    Fake.NUnitSequential.NUnit (fun p -> { p with ToolPath = @"packages\NUnit.Runners\tools"
                                                  ToolName = "nunit-console.exe" }) [@"bin\Release\Aardvark.Base.Incremental.Tests.exe"]
)

Target "Statistics" (fun () ->
    let fsFiles = !!"src/**/*.fs"  

    let mutable stats = Map.empty
    for f in fsFiles do
        tracefn "file: %A" f
        ()
)

let projInfo =
  [ "page-description", "Fablish meets aardvark"
    "page-author", "Harald Steinlechner"
    "github-link", "https://github.com/vrvis"
    "project-name", "FablishInterop" ]


Target "Docs" (fun () -> 
    Fake.FSharpFormatting.CreateDocs "src/FablishInterop/docs" "docs" "src/FablishInterop/docs/template.html" projInfo
    Fake.FileHelper.CopyRecursive "packages/build/FSharp.Formatting.CommandTool/styles" "docs/content" true |> printfn "copied: %A"
)


entry()