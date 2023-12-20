namespace Aardvark.UI.Primitives.Golden

open Adaptify
open System

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
    { Id       : string
      Title    : string
      Closable : bool
      Header   : Header option
      Buttons  : Buttons
      Size     : Size }

type Stack =
    { Header  : Header
      Buttons : Buttons
      Size    : Size
      Content : Element list }

type RowOrColumn =
    { IsRow   : bool
      Size    : Size
      Content : Layout list }

and [<RequireQualifiedAccess>] Layout =
    | Element     of Element
    | Stack       of Stack
    | RowOrColumn of RowOrColumn

type Theme =
    | Theme of resourcePath: string
    member inline x.Path = let (Theme p) = x in p

type LayoutConfig =
    {
        /// The color theme to use.
        Theme            : Theme

        /// Determines whether popouts are automatically docked when the window is closed.
        /// Shows a small dock button in popouts if false.
        PopInOnClose     : bool

        /// Tooltip label of minimize button.
        LabelMinimize    : string

        /// Tooltip label of maximize button.
        LabelMaximize    : string

        /// Tooltip label of pop-out button.
        LabelPopOut      : string

        /// Tooltip label of pop-in / dock button.
        LabelPopIn       : string

        /// Tooltip label of close button.
        LabelClose       : string

        /// Tooltip label of stack tab dropdown.
        /// The dropdown is only visible when a stack has too many tabs to display at once.
        LabelTabDropdown : string
    }

[<ModelType>]
type GoldenLayout =
    {
        [<NonAdaptive>]
        DefaultLayout : Layout

        [<NonAdaptive>]
        Config        : LayoutConfig

        [<TreatAsValue>]
        SetLayout     : Option<Layout * int>

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

        /// Saves the current layout in local storage with the given key.
        | SaveLayout of key: string

        /// Loads the layout from local storage with the given key.
        | LoadLayout of key: string