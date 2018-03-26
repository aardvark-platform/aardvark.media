#load @"paket-files/build/aardvark-platform/aardvark.fake/DefaultSetup.fsx"

open Fake
open System
open System.IO
open System.Diagnostics
open Aardvark.Fake

do MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some Minimal }
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




let projInfo n =
    [ "page-description", "Aardvark.Gems"
      "page-author",  "The Aardvark Platform Team" 
      "project-author",  "The Aardvark Platform Team" 
      "github-link", "https://github.com/aardvark-platform/aardvark.media"
      "project-github", "https://github.com/aardvark-platform/aardvark.media"
      "project-nuget", sprintf "https://www.nuget.org/packages/%s" n
      "root", sprintf "https://rawgit.com/vrvis/aardvark.media/base31/docs/api/%s" n 
      "project-name", n
    ]

let libDirs = ["--libDirs", "bin/Release"]


module MyFake =
    let CreateDocsForDlls outputDir templatesDirs projectParameters sourceRepo dllFiles = 
        let layoutRoots = String.concat " " (templatesDirs |> Seq.map (sprintf "\"%s\""))
        for file in dllFiles do
            projectParameters
            |> Seq.map (fun (k, v) -> [ k; v ])
            |> Seq.concat
            |> Seq.append 
                    ([ "metadataformat"; "--generate"; "--outdir"; outputDir; "--layoutroots"; layoutRoots;
                        "--sourceRepo"; sourceRepo; "--sourceFolder"; currentDirectory; "--parameters" ])
            |> Seq.map (fun s -> 
                    if s.StartsWith "\"" then s
                    else sprintf "\"%s\"" s)
            |> separated " "
            |> fun prefix -> sprintf "%s --dllfiles \"%s\"" prefix file
            |> Fake.FSharpFormatting.run

            printfn "Successfully generated docs for DLL %s" file

Target "API" (fun () -> 
    let projects = ["Aardvark.UI"; "Aardvark.UI.Primitives";]
    for p in projects do
        let target = sprintf "docs/api/%s" p
        if Directory.Exists target then ()
        else Directory.CreateDirectory target |> ignore
        MyFake.CreateDocsForDlls target ["docs/templates/"; "docs/templates/reference/"] (projInfo p @ libDirs) "https://github.com/aardvark-platform/aardvark.media/tree/base31" [sprintf "bin/Release/%s.dll" p]
        let logo = Path.Combine(target, "logo.png")
        if File.Exists logo |> not then File.Copy("docs/logo.png", logo) |> ignore

)

Target "Docs" (fun () -> 
    Fake.FSharpFormatting.CreateDocs "src/Aardvark.Service.Demo/docs" "docs" "docs/template.html" (projInfo "Aardvark.Base")
    Fake.FileHelper.CopyRecursive "packages/build/FSharp.Formatting.CommandTool/styles" "docs/content" true |> printfn "copied: %A"
)


"Compile" ==> "API"

let toolPath = "packages/Aardvark.Compiler.DomainTypeTool/tools/Aardvark.Compiler.DomainTypeTool.exe"
let projectPath = "src/Aardvark.Service.Demo/Aardvark.Service.Demo.fsproj"

Target "GenTypes" (fun () -> 

    let error m = 
        traceError m
    let message m = 
        trace m

    let ok = Fake.ProcessHelper.ExecProcessWithLambdas (fun s -> s.FileName <- toolPath; s.Arguments <- projectPath ) TimeSpan.MaxValue false error message 
    printfn "Aardvark.Compiler.DomainTypeTool.exe returned: %d" ok

)


entry()