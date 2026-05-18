namespace Aardvark.Cef.WinForms

open System.Runtime.InteropServices

type DpiMode =
    /// The application window does not scale for DPI changes and always assumes a scale factor of 100%.
    | Unaware          = 0

    /// The window queries for the DPI of the primary monitor once and uses this for the application on all monitors.
    | SystemAware      = 1

    /// The window checks for DPI when it's created and adjusts scale factor when the DPI changes.
    | PerMonitor       = 2

    /// Similar to PerMonitor, but enables child window DPI change notification, improved scaling of comctl32 controls, and dialog scaling.
    | PerMonitorV2     = 3

    /// Similar to DpiUnaware, but improves the quality of GDI/GDI+ based content.
    | UnawareGdiScaled = 4

module internal DpiAwarenessContext =
    let Unaware          = -1n
    let SystemAware      = -2n
    let PerMonitor       = -3n
    let PerMonitorV2     = -4n
    let UnawareGdiScaled = -5n

module internal DpiMode =
    let ofAwarenessContext (context: nativeint) =
        if context = DpiAwarenessContext.Unaware then DpiMode.Unaware
        elif context = DpiAwarenessContext.SystemAware then DpiMode.SystemAware
        elif context = DpiAwarenessContext.PerMonitor then DpiMode.PerMonitor
        elif context = DpiAwarenessContext.PerMonitorV2 then DpiMode.PerMonitorV2
        elif context = DpiAwarenessContext.UnawareGdiScaled then DpiMode.UnawareGdiScaled
        else DpiMode.Unaware

    let toAwarenessContext (mode: DpiMode) : nativeint =
        match mode with
        | DpiMode.Unaware          -> DpiAwarenessContext.Unaware
        | DpiMode.SystemAware      -> DpiAwarenessContext.SystemAware
        | DpiMode.PerMonitor       -> DpiAwarenessContext.PerMonitor
        | DpiMode.PerMonitorV2     -> DpiAwarenessContext.PerMonitorV2
        | DpiMode.UnawareGdiScaled -> DpiAwarenessContext.UnawareGdiScaled
        | _                        -> DpiAwarenessContext.Unaware

module internal User32 =
    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool SetProcessDpiAwarenessContext(nativeint value)

    [<DllImport("user32.dll")>]
    extern nativeint GetThreadDpiAwarenessContext();