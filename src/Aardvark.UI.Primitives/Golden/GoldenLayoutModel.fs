namespace Aardvark.UI.Primitives.Golden

open Adaptify
open System
open Aardvark.Base

[<RequireQualifiedAccess>]
type Size =
    | Weight     of int
    | Percentage of int

    override x.ToString() =
        match x with
        | Weight v -> $"{v}fr"
        | Percentage v -> $"{v}%%"

type Header =
    | Top    = 0
    | Left   = 1
    | Right  = 2
    | Bottom = 3

[<Flags>]
type Buttons =
    | None     = 0
    | Close    = 1
    | Popout   = 2
    | Maximize = 4
    | All      = 7

type Element =
    {
        /// Unique name to identify the element in the view function.
        Id        : string

        /// Title shown in the header tab.
        Title     : string

        /// Determines if the element can be closed via buttons in the header and tab.
        Closable  : bool

        /// Determines the position of the header or if one is shown at all.
        Header    : Header option

        /// Buttons to display in the header.
        Buttons   : Buttons option

        /// Minimum size (in pixels) of the element in any dimension.
        MinSize   : int option

        /// Size of the element in case the parent is a row or column container.
        Size      : Size

        /// If true the DOM element is hidden rather than destroyed if it is removed from the layout.
        /// This allows for faster restoring of the element but may come with a performance penalty. Default is true.
        KeepAlive : bool
    }

type Stack =
    {
        /// Determines the position of the header.
        Header  : Header

        /// Buttons to display in the header.
        Buttons : Buttons option

        /// Size of the element in case the parent is a row or column container.
        Size    : Size

        /// Children of the stack.
        Content : Element list
    }

type RowOrColumn =
    {
        /// True if row container, false if column container.
        IsRow   : bool

        /// Size of the element in case the parent is a row or column container.
        Size    : Size

        /// Children of the container.
        Content : Layout list
    }

and [<RequireQualifiedAccess>] Layout =
    | Element     of Element
    | Stack       of Stack
    | RowOrColumn of RowOrColumn

type PopoutWindow =
    {
        // Layout of the window contents.
        Root : Layout

        // Position on the screen.
        Position : V2i option

        // Size in pixels.
        Size : V2i option
    }

type WindowLayout =
    {
        // Layout of the main window.
        Root : Layout option

        // List of popout windows.
        PopoutWindows : PopoutWindow list
    }

type Theme =
    | Theme of resourcePath: string
    member inline x.Path = let (Theme p) = x in p

type LayoutConfig =
    {
        /// The color theme to use. Default is Theme.BorderlessDark.
        Theme : Theme

        /// Determines whether items in popout windows are automatically docked when the window is closed.
        /// Shows a small dock button in popout windows if false. Default is true.
        PopInOnClose : bool

        /// Determines whether the popout header button affects the whole stack or just the active tab. Default is true.
        PopOutWholeStack : bool

        /// Determines whether elements may be dragged from one window to another. Default is true.
        DragBetweenWindows : bool

        /// Determines whether elements may be dragged and dropped outside the containing window creating a new popout window. Default is true.
        DragToNewWindow : bool

        /// Default buttons to be displayed in the headers.
        HeaderButtons : Buttons

        /// Determines whether the document.title of popout windows is set and updated automatically to the document.title of the main window.
        SetPopoutTitle : bool

        /// Default minimum width (in pixels) of any item. Default is 20.
        MinItemWidth : int

        /// Default minimum height (in pixels) of any item. Default is 20.
        MinItemHeight : int

        /// Width (in pixels) of drag proxy / preview elements. Default is 300.
        DragProxyWidth : int

        /// Height (in pixels) of drag proxy / preview elements. Default is 200.
        DragProxyHeight : int

        /// Tooltip label of minimize button.
        LabelMinimize : string

        /// Tooltip label of maximize button.
        LabelMaximize : string

        /// Tooltip label of pop-out button.
        LabelPopOut : string

        /// Tooltip label of pop-in / dock button.
        /// Only visible if PopInOnClose is false.
        LabelPopIn : string

        /// Tooltip label of close button.
        LabelClose : string

        /// Tooltip label of stack tab dropdown.
        /// The dropdown is only visible when a stack has too many tabs to display at once.
        LabelTabDropdown : string
    }

[<ModelType>]
type GoldenLayout =
    {
        [<NonAdaptive>]
        DefaultLayout : WindowLayout

        [<NonAdaptive>]
        Config        : LayoutConfig

        [<TreatAsValue>]
        SetLayout     : Option<WindowLayout * int>

        [<TreatAsValue>]
        SaveLayout    : Option<string * int>

        [<TreatAsValue>]
        LoadLayout    : Option<string * int>
    }

module GoldenLayout =

    [<RequireQualifiedAccess>]
    type Message =

        /// Restores the default layout.
        | ResetLayout

        /// Sets the given layout.
        | SetLayout of layout: Layout

        /// Sets the given window layout.
        | SetWindowLayout of layout: WindowLayout

        /// Saves the current layout in local storage with the given key.
        | SaveLayout of key: string

        /// Loads the layout from local storage with the given key.
        | LoadLayout of key: string