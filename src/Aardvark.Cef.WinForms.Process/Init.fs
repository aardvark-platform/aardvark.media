namespace Aardvark.Cef

open System
open System.IO
open System.Windows.Forms
open Xilium.CefGlue

module Cef =
    let mutable private initialized = false
    let private l = obj()

    
    let shutdown() =
        lock l (fun () ->
            if initialized then
                initialized <- false
                CefRuntime.Shutdown()
        )

    let init argv =
        lock l (fun _ -> 
            if not initialized then
                initialized <- true

                CefRuntime.Load()

                let settings = CefSettings()
                settings.MultiThreadedMessageLoop <- CefRuntime.Platform = CefRuntimePlatform.Windows
                settings.LogSeverity <- CefLogSeverity.Default
                settings.LogFile <- "cef.log"
                settings.RemoteDebuggingPort <- 1337
                settings.NoSandbox <- true
                settings.IgnoreCertificateErrors <- true

                let path = Path.Combine(System.Environment.CurrentDirectory, "cef_cache")
                if not <| Directory.Exists path then Directory.CreateDirectory path |> ignore
                settings.CachePath <- path

                settings.BrowserSubprocessPath <- "Aardvark.Cef.WinForms.Process.exe"
                let args = 
                    if CefRuntime.Platform = CefRuntimePlatform.Windows then argv
                    else Array.append [|"-"|] argv

                let mainArgs = CefMainArgs(argv)
                let app = AardvarkCefApp()
                let code = CefRuntime.ExecuteProcess(mainArgs, app, 0n)
                if code <> -1 then System.Environment.Exit code

                CefRuntime.Initialize(mainArgs, settings, app, 0n)

                Application.ApplicationExit.Add(fun _ -> 
                    shutdown()
                )
                AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> 
                    shutdown()
                )
        )

        