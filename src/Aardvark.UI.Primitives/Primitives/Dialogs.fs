namespace Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.UI
open FSharp.Data.Adaptive
open System

type FileFilter =
    { Name       : string
      Extensions : string list }

type DialogMode =
    | OpenFile   = 0
    | SaveFile   = 1
    | OpenFolder = 2

type DialogConfig =
    {
        /// Title displayed for the dialog window.
        Title       : string

        /// List of filters to apply for file dialogs (description and list of file extensions).
        Filters     : List<string * string list>

        /// If true, multiple files and folders may be chosen.
        Multiselect : bool

        /// If true, the dialog stays on top of the main window.
        Modal       : bool

        /// Initial path.
        DefaultPath : string
    }

    static member Default =
        { Title       = null
          Filters     = []
          Multiselect = true
          Modal       = true
          DefaultPath = null }

module Dialog =

    module private AttributeMap =
        let toList (attributes: AttributeMap<'msg>) : Attribute<'msg> list =
            let map = attributes.Content.GetValue()
            HashMap.toList map

    module private DialogConfig =

        let private removeDot str =
            if str |> String.startsWith "." then str.Substring 1
            else str

        let toElectronJson (mode: DialogMode) (config: DialogConfig) =
            let filters =
                config.Filters |> List.map (fun (name, extensions) ->
                    let extensions = extensions |> List.map (removeDot >> Pickler.jsonToString) |> String.concat ", "
                    $"{{ name: {Pickler.jsonToString (name ||? String.Empty)}, extensions: [{extensions}]}}"
                )
                |> String.concat ", "

            let properties =
                [
                    if mode = DialogMode.OpenFile then "openFile"
                    elif mode = DialogMode.OpenFolder then "openDirectory"
                    if config.Multiselect then "multiSelections"
                    if config.Modal then "modal" // Handled in aardvark.js for Aardium
                ]
                |> List.map Pickler.jsonToString
                |> String.concat ", "

            [
                $"filters: [{filters}]"
                $"properties: [{properties}]"

                if not <| String.IsNullOrEmpty config.Title then
                    $"title: {Pickler.jsonToString config.Title}"

                if not <| String.IsNullOrEmpty config.DefaultPath then
                    $"defaultPath: {Pickler.jsonToString config.DefaultPath}"
            ]
            |> String.concat ", "
            |> sprintf "{ %s }"

    module Incremental =

        let private (|PathList|) =
            Pickler.json.UnPickleOfString >> List.map Path.ofUnixStyle

        let dialog (mode: DialogMode) (callback: string list -> 'msg) (config: aval<DialogConfig>)
                   (event: string) (node: AttributeMap<'msg> -> DomNode<'msg>) =

            let javascript (config: DialogConfig) =
                let cfg = DialogConfig.toElectronJson mode config

                if mode = DialogMode.SaveFile then
                    $"aardvark.saveFileDialog({cfg}, path => aardvark.processEvent('__ID__', 'onchoosepaths', path ? [path] : []));"
                else
                    $"aardvark.openFileDialog({cfg}, paths => aardvark.processEvent('__ID__', 'onchoosepaths', paths));"

            let attributes =
                AttributeMap.ofAMap <| amap {
                    let! config = config
                    yield clientEvent event <| javascript config
                    yield onEvent' "onchoosepaths" [] (function
                        | PathList paths::_ when paths.Length > 0 -> [ callback paths ]
                        | _ -> []
                    )
                }

            node attributes

        let inline button (mode: DialogMode) (callback: string list -> 'msg) (config: aval<DialogConfig>) attributes content =
            dialog mode callback config "onclick" (fun dialogAtt ->
                let attributes = AttributeMap.union dialogAtt (Generic.att attributes)
                Incremental.button attributes content
            )

        let inline openFileButton (callback: string -> 'msg) (config: aval<DialogConfig>) attributes content =
            let config = config |> AVal.map (fun config -> { config with Multiselect = false })
            button DialogMode.OpenFile (List.head >> callback) config attributes content

        let inline openFilesButton (callback: string list -> 'msg) (config: aval<DialogConfig>) attributes content =
            button DialogMode.OpenFile callback config attributes content

        let inline saveFileButton (callback: string -> 'msg) (config: aval<DialogConfig>) attributes content =
            button DialogMode.SaveFile (List.head >> callback) config attributes content

        let inline openFolderButton (callback: string -> 'msg) (config: aval<DialogConfig>) attributes content =
            let config = config |> AVal.map (fun config -> { config with Multiselect = false })
            button DialogMode.OpenFolder (List.head >> callback) config attributes content

        let inline openFoldersButton (callback: string list -> 'msg) (config: aval<DialogConfig>) attributes content =
            button DialogMode.OpenFolder callback config attributes content

    let dialog (mode: DialogMode) (callback: string list -> 'msg) (config: DialogConfig) (event: string) (node: Attribute<'msg> list -> DomNode<'msg>) =
        Incremental.dialog mode callback (AVal.constant config) event (AttributeMap.toList >> node)

    let button (mode: DialogMode) (callback: string list -> 'msg) (config: DialogConfig) attributes content : DomNode<'msg> =
        Incremental.button mode callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)

    let openFileButton (callback: string -> 'msg) (config: DialogConfig) attributes content =
        Incremental.openFileButton callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)

    let openFilesButton (callback: string list -> 'msg) (config: DialogConfig) attributes content =
        Incremental.openFilesButton callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)

    let saveFileButton (callback: string -> 'msg) (config: DialogConfig) attributes content =
        Incremental.saveFileButton callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)

    let openFolderButton (callback: string -> 'msg) (config: DialogConfig) attributes content =
        Incremental.openFolderButton callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)

    let openFoldersButton (callback: string list -> 'msg) (config: DialogConfig) attributes content =
        Incremental.openFoldersButton callback (AVal.constant config) (AttributeMap.ofList attributes) (AList.ofList content)