#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"
#load @"paket-files/build/aardvark-platform/aardvark.fake/DefaultSetup.fsx"

open System
open System.IO
open System.Diagnostics
open Aardvark.Fake
open Fake.Core
open Fake.DotNet
open System.Runtime.InteropServices

do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
    DefaultSetup.install ["src/Aardvark.Media.sln"]
else
    DefaultSetup.install ["src/Aardvark.Media.NonWindows.sln"]


Target.create "CompileAll" (fun _ ->
    let cfg = "Debug" //if config.debug then "Debug" else "Release"
        
    let assemblyVersion = getVersion()
    
    let props =
        [
            yield "Configuration", cfg
            yield "AssemblyVersion", assemblyVersion
            yield "AssemblyFileVersion", assemblyVersion
            yield "InformationalVersion", assemblyVersion
            yield "ProductVersion", assemblyVersion
            yield "PackageVersion", assemblyVersion
        ]

    let debug = ("Configuration", "Debug") :: props
    let release = ("Configuration", "Debug") :: props
    
    "src/Aardvark.Media.sln"|> DotNet.build (fun o ->
        { o with
            NoRestore = false 
            Configuration = DotNet.BuildConfiguration.Debug
            MSBuildParams =
                { o.MSBuildParams with
                    Properties = debug
                    DisableInternalBinLog = true
                }
        }
    )

    "src/Aardvark.Media.sln"|> DotNet.build (fun o ->
        { o with
            NoRestore = false 
            Configuration = DotNet.BuildConfiguration.Debug
            MSBuildParams =
                { o.MSBuildParams with
                    Properties = release
                    DisableInternalBinLog = true
                }
        }
    )
    
    "src/Aardvark.Media.Examples.sln"|> DotNet.build (fun o ->
        { o with
            NoRestore = false 
            Configuration = DotNet.BuildConfiguration.Release
            MSBuildParams =
                { o.MSBuildParams with
                    Properties = debug
                    DisableInternalBinLog = true
                }
        }
    )

    "src/Aardvark.Media.Examples.sln"|> DotNet.build (fun o ->
        { o with
            NoRestore = false 
            Configuration = DotNet.BuildConfiguration.Release
            MSBuildParams =
                { o.MSBuildParams with
                    Properties = release
                    DisableInternalBinLog = true
                }
        }
    )
    
)

entry()