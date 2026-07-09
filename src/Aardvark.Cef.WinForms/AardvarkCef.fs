namespace Aardvark.Cef.WinForms

open Aardvark.Base
open System
open System.IO
open System.Runtime.InteropServices
open System.Threading
open CefSharp
open CefSharp.Handler
open CefSharp.Web
open CefSharp.WinForms
open CefSharp.BrowserSubprocess

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

    /// <summary>
    /// Returns the DPI mode for the current thread (or process if it has not been set for the thread).
    /// </summary>
    /// <remarks>Only supported on Windows 10 or higher.</remarks>
    static member GetThreadDpiMode() =
        let os = Environment.OSVersion
        if os.Platform = PlatformID.Win32NT && os.Version.Major >= 10 then
            User32.GetThreadDpiAwarenessContext() |> DpiMode.ofAwarenessContext
        else
            DpiMode.Unaware

    /// <summary>
    /// Sets the DPI mode for the process.
    /// </summary>
    /// <remarks>Only supported on Windows 10 or higher.</remarks>
    /// <param name="mode">Mode to set.</param>
    /// <returns>Result code of the Win32 call (success = 0).</returns>
    static member SetProcessDpiMode(mode: DpiMode) =
        let os = Environment.OSVersion
        if os.Platform = PlatformID.Win32NT && os.Version.Major >= 10 then
            let context = DpiMode.toAwarenessContext mode
            if User32.SetProcessDpiAwarenessContext context then
                0
            else
                Marshal.GetLastWin32Error()
        else
            50 // ERROR_NOT_SUPPORTED

    static member DefaultSettings =
        let settings = new CefSettings()
        settings.MultiThreadedMessageLoop <- true
        settings.CachePath <- Path.Combine(Environment.CurrentDirectory, "cef_cache")
        settings.LogFile <- Path.Combine(Environment.CurrentDirectory, "cef.log")
        settings.LogSeverity <- LogSeverity.Warning
        settings.IgnoreCertificateErrors <- true
        settings.CommandLineArgsDisabled <- false
        settings.WindowlessRenderingEnabled <- false
        settings.CefCommandLineArgs.Add "no-proxy-server"
        settings.CefCommandLineArgs.Add "disable-pinch"
        settings

    static member Init([<Optional; DefaultParameterValue(null: CefSettings)>] settings: CefSettings,
                       [<Optional; DefaultParameterValue(DpiMode.PerMonitorV2)>] dpiMode: DpiMode,
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
                        if path = rootPath then "" else Path.GetFileName path

                    getCacheDirectory rootPath, name

                s.RootCachePath <- rootCachePath
                s.CachePath <- if isNull rootCachePath || isNull cacheName then null else Path.Combine(rootCachePath, cacheName)

                if dpiMode <> DpiMode.Unaware then
                    AardvarkCef.SetProcessDpiMode dpiMode |> ignore

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

    static member Run(args: string[], [<Optional; DefaultParameterValue(DpiMode.PerMonitorV2)>] dpiMode: DpiMode) =
        if dpiMode <> DpiMode.Unaware then AardvarkCef.SetProcessDpiMode dpiMode |> ignore
        let exitCode = SelfHost.Main args
        if exitCode >= 0 then Environment.Exit exitCode

    static member CreateBrowser(address: string, [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        if not AardvarkCef.IsInitialized then raise <| InvalidOperationException("Aardvark CEF has not been initialized.")
        AardvarkCefBrowser.Create(address, requestContext)

    static member CreateBrowser(html: HtmlString, [<Optional; DefaultParameterValue(null: IRequestContext)>] requestContext: IRequestContext) =
        if not AardvarkCef.IsInitialized then raise <| InvalidOperationException("Aardvark CEF has not been initialized.")
        AardvarkCefBrowser.Create(html, requestContext)