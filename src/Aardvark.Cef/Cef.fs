namespace Aardvark.Cef.Internal

open System
open Xilium.CefGlue
open Aardvark.Base

module Cef =

    let mutable private initialized = false

    let getArgs() =
        let args = Environment.GetCommandLineArgs()
        Array.sub args 1 (args.Length - 1)

    let init() =
        if not initialized then
            initialized <- true

          

            let args = getArgs()
            CefRuntime.Load()



            let args = CefMainArgs(args)
            
            let app = App()
            Aardvark.Base.Report.LogFileName <- System.IO.Path.GetTempFileName()
            let other = CefRuntime.ExecuteProcess(args, app, 0n)
            if other <> -1 then
                // other <> -1 if we're a secondary process
                System.Environment.Exit(0)

            let settings = CefSettings()
            settings.BrowserSubprocessPath <- "Aardvark.Cef.Process.exe"
            settings.SingleProcess <- false
            settings.MultiThreadedMessageLoop <- true
            settings.NoSandbox <- true
            settings.LogSeverity <- CefLogSeverity.Warning
            settings.RemoteDebuggingPort <- 1337
            settings.IgnoreCertificateErrors <- true
            settings.LocalesDirPath <- "."
            settings.WindowlessRenderingEnabled <- true
            
            settings.CommandLineArgsDisabled <- false

            CefRuntime.Initialize(args, settings, app, 0n)

    let shutdown() =
        CefRuntime.Shutdown() 