namespace Aardvark.Cef.OffScreen

open Aardvark.Base
open Aardvark.Rendering
open CefSharp
open CefSharp.OffScreen
open CefSharp.BrowserSubprocess
open FSharp.Data.Adaptive
open System
open System.IO
open System.Runtime.InteropServices
open System.Threading

[<AbstractClass; Sealed>]
type AardvarkCef =
    static let lockObj = obj()

    /// <summary>
    /// Returns whether CEF has been initialized.
    /// </summary>
    static member IsInitialized =
        let mutable lockTaken = false
        try
            Monitor.TryEnter(lockObj, &lockTaken)
            lockTaken && Cef.IsInitialized.HasValue && Cef.IsInitialized.Value
        finally
            if lockTaken then Monitor.Exit lockObj

    static member DefaultSettings =
        let settings = new CefSettings()
        settings.MultiThreadedMessageLoop <- true
        settings.CachePath <- Path.Combine(Environment.CurrentDirectory, "cef_cache")
        settings.LogFile <- Path.Combine(Environment.CurrentDirectory, "cef.log")
        settings.LogSeverity <- LogSeverity.Warning
        settings.IgnoreCertificateErrors <- true
        settings.CommandLineArgsDisabled <- false
        settings.WindowlessRenderingEnabled <- true
        settings.SetOffScreenRenderingBestPerformanceArgs() // https://github.com/cefsharp/CefSharp/issues/4953
        settings.CefCommandLineArgs.Add "no-proxy-server"
        settings

    static member Init([<Optional; DefaultParameterValue(null: CefSettings)>] settings: CefSettings) =
        lock lockObj (fun _ ->
            if not AardvarkCef.IsInitialized then
                let s = settings ||? AardvarkCef.DefaultSettings
                use _ = if notNull settings then Disposable.empty else s

                try
                    if not <| String.IsNullOrWhiteSpace s.CachePath then
                        Directory.CreateDirectory s.CachePath |> ignore
                with exn ->
                    Log.warn $"[CEF] Failed to create cache directory '{s.CachePath}': {exn.Message}"
                    s.CachePath <- null

                if not <| Cef.Initialize(s, performDependencyCheck = true) then
                    let exitCode = Cef.GetExitCode()
                    failwith $"Cef.Initialize failed with exit code {exitCode}"

                { new IDisposable with member _.Dispose() = AardvarkCef.Shutdown() }
            else
                Disposable.empty
        )

    static member Shutdown() =
        lock lockObj (fun _ ->
            if AardvarkCef.IsInitialized && not Cef.IsShutdown then
                Cef.Shutdown()
        )

    static member Run(args: string[]) =
        let exitCode = SelfHost.Main args
        if exitCode >= 0 then Environment.Exit exitCode

    static member CreateBrowser(runtime: IRuntime, size: aval<V2i>, mipMap: bool,
                                [<Optional; DefaultParameterValue(null: BrowserSettings)>] settings: BrowserSettings,
                                [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        if not AardvarkCef.IsInitialized then raise <| InvalidOperationException("Aardvark CEF has not been initialized.")
        AardvarkCefBrowser.Create(runtime, size, mipMap, settings, requestContext)