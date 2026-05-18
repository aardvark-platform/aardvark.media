namespace Aardvark.Cef.OffScreen

open Aardvark.Application
open CefSharp

type internal BrowserKeyboard(host: IBrowserHost, focus: ref<bool>, flags: ref<CefEventFlags>) =
    inherit EventKeyboard()

    let processKey (k : Keys) (down : bool) =
        let (|&~) f g =
            if down then f ||| g
            else f &&& ~~~g

        match k with
        | Keys.LeftCtrl | Keys.RightCtrl   -> flags.Value <- flags.Value |&~ CefEventFlags.ControlDown
        | Keys.LeftAlt | Keys.RightAlt     -> flags.Value <- flags.Value |&~ CefEventFlags.AltDown
        | Keys.LeftShift | Keys.RightShift -> flags.Value <- flags.Value |&~ CefEventFlags.ShiftDown
        | _ -> ()

    member x.Flags = flags

    override x.KeyDown(k : Keys) =
        if focus.Value then
            base.KeyDown(k)
            let mutable e = KeyEvent()
            e.Type <- KeyEventType.KeyDown
            e.WindowsKeyCode <- KeyConverter.virtualKeyFromKey k
            e.NativeKeyCode <- KeyConverter.virtualKeyFromKey k
            e.Modifiers <- flags.Value
            host.SendKeyEvent(e)

            processKey k true

    override x.KeyUp(k : Keys) =
        if focus.Value then
            base.KeyUp(k)
            let mutable e = KeyEvent()
            e.Type <- KeyEventType.KeyUp
            e.WindowsKeyCode <- KeyConverter.virtualKeyFromKey k
            e.NativeKeyCode <- KeyConverter.virtualKeyFromKey k
            e.Modifiers <- flags.Value
            host.SendKeyEvent(e)

            processKey k false

    override x.KeyPress(c : char) =
        if focus.Value then
            base.KeyPress(c)
            let mutable e = KeyEvent()
            e.WindowsKeyCode <- int c
            e.Type <- KeyEventType.Char
            e.Modifiers <- flags.Value
            host.SendKeyEvent(e)