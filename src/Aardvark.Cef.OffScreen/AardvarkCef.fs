namespace Aardvark.Cef.OffScreen

open Aardvark.Base
open Aardvark.Rendering
open CefSharp
open CefSharp.Handler
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
    static let mutable cacheLock = Disposable.empty

    // The root cache directory must not be used concurrently by different CEF instances.
    // If already in use, try to find another one.
    static let getCacheDirectory =
        let tryLock (path: string) =
            let mutable created = false
            let mutex = new Mutex(true, @"Global\AardvarkCef_" + path.Replace('\\', '_'), &created)

            if created then
                cacheLock.Dispose()
                cacheLock <- mutex
                path
            else
                mutex.Dispose()
                null

        fun (path: string) ->
            let rec retry (n: int) =
                match tryLock $"{path}_{n}" with
                | null -> retry (n + 1)
                | path -> path

            if String.IsNullOrEmpty path then
                path
            else
                match tryLock path with
                | null -> retry 0
                | path -> path

    static let getFullPathSafe (path: string) =
        if String.IsNullOrWhiteSpace path then null
        else try Path.GetFullPath path with _ -> null

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
        settings.CefCommandLineArgs.Add "disable-pinch"
        settings

    static member Init([<Optional; DefaultParameterValue(null: CefSettings)>] settings: CefSettings,
                       [<Optional; DefaultParameterValue(true)>] performDependencyCheck: bool) =
        lock lockObj (fun _ ->
            if not AardvarkCef.IsInitialized then
                let s = settings ||? AardvarkCef.DefaultSettings
                use _ = if notNull settings then Disposable.empty else s

                // Find a root cache directory that is not in use.
                // CachePath must be a direct child of the RootCachePath.
                let rootCachePath, cacheName =
                    let rootPath =
                        let path = if String.IsNullOrEmpty s.RootCachePath then s.CachePath else s.RootCachePath
                        getFullPathSafe path

                    let name =
                        let path = getFullPathSafe s.CachePath
                        if path = rootPath then null else Path.GetFileName path

                    getCacheDirectory rootPath, name

                s.RootCachePath <- rootCachePath
                s.CachePath <- if isNull rootCachePath || isNull cacheName then rootCachePath else Path.Combine(rootCachePath, cacheName)

                // Handle relaunches by ignoring them, otherwise blank Chromium windows will open.
                // This happens when two instances use the same root cache directory.
                let browserProcessHandler =
                    { new BrowserProcessHandler() with
                        override this.OnAlreadyRunningAppRelaunch(_, _) = Log.warn "[CEF] Ignoring relaunch attempt"; true }

                if not <| Cef.Initialize(s, performDependencyCheck, browserProcessHandler) then
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
                cacheLock.Dispose()
                cacheLock <- Disposable.empty
        )

    static member Run(args: string[]) =
        let exitCode = SelfHost.Main args
        if exitCode >= 0 then Environment.Exit exitCode

    static member CreateBrowser(runtime: IRuntime, size: aval<V2i>, mipMap: bool,
                                [<Optional; DefaultParameterValue(null: BrowserSettings)>] settings: BrowserSettings,
                                [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        if not AardvarkCef.IsInitialized then raise <| InvalidOperationException("Aardvark CEF has not been initialized.")
        AardvarkCefBrowser.Create(runtime, size, mipMap, settings, requestContext)