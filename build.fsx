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

entry()