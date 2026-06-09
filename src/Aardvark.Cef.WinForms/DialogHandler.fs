namespace Aardvark.Cef.WinForms

open Aardvark.Base
open System
open System.Collections.Generic
open System.Windows.Forms

type AardvarkDialogHandler(parent: Control) =
    static let javascript =
        """
            if (!document.aardvark) document.aardvark = {};

            document.aardvark.dialog = {
                showOpenDialog: async function (window, options) {
                    await CefSharp.BindObjectAsync("aardvarkDialogHandler");
                    if (arguments.length === 1) { options = window; window = null; }
                    return aardvarkDialogHandler.showOpenDialog(options);
                },

                showSaveDialog: async function (window, options) {
                    await CefSharp.BindObjectAsync("aardvarkDialogHandler");
                    if (arguments.length === 1) { options = window; window = null; }
                    return aardvarkDialogHandler.showSaveDialog(options);
                }
            };
        """

    static let getProperty (name: string) (fallback: 'T) (obj: IDictionary<string, obj>) =
        match obj.TryGetValue name with
        | true, (:? 'T as value) -> value
        | _ -> fallback

    static let toFilterPattern (extensions: List<obj>) =
        extensions |> Seq.choose (function
            | :? string as ext -> Some $"*.{ext}"
            | _ -> None
        ) |> String.concat ";"

    static let getFilters (filters: List<obj>) =
        filters |> Seq.choose (function
            | :? IDictionary<string, obj> as filter ->
                let name = filter |> getProperty "name" String.Empty
                let exts = filter |> getProperty "extensions" (List<obj>()) |> toFilterPattern

                if String.IsNullOrEmpty name || String.IsNullOrEmpty exts then
                    None
                else
                    Some $"{name}|{exts}"
            | _ ->
                None
        ) |> String.concat "|"

    static member internal Javascript = javascript

    member _.ShowOpenDialog(options: IDictionary<string, obj>) =
        let filePaths = List<string>()

        let result = Dictionary<string, obj>()
        result.["filePaths"] <- filePaths

        try
            let title       = options |> getProperty "title" String.Empty
            let filter      = options |> getProperty "filters" (List<obj>()) |> getFilters
            let defaultPath = options |> getProperty "defaultPath" String.Empty
            let properties  = options |> getProperty "properties" (List<obj>())
            let openFile    = properties.Contains "openFile"
            let multiselect = properties.Contains "multiSelections"

            if openFile then
                use dialog = new OpenFileDialog()
                dialog.Title <- title
                dialog.Filter <- filter
                dialog.Multiselect <- multiselect
                dialog.InitialDirectory <- defaultPath

                parent.Invoke(Action (fun _ ->
                    match dialog.ShowDialog parent with
                    | DialogResult.OK -> for f in dialog.FileNames do filePaths.Add f
                    | _ -> ()
                )) |> ignore
            else
                use dialog = new FolderBrowserDialog()
                dialog.SelectedPath <- defaultPath
#if NET8_0_OR_GREATER
                dialog.UseDescriptionForTitle <- true
                dialog.Description <- title
#endif
                parent.Invoke(Action (fun _ ->
                    match dialog.ShowDialog(parent) with
                    | DialogResult.OK -> filePaths.Add dialog.SelectedPath
                    | _ -> ()
                )) |> ignore
        with exn ->
            Log.error $"[CEF] ShowOpenDialog failed: {exn}"

        result

    member _.ShowSaveDialog(options: IDictionary<string, obj>) =
        let result = Dictionary<string, obj>()
        result.["filePath"] <- ""

        try
            let title       = options |> getProperty "title" String.Empty
            let filter      = options |> getProperty "filters" (List<obj>()) |> getFilters
            let defaultPath = options |> getProperty "defaultPath" String.Empty

            use dialog = new SaveFileDialog(Title = title)
            dialog.Filter <- filter
            dialog.InitialDirectory <- defaultPath

            parent.Invoke(Action (fun _ ->
                match dialog.ShowDialog parent with
                | DialogResult.OK -> result.["filePath"] <- dialog.FileName
                | _ -> ()
            )) |> ignore
        with exn ->
            Log.error $"[CEF] ShowSaveDialog failed: {exn}"

        result