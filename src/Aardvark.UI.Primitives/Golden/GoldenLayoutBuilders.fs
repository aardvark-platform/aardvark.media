namespace Aardvark.UI.Primitives.Golden

open Aardvark.Base

[<AutoOpen>]
module Builders =

    module ElementError =
        type IdMustBeSpecified = IdMustBeSpecified

    type ElementBuilder() =
        member inline x.Yield(()) = ElementError.IdMustBeSpecified

        /// Unique name to identify the element in the view function.
        [<CustomOperation("id")>]
        member inline x.Id(_ : ElementError.IdMustBeSpecified, id : string) =
            { Id        = id
              Title     = "Untitled"
              Closable  = true
              Header    = Some Header.Top
              Buttons   = None
              MinSize   = None
              Size      = Size.Weight 1
              KeepAlive = true }

        /// Title shown in the header tab. Default is "Untitled".
        [<CustomOperation("title")>]
        member inline x.Title(e : Element, title : string) =
            { e with Title = title }

        /// Determines if the element can be closed via buttons in the header and tab.
        [<CustomOperation("closable")>]
        member inline x.Closable(e : Element, closable : bool) =
            { e with Closable = closable }

        /// Determines the position of the header or if one is shown at all. Default is Header.Top.
        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header option) =
            { e with Header = header }

        /// Determines the position of the header. Default is Header.Top.
        [<CustomOperation("header")>]
        member inline x.Header(e : Element, header : Header) =
            { e with Header = Some header }

        /// Buttons to display in the header.
        [<CustomOperation("buttons")>]
        member inline x.Buttons(e : Element, buttons : Buttons) =
            { e with Buttons = Some buttons }

        /// Minimum size (in pixels) of the element in any dimension.
        [<CustomOperation("minSize")>]
        member inline x.MinSize(e : Element, sizeInPixels : int) =
            { e with MinSize = Some sizeInPixels }

        /// Size of the element in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(e : Element, size : Size) =
            { e with Size = size }

        /// Size of the element (in percent) in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(e : Element, sizeInPercent : int) =
            { e with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(e : Element, weight : int) =
            { e with Size = Size.Weight weight }

        /// If true the DOM element is hidden rather than destroyed if it is removed from the layout.
        /// This allows for faster restoring of the element but may come with a performance penalty. Default is true.
        [<CustomOperation("keepAlive")>]
        member inline x.KeepAlive(e : Element, keepAlive : bool) =
            { e with KeepAlive = keepAlive }

    type StackBuilder() =
        static let empty =
            { Header  = Header.Top
              Buttons = None
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(e : Element) = { empty with Content = [e] }
        member x.Yield(e : Element seq) = { empty with Content = List.ofSeq e }

        member x.Delay(f : unit -> Stack) = f()
        member x.Combine(a : Stack, b : Stack) = { a with Content = a.Content @ b.Content }
        member x.For(s: Stack, f: unit -> Stack) = x.Combine(s, f())

        /// Determines the position of the header.
        [<CustomOperation("header")>]
        member inline x.Header(s : Stack, header : Header) =
            { s with Header = header }

        /// Buttons to display in the header.
        [<CustomOperation("buttons")>]
        member inline x.Buttons(s : Stack, buttons : Buttons) =
            { s with Buttons = Some buttons }

        /// Size of the stack in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, size : Size) =
            { s with Size = size }

        /// Size of the stack (in percent) in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(s : Stack, sizeInPercent : int) =
            { s with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(s : Stack, weight : int) =
            { s with Size = Size.Weight weight }

        /// Content of the stack.
        [<CustomOperation("content")>]
        member inline x.Content(s : Stack, c : Element seq) =
            { s with Content = List.ofSeq c }

    type RowOrColumnBuilder(isRow : bool) =
        let empty =
            { IsRow   = isRow
              Content = []
              Size    = Size.Weight 1 }

        member x.Yield(()) = empty
        member x.Yield(l : Layout)           = { empty with Content = [l] }
        member x.Yield(e : Element)          = x.Yield(Layout.Element e)
        member x.Yield(s : Stack)            = x.Yield(Layout.Stack s)
        member x.Yield(rc : RowOrColumn)     = x.Yield(Layout.RowOrColumn rc)
        member x.Yield(l : Layout seq)       = { empty with Content = List.ofSeq l }
        member x.Yield(e : Element seq)      = x.Yield(e |> Seq.map Layout.Element)
        member x.Yield(s : Stack seq)        = x.Yield(s |> Seq.map Layout.Stack)
        member x.Yield(rc : RowOrColumn seq) = x.Yield(rc |> Seq.map Layout.RowOrColumn)

        member x.Delay(f : unit -> RowOrColumn) = f()
        member x.Combine(a : RowOrColumn, b : RowOrColumn) = { a with Content = a.Content @ b.Content }
        member x.For(rc: RowOrColumn, f: unit -> RowOrColumn) = x.Combine(rc, f())

        /// Size of the container in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, size : Size) =
            { rc with Size = size }

        /// Size of the container (in percent) in case the parent is a row or column container.
        [<CustomOperation("size")>]
        member inline x.Size(rc : RowOrColumn, sizeInPercent : int) =
            { rc with Size = Size.Percentage sizeInPercent }

        /// Size as weight relative to siblings in case the parent is a row or column container.
        [<CustomOperation("weight")>]
        member inline x.Weight(rc : RowOrColumn, weight : int) =
            { rc with Size = Size.Weight weight }

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Layout seq) =
            { rc with Content = List.ofSeq c }

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Element seq) =
            x.Content(rc, c |> Seq.map Layout.Element)

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : Stack seq) =
            x.Content(rc, c |> Seq.map Layout.Stack)

        /// Content of the container.
        [<CustomOperation("content")>]
        member inline x.Content(rc : RowOrColumn, c : RowOrColumn seq) =
            x.Content(rc, c |> Seq.map Layout.RowOrColumn)

    module PopoutWindowError =
        type RootLayoutMustBeSpecified = RootLayoutMustBeSpecified

    type PopoutWindowState<'T> = 'T * V2i option * V2i option

    type PopoutWindowBuilder() =
        member inline x.Yield(()) :  PopoutWindowState<PopoutWindowError.RootLayoutMustBeSpecified> = PopoutWindowError.RootLayoutMustBeSpecified, None, None
        member inline x.Yield(r : ^Layout) : PopoutWindowState<Layout> = Layout.ofRoot r, None, None

        member inline x.Delay(f : unit -> PopoutWindowState<'T>) = f()

        member inline x.Combine((l, p1, s1) : PopoutWindowState<'T1>, (_, p2, s2) : PopoutWindowState<'T2>) : PopoutWindowState<'T1> =
            (l, p2 |> Option.orElse p1, s2 |> Option.orElse s1 )

        member inline x.For(s : PopoutWindowState<'T1>, f: unit -> PopoutWindowState<'T2>) = x.Combine(s, f())

        // Layout of the window contents.
        [<CustomOperation("root")>]
        member inline x.Root((_, position, size) : PopoutWindowState<'T>, root : ^Root) : PopoutWindowState<Layout> =
            Layout.ofRoot root, position, size

        /// Determines the position of the popout window.
        [<CustomOperation("position")>]
        member inline x.Position((root, _, size) : PopoutWindowState<'T>, position : V2i) : PopoutWindowState<'T> =
            root, Some position, size

        /// Determines the size of the popout window.
        [<CustomOperation("size")>]
        member inline x.Size((root, position, _) : PopoutWindowState<'T>, size : V2i) : PopoutWindowState<'T> =
            root, position, Some size

        /// Determines the width of the popout window.
        [<CustomOperation("width")>]
        member inline x.Width((root, position, size) : PopoutWindowState<'T>, width : int) : PopoutWindowState<'T> =
            let size = size |> Option.defaultValue (V2i(300)) |> (fun s -> Some <| V2i(width, s.Y))
            root, position, size

        /// Determines the width of the popout window.
        [<CustomOperation("height")>]
        member inline x.Height((root, position, size) : PopoutWindowState<'T>, height : int) : PopoutWindowState<'T> =
            let size = size |> Option.defaultValue (V2i(300)) |> (fun s -> Some <| V2i(s.X, height))
            root, position, size

        member inline x.Run((root, position, size) : PopoutWindowState<Layout>) =
            { Root = root; Position = position; Size = size }

    type WindowLayoutBuilder() =
        member inline x.Yield(()) = { Root = None; PopoutWindows = [] }
        member inline x.Yield(r : ^Layout) =  { Root = Some <| Layout.ofRoot r; PopoutWindows = [] }
        member inline x.Yield(p : PopoutWindow) =  { Root = None; PopoutWindows = [p] }

        member inline x.Delay(f : unit -> WindowLayout) = f()
        member inline x.Combine(a : WindowLayout, b : WindowLayout) = { a with PopoutWindows = a.PopoutWindows @ b.PopoutWindows }
        member inline x.For(l: WindowLayout, f: unit -> WindowLayout) = x.Combine(l, f())

        // Layout of the main window.
        [<CustomOperation("root")>]
        member inline x.Root(l : WindowLayout, root : ^Root) = { l with Root = Some <| Layout.ofRoot root }

        // Sequence of popout windows.
        [<CustomOperation("popouts")>]
        member inline x.Popouts(l : WindowLayout, popouts : PopoutWindow seq) = { l with PopoutWindows = List.ofSeq popouts }

    let element = ElementBuilder()
    let stack = StackBuilder()
    let row = RowOrColumnBuilder true
    let column = RowOrColumnBuilder false
    let popout = PopoutWindowBuilder()
    let layout = WindowLayoutBuilder()