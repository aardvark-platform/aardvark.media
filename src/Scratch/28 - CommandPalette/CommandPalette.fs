namespace CommandPalette

open System.Text.Json
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI

module PaletteCommand =

    let create (id : string) (category : string) (title : string) (detail : string) (action : PaletteCommandKind<'msg>) : PaletteCommand<'msg> =
        { id = id; title = title; category = category; detail = detail; action = action }

    let run (category : string) (title : string) (detail : string) (msg : 'msg) : PaletteCommand<'msg> =
        { id = $"{category}: {title}"; title = title; category = category; detail = detail; action = Run msg }

    let pick (category : string) (title : string) (placeholder : string) (children : list<PaletteCommand<'msg>>) : PaletteCommand<'msg> =
        { id = $"{category}: {title}"; title = title + "..."; category = category; detail = ""; action = Pick (placeholder, children) }

[<RequireQualifiedAccess>]
module CommandPalette =

    let rootPlaceholder = "Type the name of a command to run"

    let initial = { isOpen = false; stack = [] }

    /// Returns the placeholder and the commands of the step referenced by the stack.
    let private resolve (stack : list<string>) (commands : list<PaletteCommand<'msg>>) =
        let rec go placeholder commands stack =
            match stack with
            | [] -> Some (placeholder, commands)
            | id :: rest ->
                commands
                |> List.tryPick (fun c ->
                    match c.action with
                    | Pick (p, children) when c.id = id -> Some (p, children)
                    | _ -> None
                )
                |> Option.bind (fun (p, children) -> go p children rest)

        go rootPlaceholder commands stack

    /// Updates the palette, returning the message of an executed command (if any).
    let update (commands : list<PaletteCommand<'msg>>) (model : PaletteState) (msg : PaletteMessage) : PaletteState * option<'msg> =
        match msg with
        | Open ->
            { isOpen = true; stack = [] }, None

        | Close ->
            initial, None

        | Back ->
            match model.stack with
            | [] -> model, None
            | s -> { model with stack = List.take (s.Length - 1) s }, None

        | Execute id ->
            match resolve model.stack commands with
            | Some (_, current) ->
                match current |> List.tryFind (fun c -> c.id = id) with
                | Some { action = Run msg } -> initial, Some msg
                | Some { action = Pick _ } -> { model with stack = model.stack @ [id] }, None
                | None ->
                    Log.warn "[CommandPalette] unknown command '%s'" id
                    model, None
            | None ->
                { model with stack = [] }, None

    let private toStep (stack : list<string>) (commands : list<PaletteCommand<'msg>>) =
        let key, placeholder, current =
            match resolve stack commands with
            | Some (p, current) -> String.concat "/" stack, p, current
            | None -> "", rootPlaceholder, commands

        let items =
            current |> List.toArray |> Array.map (fun c ->
                {
                    id          = c.id
                    title       = c.title
                    category    = c.category
                    detail      = c.detail
                    hasChildren = match c.action with Pick _ -> true | _ -> false
                }
            )

        { key = key; placeholder = placeholder; items = items }

    let private dependencies =
        [
            { kind = Stylesheet; name = "commandPaletteCss"; url = "resources/commandPalette.css" }
            { kind = Script;     name = "commandPaletteJs";  url = "resources/commandPalette.js" }
        ]

    /// Overlay opened via Ctrl+P (or F1). Filtering and keyboard navigation happen client-side,
    /// only the id of the executed command is sent back.
    let view (lift : PaletteMessage -> 'msg) (commands : aval<list<PaletteCommand<'msg>>>) (model : AdaptivePaletteState) : DomNode<'msg> =
        let step =
            (model.stack, commands)
            ||> AVal.map2 toStep
            |> AVal.map JsonSerializer.Serialize

        let execute (args : list<string>) =
            match args with
            | id :: _ ->
                try Seq.singleton (lift (Execute (Pickler.unpickleOfJson id)))
                with _ -> Seq.empty
            | _ -> Seq.empty

        let boot = "aardvark.commandPalette(document.getElementById('__ID__'), paletteStep, paletteOpen);"

        require dependencies (
            onBoot' ["paletteStep", AVal.channel step; "paletteOpen", AVal.channel model.isOpen] boot (
                div [
                    clazz "cp-overlay"
                    onEvent' "cp-execute" [] execute
                    onEvent "cp-open"  [] (fun _ -> lift Open)
                    onEvent "cp-close" [] (fun _ -> lift Close)
                    onEvent "cp-back"  [] (fun _ -> lift Back)
                ] [
                    div [ clazz "cp-box" ] [
                        input [
                            clazz "cp-input"
                            attribute "type" "text"
                            attribute "spellcheck" "false"
                            attribute "autocomplete" "off"
                        ]
                        div [ clazz "cp-list" ] []
                    ]
                ]
            )
        )
