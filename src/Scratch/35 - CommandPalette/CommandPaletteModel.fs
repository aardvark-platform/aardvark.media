namespace CommandPalette

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify

/// A command shown in the palette. Ids have to be unique within one step.
type PaletteCommand<'msg> =
    {
        id       : string
        title    : string
        category : string
        detail   : string
        action   : PaletteCommandKind<'msg>
    }

and PaletteCommandKind<'msg> =
    /// Executes the message and closes the palette.
    | Run  of 'msg
    /// Opens a second step listing the children (e.g. "Select Box..." -> list of boxes).
    | Pick of placeholder : string * children : list<PaletteCommand<'msg>>

[<ModelType>]
type PaletteState =
    {
        isOpen : bool
        /// Ids of the opened Pick commands, root first. Empty = root list.
        stack  : list<string>
    }

type PaletteMessage =
    | Open
    | Close
    | Back
    | Execute of string

// DTOs sent to the client, serialized as JSON.
type PaletteItem =
    {
        id          : string
        title       : string
        category    : string
        detail      : string
        hasChildren : bool
    }

type PaletteStep =
    {
        key         : string
        placeholder : string
        items       : PaletteItem[]
    }
