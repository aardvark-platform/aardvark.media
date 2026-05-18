namespace Aardvark.Cef.OffScreen

open Aardvark.Application
open Aardvark.Base
open CefSharp

type BrowserMouse(host: IBrowserHost, focus : ref<bool>, flags : ref<CefEventFlags>) =
    inherit EventMouse(false)

    static let getEventFlags = function
        | MouseButtons.Left   -> CefEventFlags.LeftMouseButton
        | MouseButtons.Middle -> CefEventFlags.MiddleMouseButton
        | MouseButtons.Right  -> CefEventFlags.RightMouseButton
        | _ -> CefEventFlags.None

    static let getMouseButtonType = function
        | MouseButtons.Left   -> MouseButtonType.Left
        | MouseButtons.Middle -> MouseButtonType.Middle
        | MouseButtons.Right  -> MouseButtonType.Right
        | _ -> MouseButtonType.Left

    let processMouse (pp : PixelPosition) (b : MouseButtons) (down : bool) =
        let (|&~) f g =
            if down then f ||| g
            else f &&& ~~~g

        let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
        host.SendMouseClickEvent(e, getMouseButtonType b, not down, 1)
        flags.Value <- flags.Value |&~ getEventFlags b

    override x.Down(pp : PixelPosition, b : MouseButtons) =
        if focus.Value then
            base.Down(pp, b)
            processMouse pp b true

    override x.Up(pp : PixelPosition, b : MouseButtons) =
        if focus.Value then
            base.Up(pp, b)
            processMouse pp b false

    override x.Click(pp : PixelPosition, b : MouseButtons) =
        if focus.Value then
            base.Click(pp, b)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseClickEvent(e, getMouseButtonType b, false, 1)

    override x.DoubleClick(pp : PixelPosition, b : MouseButtons) =
        if focus.Value then
            base.DoubleClick(pp, b)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseClickEvent(e, getMouseButtonType b, true, 2)

    override x.Scroll(pp : PixelPosition, delta : float) =
        if focus.Value then
            base.Scroll(pp, delta)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseWheelEvent(e, 0, int delta)

    override x.Enter(pp : PixelPosition) =
        if focus.Value then
            base.Enter(pp)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseMoveEvent(e, false)

    override x.Leave(pp : PixelPosition) =
        if focus.Value then
            base.Enter(pp)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseMoveEvent(e, true)

    override x.Move(pp : PixelPosition) =
        if focus.Value then
            base.Move(pp)
            let e = MouseEvent(pp.Position.X, pp.Position.Y, flags.Value)
            host.SendMouseMoveEvent(e, false)